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
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Economy.Node;
public static partial class EconomyNodeHost
{
    public static IServiceCollection AddEconomyNode(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<EconomyNodeHostOptions>? configure = null)
    {
        EconomyNodeHostOptions options = ResolveOptions(configuration);
        configure?.Invoke(options);
        options.ApplyDevelopmentAdminDefaults();
        options.ValidateApiOptions();
        EconomyNodeAdminAccount treasuryAdminAccount = options.ValidateAndGetTreasuryAdminAccount();
        EconomyNodeAdminAccount? ledgerAdminAccount = options.ValidateAndGetLedgerAdminAccount();
        options.ValidateProductionSecurityOptions(treasuryAdminAccount, ledgerAdminAccount);

        services.AddProblemDetails();
        services.AddSingleton(options);
        services.AddHttpContextAccessor();
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, EconomyNodeAuthenticationStateProvider>();
        services.AddAntiforgery(antiforgery => antiforgery.HeaderName = "X-CSRF-TOKEN");
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
        services.AddScoped<LedgerExecutor>();
        services.AddScoped<EconomyLedgerReadModelService>();
        services.AddScoped<EconomyNodeService>();
        services.AddScoped<EconomyNodeBootstrapService>();
        services.AddScoped<EconomyLedgerCommandService>();
        services.AddScoped<EconomyTreasuryCommandService>();
        services.AddScoped<EconomyLedgerQueryService>();
        services.AddScoped<EconomyNodeHealthService>();
        services.AddScoped<EconomyNodeUiService>();
        return services;
    }

    private static void AddPersistence(IServiceCollection services, EconomyNodeHostOptions options)
    {
        switch (options.PersistenceProvider)
        {
            case EconomyNodePersistenceProvider.InMemory:
                services.AddEconomyInMemoryPersistence(options.InMemoryDatabaseName);
                return;

            case EconomyNodePersistenceProvider.PostgreSql:
                string connectionString = string.IsNullOrWhiteSpace(options.PostgreSqlConnectionString)
                    ? EconomyNodeHostDefaults.DefaultPostgreSqlConnectionString
                    : options.PostgreSqlConnectionString;
                services.AddEconomyPostgreSqlPersistence(
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

    private static void AddNodeAuth(IServiceCollection services, EconomyNodeHostOptions options)
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
            services.AddDbContext<EconomyNodeAuthDbContext>((_, dbOptions) =>
                ConfigureAuthDbContext(dbOptions, options));
            services.AddScoped<EconomyNodeJwtService>();
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
                .AddIdentityCore<EconomyNodeUser>(identity =>
                {
                    identity.User.RequireUniqueEmail = true;
                    identity.SignIn.RequireConfirmedAccount = false;
                    identity.Lockout.AllowedForNewUsers = true;
                    identity.Lockout.MaxFailedAccessAttempts = 5;
                    identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<EconomyNodeAuthDbContext>()
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
                jwt.TokenValidationParameters = EconomyNodeJwtService.CreateValidationParameters(options);
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        EconomyNodeJwtService validator = context.HttpContext.RequestServices
                            .GetRequiredService<EconomyNodeJwtService>();
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
            authorization.AddPolicy(EconomyNodeAuthPolicies.Administrator, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(EconomyNodeAuthPolicies.AdministratorRole);
            });
        });
    }

    private static void AddDataProtection(IServiceCollection services, EconomyNodeHostOptions options)
    {
        string keyRingPath = options.ResolveDataProtectionKeyRingPath();
        Directory.CreateDirectory(keyRingPath);
        IDataProtectionBuilder builder = services
            .AddDataProtection()
            .SetApplicationName("CanDoItAll.Economy.Node")
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

        if (OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }
    }

    private static void ConfigureAuthDbContext(
        DbContextOptionsBuilder dbOptions,
        EconomyNodeHostOptions options)
    {
        switch (options.AuthPersistenceProvider)
        {
            case EconomyNodeAuthPersistenceProvider.InMemory:
                dbOptions.UseInMemoryDatabase(options.AuthInMemoryDatabaseName);
                return;

            case EconomyNodeAuthPersistenceProvider.Sqlite:
                dbOptions.UseSqlite(options.ResolveAuthSqliteConnectionString());
                return;

            case EconomyNodeAuthPersistenceProvider.PostgreSql:
                dbOptions.UseNpgsql(options.ResolveAuthPostgreSqlConnectionString());
                return;

            default:
                throw new InvalidOperationException($"Unsupported economy node auth persistence provider: {options.AuthPersistenceProvider}.");
        }
    }

    private static void AddRateLimiting(IServiceCollection services, EconomyNodeHostOptions options)
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
        EconomyDbContext db,
        EconomyNodeHostOptions options,
        CancellationToken cancellationToken)
    {
        switch (options.SchemaManagementMode)
        {
            case EconomyNodeSchemaManagementMode.EnsureCreated:
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                return;

            case EconomyNodeSchemaManagementMode.Migrate:
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                return;

            case EconomyNodeSchemaManagementMode.None:
                return;

            default:
                throw new InvalidOperationException($"Unsupported economy schema management mode: {options.SchemaManagementMode}.");
        }
    }

    private static async Task ApplyAuthSchemaManagementAsync(
        EconomyNodeAuthDbContext db,
        EconomyNodeHostOptions options,
        CancellationToken cancellationToken)
    {
        switch (options.AuthSchemaManagementMode)
        {
            case EconomyNodeAuthSchemaManagementMode.EnsureCreated:
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                return;

            case EconomyNodeAuthSchemaManagementMode.Migrate:
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                return;

            case EconomyNodeAuthSchemaManagementMode.None:
                return;

            default:
                throw new InvalidOperationException($"Unsupported economy auth schema management mode: {options.AuthSchemaManagementMode}.");
        }
    }
}
