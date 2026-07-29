using System.Security.Claims;
using CanDoItAll.Ledger.Node;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Routing;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/Logout", async (
            ClaimsPrincipal user,
            [FromServices] SignInManager<LedgerNodeUser> signInManager,
            [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync().ConfigureAwait(false);
            string localReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
            return TypedResults.LocalRedirect($"~/{localReturnUrl.TrimStart('/')}");
        })
        .RequireAuthorization();

        accountGroup.MapGet("/Register", () => Results.NotFound("Public account registration is disabled for the Ledger node."))
            .AllowAnonymous();

        return accountGroup;
    }
}
