using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Rendering;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Node.Components;
using CanDoItAll.Ledger.Persistence.EFCore;
using CanDoItAll.Ledger.Persistence.PostgreSql;
using CanDoItAll.Ledger.Storage.InMemory;
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
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Ledger.Node;
public static partial class LedgerNodeHost
{
    private const string LedgerNodeCorsPolicyName = "CanDoItAll.Ledger.Node.Cors";

    public static IServiceCollection AddLedgerNode(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<LedgerNodeHostOptions>? configure = null)
    {
        LedgerNodeHostOptions options = ResolveOptions(configuration);
        configure?.Invoke(options);
        options.ApplyDevelopmentAdminDefaults();
        options.ValidateApiOptions();
        LedgerNodeAdminAccount treasuryAdminAccount = options.ValidateAndGetTreasuryAdminAccount();
        LedgerNodeAdminAccount? ledgerAdminAccount = options.ValidateAndGetLedgerAdminAccount();
        options.ValidateProductionSecurityOptions(treasuryAdminAccount, ledgerAdminAccount);

        services.AddProblemDetails();
        services.AddSingleton(options);
        services.Configure<KestrelServerOptions>(serverOptions =>
            serverOptions.Limits.MaxRequestBodySize = options.MaxRequestBodyBytes);
        services.AddHttpContextAccessor();
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, LedgerNodeAuthenticationStateProvider>();
        services.AddAntiforgery(antiforgery => antiforgery.HeaderName = "X-CSRF-TOKEN");
        AddCors(services, options);
        services.AddCanDoItAllBaseLib();
        services.AddCanDoItAllCharts();
        services.AddCanDoItAllMermaid();
        services.AddBusinessObjectComponentRenderers();
        services.AddBusinessObjectCasting();
        AddRateLimiting(services, options);
        AddNodeAuth(services, options);
        services.AddSingleton(new TokenCreationPolicyOptions
        {
            Enabled = options.EnforceTokenCreationPolicy,
            TreasuryAdminAddress = treasuryAdminAccount.Address
        });
        AddPersistence(services, options);
        services.AddScoped<ITransactionCanonicalizer, DefaultTransactionCanonicalizer>();
        services.AddScoped<ITransactionHasher, TransactionHasher>();
        services.AddScoped<ITokenDefinitionCanonicalizer, DefaultTokenDefinitionCanonicalizer>();
        services.AddScoped<ITokenDefinitionHasher, TokenDefinitionHasher>();
        services.AddScoped<ISignaturePayloadBuilder, SignaturePayloadBuilder>();
        services.AddSingleton<ISignatureProviderRegistry>(_ => SignatureProviderRegistry.CreateDefault());
        services.AddScoped<IAccountInputSigner, AccountInputSigner>();
        services.AddSingleton<ManualTimeProvider>(_ => new ManualTimeProvider(DateTimeOffset.UtcNow));
        services.AddSingleton<ITimeProvider>(serviceProvider => serviceProvider.GetRequiredService<ManualTimeProvider>());
        services.AddSingleton<ILedgerAdminSigner>(serviceProvider =>
            ledgerAdminAccount is null
                ? new DevelopmentLedgerAdminSigner()
                : new ConfiguredLedgerAdminSigner(
                    ledgerAdminAccount,
                    serviceProvider.GetRequiredService<ISignatureProviderRegistry>()));
        services.AddScoped<ITokenCreationPolicy, TokenCreationPolicy>();
        services.AddScoped<ITokenDefinitionPolicy, TokenDefinitionPolicy>();
        services.AddScoped<IMainCurrencyIssuancePolicy>(_ => new MainCurrencyIssuancePolicy(
            new MainCurrencyIssuancePolicyOptions { TreasuryAdminAddress = treasuryAdminAccount.Address }));
        services.AddScoped<InputReferenceValidator>();
        services.AddScoped<BusinessObjectTransitionValidator>();
        services.AddScoped<BusinessObjectReferenceValidator>();
        services.AddScoped<InputSignatureValidator>();
        services.AddScoped<AssetConservationValidator>();
        services.AddScoped<LedgerTransactionValidationPipeline>();
        services.AddScoped<ILedgerSemanticRuleSet, LedgerV1SemanticRuleSet>();
        services.AddScoped<ILedgerSemanticRuleSet, LedgerV2SemanticRuleSet>();
        services.AddScoped(serviceProvider => new LedgerSemanticRuleSetRegistry(
            serviceProvider.GetServices<ILedgerSemanticRuleSet>()));
        services.AddScoped<LedgerSemanticReplayVerifier>();
        services.AddScoped<LedgerExecutor>();
        services.AddScoped<LedgerReadModelService>();
        services.AddScoped<LedgerNodeService>();
        services.AddScoped<LedgerNodeBootstrapService>();
        services.AddScoped<LedgerCommandService>();
        services.AddScoped<LedgerTreasuryCommandService>();
        services.AddScoped<LedgerQueryService>();
        services.AddScoped<LedgerNodeHealthService>();
        services.AddScoped<LedgerNodeUiService>();
        return services;
    }

