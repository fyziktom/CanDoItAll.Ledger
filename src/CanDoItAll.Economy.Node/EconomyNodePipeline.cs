using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Rendering;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Node.Components;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.PostgreSql;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Economy.Node;
public static partial class EconomyNodeHost
{
    public const string ExternalModelsRequestPath = "/assets/external-models";

    public static void ConfigurePipeline(WebApplication app)
    {
        EconomyNodeHostOptions options = app.Services.GetRequiredService<EconomyNodeHostOptions>();

        if (options.UseForwardedHeaders)
        {
            app.UseForwardedHeaders();
        }

        app.UseExceptionHandler();
        if (options.EnableHsts && !app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        if (options.EnableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        if (options.UsesAspNetAuthentication)
        {
            app.UseAuthentication();
        }

        if (options.UsesCookieUiAuth)
        {
            app.Use(BlockPublicIdentityRegistrationAsync);
            app.Use(ProtectNodeConsoleStaticAssetsAsync);
        }

        if (options.UsesCookieUiAuth && options.RequireTwoFactorAuthentication)
        {
            app.Use(RequireTwoFactorEnrollmentAsync);
        }

        app.UseStaticFiles();
        UseExternalModelStaticFiles(app, options);

        if (options.UsesAspNetAuthentication)
        {
            app.UseAuthorization();
        }

        app.UseAntiforgery();
        app.UseRateLimiter();

        MapEconomyApi(app.MapGroup(options.VersionedApiBasePath));

        if (options.MapLegacyApiRoutes)
        {
            MapEconomyApi(app.MapGroup("/api"));
        }

        if (options.UsesCookieUiAuth)
        {
            MapAccountRedirects(app);
            app.MapAdditionalIdentityEndpoints();
        }

        if (options.UsesJwtApiAuth && options.UsesCookieUiAuth)
        {
            MapJwtAdminEndpoints(app);
        }

        app.MapStaticAssets();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
    }

    private static async Task ProtectNodeConsoleStaticAssetsAsync(HttpContext context, RequestDelegate next)
    {
        if (!RequiresUiAuthentication(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    private static async Task BlockPublicIdentityRegistrationAsync(HttpContext context, RequestDelegate next)
    {
        PathString path = context.Request.Path;
        if (path.StartsWithSegments("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Account/Register", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Public account registration is disabled for the Economy node.").ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool RequiresUiAuthentication(PathString path)
    {
        if (!path.HasValue || path == PathString.Empty || path == "/")
        {
            return true;
        }

        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Identity", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/admin/jwts", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(ExternalModelsRequestPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static string? ResolveExternalModelRoot(string contentRootPath, string? configuredRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        List<string> candidates = [];
        AddConfiguredCandidates(candidates, contentRootPath, configuredRoot);
        AddCandidate(candidates, Path.Combine(contentRootPath, "wwwroot", "assets", "external-models"));
        AddCandidate(candidates, Path.Combine(contentRootPath, "3DModels"));

        DirectoryInfo? directory = new DirectoryInfo(contentRootPath);
        while (directory is not null)
        {
            AddCandidate(candidates, Path.Combine(directory.FullName, "CanDoItAll.Components", "3DModels"));
            directory = directory.Parent;
        }

        foreach (string candidate in candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(Path.Combine(candidate, "glb")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void UseExternalModelStaticFiles(WebApplication app, EconomyNodeHostOptions options)
    {
        string? modelRoot = ResolveExternalModelRoot(app.Environment.ContentRootPath, options.ExternalModelRoot);
        if (string.IsNullOrWhiteSpace(modelRoot))
        {
            return;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(modelRoot),
            RequestPath = ExternalModelsRequestPath,
            ContentTypeProvider = CreateExternalModelContentTypeProvider()
        });
    }

    private static FileExtensionContentTypeProvider CreateExternalModelContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".glb"] = "model/gltf-binary";
        return provider;
    }

    private static void AddConfiguredCandidates(List<string> candidates, string contentRootPath, string? configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return;
        }

        string root = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(contentRootPath, configuredRoot);
        AddCandidate(candidates, root);
        AddCandidate(candidates, Path.Combine(root, "3DModels"));
    }

    private static void AddCandidate(List<string> candidates, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.Add(path);
        }
    }

    private static async Task RequireTwoFactorEnrollmentAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User?.Identity?.IsAuthenticated != true || ShouldBypassTwoFactorEnrollment(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        UserManager<EconomyNodeUser> userManager = context.RequestServices.GetRequiredService<UserManager<EconomyNodeUser>>();
        EconomyNodeUser? user = await userManager.GetUserAsync(context.User).ConfigureAwait(false);
        if (user is null || await userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Two-factor authentication enrollment is required.").ConfigureAwait(false);
            return;
        }

        string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/Account/Manage/EnableAuthenticator?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    private static bool ShouldBypassTwoFactorEnrollment(PathString path)
        => path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/Identity/Account/Manage/EnableAuthenticator", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/Identity/Account/Manage/TwoFactorAuthentication", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/Identity/Account/Manage/GenerateRecoveryCodes", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/Identity/Account/Manage/ResetAuthenticator", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/Identity/Account/Manage/Disable2fa", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/favicon", StringComparison.OrdinalIgnoreCase);

    private static void MapAccountRedirects(WebApplication app)
    {
        app.MapGet("/Identity/Account/Login", (HttpContext context) =>
            Results.Redirect($"/Account/Login{context.Request.QueryString}"))
            .AllowAnonymous();
        app.MapGet("/Identity/Account/LoginWith2fa", (HttpContext context) =>
            Results.Redirect($"/Account/LoginWith2fa{context.Request.QueryString}"))
            .AllowAnonymous();
        app.MapGet("/Identity/Account/LoginWithRecoveryCode", (HttpContext context) =>
            Results.Redirect($"/Account/LoginWith2fa{context.Request.QueryString}"))
            .AllowAnonymous();
        app.MapGet("/Identity/Account/Manage", () => Results.Redirect("/Account/Manage"))
            .RequireAuthorization();
        app.MapGet("/Identity/Account/Manage/EnableAuthenticator", () => Results.Redirect("/Account/Manage/EnableAuthenticator"))
            .RequireAuthorization();
        app.MapGet("/Identity/Account/Manage/TwoFactorAuthentication", () => Results.Redirect("/Account/Manage/EnableAuthenticator"))
            .RequireAuthorization();
    }

    private static void MapJwtAdminEndpoints(WebApplication app)
    {
        RouteGroupBuilder admin = app.MapGroup("/admin/jwts")
            .RequireAuthorization(EconomyNodeAuthPolicies.Administrator);

        admin.MapGet("/", async (
            HttpContext context,
            EconomyNodeJwtService jwtService,
            IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            AntiforgeryTokenSet tokenSet = antiforgery.GetAndStoreTokens(context);
            IReadOnlyList<EconomyNodeJwtTokenSummary> tokens = await jwtService
                .ListTokensAsync(cancellationToken)
                .ConfigureAwait(false);
            return Results.Content(RenderJwtAdminPage(tokens, tokenSet.RequestToken, null, null), "text/html; charset=utf-8");
        });

        admin.MapPost("/create", async (
            HttpContext context,
            EconomyNodeJwtService jwtService,
            IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
                IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
                string name = form["name"].ToString();
                string scopes = form["scopes"].ToString();
                int lifetimeMinutes = int.TryParse(form["lifetimeMinutes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : 60;
                EconomyNodeJwtIssuedToken issued = await jwtService
                    .CreateTokenAsync(context.User, name, TimeSpan.FromMinutes(lifetimeMinutes), scopes, cancellationToken)
                    .ConfigureAwait(false);
                AntiforgeryTokenSet tokenSet = antiforgery.GetAndStoreTokens(context);
                IReadOnlyList<EconomyNodeJwtTokenSummary> tokens = await jwtService
                    .ListTokensAsync(cancellationToken)
                    .ConfigureAwait(false);
                return Results.Content(RenderJwtAdminPage(tokens, tokenSet.RequestToken, issued.Token, null), "text/html; charset=utf-8");
            }
            catch (Exception ex) when (ex is InvalidOperationException or AntiforgeryValidationException)
            {
                AntiforgeryTokenSet tokenSet = antiforgery.GetAndStoreTokens(context);
                IReadOnlyList<EconomyNodeJwtTokenSummary> tokens = await jwtService
                    .ListTokensAsync(cancellationToken)
                    .ConfigureAwait(false);
                return Results.Content(RenderJwtAdminPage(tokens, tokenSet.RequestToken, null, ex.Message), "text/html; charset=utf-8");
            }
        });

        admin.MapPost("/revoke", async (
            HttpContext context,
            EconomyNodeJwtService jwtService,
            IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
            IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            await jwtService.RevokeTokenAsync(form["jti"].ToString(), context.User, cancellationToken).ConfigureAwait(false);
            return Results.Redirect("/admin/jwts/");
        });
    }

    private static string RenderJwtAdminPage(
        IReadOnlyList<EconomyNodeJwtTokenSummary> tokens,
        string? requestVerificationToken,
        string? issuedToken,
        string? error)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("<title>Economy Node JWTs</title>");
        builder.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:0;background:#f6f7f9;color:#172033}.shell{max-width:1120px;margin:0 auto;padding:32px}.panel{background:#fff;border:1px solid #d9dee8;border-radius:8px;padding:20px;margin-bottom:20px}label{display:block;font-weight:600;margin-top:12px}input{width:100%;box-sizing:border-box;padding:10px;border:1px solid #b7c0cf;border-radius:6px}button,.button{margin-top:14px;background:#1f6feb;color:#fff;border:0;border-radius:6px;padding:10px 14px;text-decoration:none;display:inline-block;cursor:pointer}textarea{width:100%;min-height:130px;margin-top:10px;font-family:Consolas,monospace}.error{color:#9f1239}.success{color:#166534}table{width:100%;border-collapse:collapse}td,th{border-bottom:1px solid #e2e8f0;padding:8px;text-align:left}.muted{color:#5f6b7a}</style>");
        builder.AppendLine("</head><body><main class=\"shell\"><p><a href=\"/\">Node console</a> · <a href=\"/Account/Manage\">Account</a></p><h1>API JWTs</h1>");
        if (!string.IsNullOrWhiteSpace(error))
        {
            builder.Append("<p class=\"error\">").Append(WebUtility.HtmlEncode(error)).AppendLine("</p>");
        }

        if (!string.IsNullOrWhiteSpace(issuedToken))
        {
            builder.AppendLine("<section class=\"panel\"><h2>Token created</h2><p class=\"success\">Copy this token now. It is shown only once.</p>");
            builder.Append("<textarea readonly>").Append(WebUtility.HtmlEncode(issuedToken)).AppendLine("</textarea></section>");
        }

        builder.AppendLine("<section class=\"panel\"><h2>Create token</h2><form method=\"post\" action=\"/admin/jwts/create\">");
        AppendAntiforgery(builder, requestVerificationToken);
        builder.AppendLine("<label>Name<input name=\"name\" autocomplete=\"off\" value=\"node api token\"></label>");
        builder.AppendLine("<label>Lifetime minutes<input name=\"lifetimeMinutes\" type=\"number\" min=\"1\" value=\"60\"></label>");
        builder.AppendLine("<label>Scopes<input name=\"scopes\" autocomplete=\"off\" value=\"node.api\"></label>");
        builder.AppendLine("<button type=\"submit\">Create JWT</button></form></section>");
        builder.AppendLine("<section class=\"panel\"><h2>Issued tokens</h2><table><thead><tr><th>Name</th><th>Expires</th><th>Status</th><th>Scopes</th><th></th></tr></thead><tbody>");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (EconomyNodeJwtTokenSummary token in tokens)
        {
            builder.Append("<tr><td>").Append(WebUtility.HtmlEncode(token.Name)).Append("</td><td>")
                .Append(WebUtility.HtmlEncode(token.ExpiresAt.ToString("u", CultureInfo.InvariantCulture))).Append("</td><td>")
                .Append(token.IsActive(now) ? "active" : "inactive").Append("</td><td>")
                .Append(WebUtility.HtmlEncode(token.Scopes)).Append("</td><td>");
            if (token.RevokedAt is null)
            {
                builder.Append("<form method=\"post\" action=\"/admin/jwts/revoke\">");
                AppendAntiforgery(builder, requestVerificationToken);
                builder.Append("<input type=\"hidden\" name=\"jti\" value=\"").Append(WebUtility.HtmlEncode(token.Jti)).Append("\">");
                builder.Append("<button type=\"submit\">Revoke</button></form>");
            }
            else
            {
                builder.Append("<span class=\"muted\">revoked</span>");
            }

            builder.AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table></section></main></body></html>");
        return builder.ToString();
    }

    private static void AppendAntiforgery(StringBuilder builder, string? requestVerificationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestVerificationToken))
        {
            builder.Append("<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"")
                .Append(WebUtility.HtmlEncode(requestVerificationToken))
                .AppendLine("\">");
        }
    }
}