    private static void AddCors(IServiceCollection services, LedgerNodeHostOptions options)
    {
        string[] allowedOrigins = options.CorsAllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        services.AddCors(cors =>
        {
            cors.AddPolicy(LedgerNodeCorsPolicyName, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }

    private static void AddPersistence(IServiceCollection services, LedgerNodeHostOptions options)
    {
        switch (options.PersistenceProvider)
        {
            case LedgerNodePersistenceProvider.InMemory:
                services.AddLedgerInMemoryPersistence(options.InMemoryDatabaseName);
                return;

            case LedgerNodePersistenceProvider.PostgreSql:
                string connectionString = string.IsNullOrWhiteSpace(options.PostgreSqlConnectionString)
                    ? LedgerNodeHostDefaults.DefaultPostgreSqlConnectionString
                    : options.PostgreSqlConnectionString;
                services.AddLedgerPostgreSqlPersistence(
                    connectionString,
                    options.PostgreSqlMigrationsAssembly,
                    postgres =>
                    {
                        postgres.CommandTimeoutSeconds = options.PostgreSqlCommandTimeoutSeconds;
                        postgres.EnableRetryOnFailure = options.PostgreSqlEnableRetryOnFailure;
                    });
                return;

            default:
                throw new InvalidOperationException($"Unsupported economy persistence provider: {options.PersistenceProvider}.");
        }
    }

    private static void AddNodeAuth(IServiceCollection services, LedgerNodeHostOptions options)
    {
        if (options.UseForwardedHeaders)
        {
            services.Configure<ForwardedHeadersOptions>(forwardedHeaders =>
            {
                forwardedHeaders.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwardedHeaders.KnownIPNetworks.Clear();
                forwardedHeaders.KnownProxies.Clear();
            });
        }

        if (options.UsesAuthPersistence)
        {
            services.AddDbContext<LedgerNodeAuthDbContext>((_, dbOptions) =>
                ConfigureAuthDbContext(dbOptions, options));
            services.AddScoped<LedgerNodeJwtService>();
        }

        AuthenticationBuilder? jwtAuthenticationBuilder = null;
        if (options.UsesCookieUiAuth)
        {
            AddDataProtection(services, options);
            services
                .AddAuthentication(authOptions =>
                {
                    authOptions.DefaultScheme = IdentityConstants.ApplicationScheme;
                    authOptions.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                })
                .AddIdentityCookies();

            services
                .AddIdentityCore<LedgerNodeUser>(identity =>
                {
                    identity.User.RequireUniqueEmail = true;
                    identity.SignIn.RequireConfirmedAccount = false;
                    identity.Lockout.AllowedForNewUsers = true;
                    identity.Lockout.MaxFailedAccessAttempts = 5;
                    identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<LedgerNodeAuthDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

        }
        else if (options.UsesJwtApiAuth)
        {
            jwtAuthenticationBuilder = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
        }

        if (options.UsesJwtApiAuth)
        {
            jwtAuthenticationBuilder ??= services.AddAuthentication();
            jwtAuthenticationBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                jwt.TokenValidationParameters = LedgerNodeJwtService.CreateValidationParameters(options);
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        LedgerNodeJwtService validator = context.HttpContext.RequestServices
                            .GetRequiredService<LedgerNodeJwtService>();
                        bool active = await validator
                            .ValidateActiveTokenAsync(context.Principal ?? new ClaimsPrincipal(), context.HttpContext.RequestAborted)
                            .ConfigureAwait(false);
                        if (!active)
                        {
                            context.Fail("JWT token record is not active.");
                        }
                    }
                };
            });
        }

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(LedgerNodeAuthPolicies.Administrator, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(LedgerNodeAuthPolicies.AdministratorRole);
            });
            authorization.AddPolicy(LedgerNodeAuthPolicies.ReadApi, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new LedgerNodeScopeRequirement(LedgerNodeScopes.Read));
            });
            authorization.AddPolicy(LedgerNodeAuthPolicies.WriteApi, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new LedgerNodeScopeRequirement(LedgerNodeScopes.Write));
            });
            authorization.AddPolicy(LedgerNodeAuthPolicies.AdminApi, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new LedgerNodeScopeRequirement(LedgerNodeScopes.Admin));
            });
        });
        services.AddSingleton<IAuthorizationHandler, LedgerNodeScopeAuthorizationHandler>();
    }

    private static void AddDataProtection(IServiceCollection services, LedgerNodeHostOptions options)
    {
        string keyRingPath = options.ResolveDataProtectionKeyRingPath();
        Directory.CreateDirectory(keyRingPath);
        IDataProtectionBuilder builder = services
            .AddDataProtection()
            .SetApplicationName("CanDoItAll.Ledger.Node")
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

        if (OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }
    }

    private static void ConfigureAuthDbContext(
        DbContextOptionsBuilder dbOptions,
        LedgerNodeHostOptions options)
    {
        switch (options.AuthPersistenceProvider)
        {
            case LedgerNodeAuthPersistenceProvider.InMemory:
                dbOptions.UseInMemoryDatabase(options.AuthInMemoryDatabaseName);
                return;

            case LedgerNodeAuthPersistenceProvider.Sqlite:
                dbOptions.UseSqlite(options.ResolveAuthSqliteConnectionString());
                return;

            case LedgerNodeAuthPersistenceProvider.PostgreSql:
                dbOptions.UseNpgsql(options.ResolveAuthPostgreSqlConnectionString());
                return;

            default:
                throw new InvalidOperationException($"Unsupported Ledger node auth persistence provider: {options.AuthPersistenceProvider}.");
        }
    }

    private static void AddRateLimiting(IServiceCollection services, LedgerNodeHostOptions options)
    {
        int readLimit = options.EnableRateLimiting ? options.ReadRateLimitPerMinute : int.MaxValue;
        int writeLimit = options.EnableRateLimiting ? options.WriteRateLimitPerMinute : int.MaxValue;
        int deepHealthLimit = options.EnableRateLimiting ? options.DeepHealthRateLimitPerMinute : int.MaxValue;
        services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiter.AddFixedWindowLimiter(ReadRateLimitPolicy, limiter =>
            {
                limiter.PermitLimit = readLimit;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            rateLimiter.AddFixedWindowLimiter(WriteRateLimitPolicy, limiter =>
            {
                limiter.PermitLimit = writeLimit;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            rateLimiter.AddFixedWindowLimiter(DeepHealthRateLimitPolicy, limiter =>
            {
                limiter.PermitLimit = deepHealthLimit;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });
    }

    private static async Task ApplySchemaManagementAsync(
        LedgerDbContext db,
        LedgerNodeHostOptions options,
        ILedgerSchemaMigrator schemaMigrator,
        CancellationToken cancellationToken)
    {
        switch (options.SchemaManagementMode)
        {
            case LedgerNodeSchemaManagementMode.EnsureCreated:
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                return;

            case LedgerNodeSchemaManagementMode.Migrate:
                await schemaMigrator.MigrateAsync(db, cancellationToken).ConfigureAwait(false);
                return;

            case LedgerNodeSchemaManagementMode.None:
                return;

            default:
                throw new InvalidOperationException($"Unsupported economy schema management mode: {options.SchemaManagementMode}.");
        }
    }

    private static async Task ApplyAuthSchemaManagementAsync(
        LedgerNodeAuthDbContext db,
        LedgerNodeHostOptions options,
        CancellationToken cancellationToken)
    {
        switch (options.AuthSchemaManagementMode)
        {
            case LedgerNodeAuthSchemaManagementMode.EnsureCreated:
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                return;

            case LedgerNodeAuthSchemaManagementMode.Migrate:
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                return;

            case LedgerNodeAuthSchemaManagementMode.None:
                return;

            default:
                throw new InvalidOperationException($"Unsupported economy auth schema management mode: {options.AuthSchemaManagementMode}.");
        }
    }
}
