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
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Economy.Node;


public static partial class EconomyNodeHost
{
    private const string ReadRateLimitPolicy = "economy-node-read";
    private const string WriteRateLimitPolicy = "economy-node-write";
    private const string DeepHealthRateLimitPolicy = "economy-node-deep-health";

    public static WebApplicationBuilder CreateBuilder(
        string[]? args = null,
        Action<EconomyNodeHostOptions>? configure = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args ?? Array.Empty<string>(),
            ApplicationName = typeof(EconomyNodeHost).Assembly.GetName().Name
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.Services.AddEconomyNode(builder.Configuration, configure);
        return builder;
    }

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        builder.Environment.ApplicationName = typeof(EconomyNodeHost).Assembly.GetName().Name!;
        WebApplication app = builder.Build();
        ConfigurePipeline(app);
        return app;
    }

    public static async Task<WebApplication> StartAsync(
        string[]? args = null,
        Action<EconomyNodeHostOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        WebApplication app = Build(CreateBuilder(args, configure));
        await InitializeAsync(app.Services, cancellationToken).ConfigureAwait(false);
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return app;
    }


    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        EconomyDbContext db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<EconomyDbContext>>()
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (db)
        {
            await ApplySchemaManagementAsync(db, scope.ServiceProvider.GetRequiredService<EconomyNodeHostOptions>(), cancellationToken)
                .ConfigureAwait(false);
            EconomyNodeHostOptions options = scope.ServiceProvider.GetRequiredService<EconomyNodeHostOptions>();
            if (options.BootstrapTreasuryAdmin)
            {
                LedgerExecutor executor = scope.ServiceProvider.GetRequiredService<LedgerExecutor>();
                ITimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<ITimeProvider>();
                await EconomyNodeBootstrapper.BootstrapTreasuryAdminAsync(db, executor, timeProvider, options, cancellationToken).ConfigureAwait(false);
            }
        }

        await InitializeAuthAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InitializeAuthAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        EconomyNodeHostOptions options = services.GetRequiredService<EconomyNodeHostOptions>();
        if (!options.UsesAuthPersistence)
        {
            return;
        }

        EconomyNodeAuthDbContext db = services.GetRequiredService<EconomyNodeAuthDbContext>();
        await ApplyAuthSchemaManagementAsync(db, options, cancellationToken).ConfigureAwait(false);

        if (!options.UsesCookieUiAuth)
        {
            return;
        }

        RoleManager<IdentityRole> roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<EconomyNodeUser> userManager = services.GetRequiredService<UserManager<EconomyNodeUser>>();

        if (!await roleManager.RoleExistsAsync(EconomyNodeAuthPolicies.AdministratorRole).ConfigureAwait(false))
        {
            IdentityResult createRole = await roleManager
                .CreateAsync(new IdentityRole(EconomyNodeAuthPolicies.AdministratorRole))
                .ConfigureAwait(false);
            if (!createRole.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create economy node administrator role: {FormatIdentityErrors(createRole)}");
            }
        }

        if (options.SeedAdminUser)
        {
            await SeedAdminUserAsync(userManager, options).ConfigureAwait(false);
        }
    }

    private static async Task SeedAdminUserAsync(
        UserManager<EconomyNodeUser> userManager,
        EconomyNodeHostOptions options)
    {
        options.ValidateAdminSeedOptions();
        string email = options.AdminEmail!.Trim();
        EconomyNodeUser? user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new EconomyNodeUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                LockoutEnabled = true
            };
            IdentityResult createUser = await userManager.CreateAsync(user, options.AdminPassword!).ConfigureAwait(false);
            if (!createUser.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create economy node administrator user: {FormatIdentityErrors(createUser)}");
            }
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            IdentityResult updateUser = await userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!updateUser.Succeeded)
            {
                throw new InvalidOperationException($"Unable to confirm economy node administrator user: {FormatIdentityErrors(updateUser)}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, EconomyNodeAuthPolicies.AdministratorRole).ConfigureAwait(false))
        {
            IdentityResult addRole = await userManager.AddToRoleAsync(user, EconomyNodeAuthPolicies.AdministratorRole).ConfigureAwait(false);
            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException($"Unable to assign economy node administrator role: {FormatIdentityErrors(addRole)}");
            }
        }
    }

    private static string FormatIdentityErrors(IdentityResult result)
        => string.Join("; ", result.Errors.Select(error => error.Description));






    private static EconomyNodeHostOptions ResolveOptions(IConfiguration configuration)
    {
        string treasuryKeyId = GetValue(
            configuration,
            EconomyNodeHostDefaults.TreasuryAdminKeyId,
            "Economy:Treasury:AdminKeyId",
            "Economy:Node:TreasuryAdminKeyId");
        string treasuryPublicKey = GetValue(
            configuration,
            EconomyNodeHostDefaults.TreasuryAdminPublicKeyMaterial,
            "Economy:Treasury:AdminPublicKey",
            "Economy:Node:TreasuryAdminPublicKey");
        string treasuryPrivateKey = GetValue(
            configuration,
            EconomyNodeHostDefaults.TreasuryAdminPrivateKeyMaterial,
            "Economy:Treasury:AdminPrivateKey",
            "Economy:Node:TreasuryAdminPrivateKey");
        EconomyNodeAdminAccount treasuryAdminAccount = EconomyNodeAdminAccount.FromText(
            SignatureAlgorithmId.EcdsaP256Sha256,
            treasuryKeyId,
            treasuryPublicKey,
            treasuryPrivateKey);

        string? apiKey = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_API_KEY"),
            "Economy:Api:Key",
            "Economy:Node:ApiKey");

        var options = new EconomyNodeHostOptions
        {
            ApiVersion = GetValue(
                configuration,
                EconomyNodeHostDefaults.ApiVersion,
                "Economy:Api:Version",
                "Economy:Node:ApiVersion"),
            SecurityProfile = GetEnum(
                configuration,
                EconomyNodeSecurityProfile.Development,
                "Economy:Security:Profile",
                "Economy:Node:SecurityProfile"),
            MapLegacyApiRoutes = GetBool(
                configuration,
                true,
                "Economy:Api:MapLegacyRoutes",
                "Economy:Node:MapLegacyApiRoutes"),
            ApiSecurityMode = GetEnum(
                configuration,
                EconomyNodeApiSecurityMode.Development,
                "Economy:Auth:Api:WriteMode",
                "Economy:Auth:Api:Mode",
                "Economy:Api:SecurityMode",
                "Economy:Node:ApiSecurityMode"),
            ReadSecurityMode = GetEnum(
                configuration,
                EconomyNodeApiReadSecurityMode.Public,
                "Economy:Auth:Api:ReadMode",
                "Economy:Auth:Api:Mode",
                "Economy:Api:ReadSecurityMode",
                "Economy:Node:ReadSecurityMode"),
            ApiKeyHeaderName = GetValue(
                configuration,
                EconomyNodeHostDefaults.ApiKeyHeaderName,
                "Economy:Api:KeyHeaderName",
                "Economy:Node:ApiKeyHeaderName"),
            ApiKey = apiKey,
            VpsHostingMode = GetBool(
                configuration,
                false,
                "Economy:Hosting:VpsMode",
                "Economy:Auth:VpsMode"),
            UiAuthMode = GetEnum(
                configuration,
                EconomyNodeUiAuthMode.Disabled,
                "Economy:Auth:Ui:Mode",
                "Economy:Node:UiAuthMode"),
            RequireTwoFactorAuthentication = GetBool(
                configuration,
                false,
                "Economy:Auth:Ui:RequireTwoFactor",
                "Economy:Auth:Ui:RequireTwoFactorAuthentication",
                "Economy:Node:RequireTwoFactorAuthentication"),
            SeedAdminUser = GetBool(
                configuration,
                false,
                "Economy:Auth:Ui:SeedAdmin",
                "Economy:Auth:Ui:SeedAdminUser",
                "Economy:Node:SeedAdminUser"),
            AllowExistingAdminUser = GetBool(
                configuration,
                false,
                "Economy:Auth:Ui:AllowExistingAdmin",
                "Economy:Auth:Ui:AllowExistingAdminUser",
                "Economy:Node:AllowExistingAdminUser"),
            UseDevelopmentAdminDefaults = GetBool(
                configuration,
                false,
                "Economy:Auth:Ui:UseDevelopmentAdminDefaults",
                "Economy:Node:UseDevelopmentAdminDefaults"),
            AdminEmail = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_ADMIN_EMAIL"),
                "Economy:Auth:Ui:AdminEmail",
                "Economy:Node:AdminEmail"),
            AdminPassword = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_ADMIN_PASSWORD"),
                "Economy:Auth:Ui:AdminPassword",
                "Economy:Node:AdminPassword"),
            AuthPersistenceProvider = GetEnum(
                configuration,
                EconomyNodeAuthPersistenceProvider.InMemory,
                "Economy:Auth:Persistence:Provider",
                "Economy:Node:AuthPersistenceProvider"),
            AuthSchemaManagementMode = GetEnum(
                configuration,
                EconomyNodeAuthSchemaManagementMode.EnsureCreated,
                "Economy:Auth:Persistence:SchemaManagementMode",
                "Economy:Node:AuthSchemaManagementMode"),
            AuthInMemoryDatabaseName = GetValue(
                configuration,
                EconomyNodeHostDefaults.DefaultAuthInMemoryDatabaseName,
                "Economy:Auth:Persistence:InMemoryDatabaseName",
                "Economy:Node:AuthInMemoryDatabaseName"),
            AuthSqliteConnectionString = GetOptionalValue(
                configuration,
                null,
                "Economy:Auth:Sqlite:ConnectionString",
                "Economy:Auth:Persistence:SqliteConnectionString"),
            AuthPostgreSqlConnectionString = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_AUTH_CONNECTION"),
                "Economy:Auth:PostgreSql:ConnectionString",
                "Economy:Auth:Persistence:PostgreSqlConnectionString"),
            JwtIssuer = GetValue(
                configuration,
                EconomyNodeHostDefaults.JwtIssuer,
                "Economy:Auth:Jwt:Issuer",
                "Economy:Node:JwtIssuer"),
            JwtAudience = GetValue(
                configuration,
                EconomyNodeHostDefaults.JwtAudience,
                "Economy:Auth:Jwt:Audience",
                "Economy:Node:JwtAudience"),
            JwtSigningKey = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_JWT_SIGNING_KEY"),
                "Economy:Auth:Jwt:SigningKey",
                "Economy:Node:JwtSigningKey"),
            JwtDefaultLifetimeMinutes = GetInt(
                configuration,
                60,
                "Economy:Auth:Jwt:DefaultLifetimeMinutes",
                "Economy:Node:JwtDefaultLifetimeMinutes"),
            JwtMaxLifetimeDays = GetInt(
                configuration,
                30,
                "Economy:Auth:Jwt:MaxLifetimeDays",
                "Economy:Node:JwtMaxLifetimeDays"),
            JwtRequireActiveTokenRecord = GetBool(
                configuration,
                true,
                "Economy:Auth:Jwt:RequireActiveTokenRecord",
                "Economy:Node:JwtRequireActiveTokenRecord"),
            DataProtectionKeyRingPath = GetOptionalValue(
                configuration,
                null,
                "Economy:Auth:DataProtection:KeyRingPath",
                "DataProtection:KeyRingPath"),
            DataProtectionCertificatePath = GetOptionalValue(
                configuration,
                null,
                "Economy:Auth:DataProtection:CertificatePath",
                "DataProtection:CertificatePath"),
            DataProtectionCertificatePassword = GetOptionalValue(
                configuration,
                null,
                "Economy:Auth:DataProtection:CertificatePassword",
                "DataProtection:CertificatePassword"),
            UseForwardedHeaders = GetBool(
                configuration,
                false,
                "Economy:Hosting:UseForwardedHeaders",
                "Economy:Node:UseForwardedHeaders"),
            EnableHttpsRedirection = GetBool(
                configuration,
                false,
                "Economy:Hosting:EnableHttpsRedirection",
                "Economy:Node:EnableHttpsRedirection"),
            EnableHsts = GetBool(
                configuration,
                false,
                "Economy:Hosting:EnableHsts",
                "Economy:Node:EnableHsts"),
            CorsAllowedOrigins = GetStringList(
                configuration,
                "Economy:Api:CorsAllowedOrigins",
                "Economy:Node:CorsAllowedOrigins"),
            InMemoryDatabaseName = GetValue(
                configuration,
                EconomyEfCoreServiceCollectionExtensions.DefaultInMemoryDatabaseName,
                "Economy:InMemory:DatabaseName",
                "Economy:Persistence:InMemoryDatabaseName"),
            PostgreSqlConnectionString = GetValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_CONNECTION"),
                "Economy:PostgreSql:ConnectionString"),
            PostgreSqlMigrationsAssembly = configuration["Economy:PostgreSql:MigrationsAssembly"],
            PostgreSqlCommandTimeoutSeconds = GetNullableInt(
                configuration,
                30,
                "Economy:PostgreSql:CommandTimeoutSeconds"),
            PostgreSqlEnableRetryOnFailure = GetBool(
                configuration,
                false,
                "Economy:PostgreSql:EnableRetryOnFailure"),
            SchemaManagementMode = GetEnum(
                configuration,
                EconomyNodeSchemaManagementMode.EnsureCreated,
                "Economy:Persistence:SchemaManagementMode",
                "Economy:Node:SchemaManagementMode"),
            BootstrapTreasuryAdmin = GetBool(
                configuration,
                true,
                "Economy:Treasury:Bootstrap",
                "Economy:Node:BootstrapTreasuryAdmin"),
            AllowLedgerReset = GetBool(
                configuration,
                true,
                "Economy:Api:AllowLedgerReset",
                "Economy:Node:AllowLedgerReset"),
            EnforceTokenCreationPolicy = GetBool(
                configuration,
                true,
                "Economy:TokenCreationPolicy:Enabled",
                "Economy:Node:EnforceTokenCreationPolicy"),
            TreasuryAdminAccount = treasuryAdminAccount,
            TreasuryAdminAddress = GetValue(
                configuration,
                treasuryAdminAccount.Address.Value,
                "Economy:Treasury:AdminAddress",
                "Economy:Node:TreasuryAdminAddress"),
            TreasuryAdminMainAmount = GetDecimal(
                configuration,
                decimal.MaxValue,
                "Economy:Treasury:AdminMainAmount",
                "Economy:Node:TreasuryAdminMainAmount"),
            EnableRateLimiting = GetBool(
                configuration,
                true,
                "Economy:Api:EnableRateLimiting",
                "Economy:Node:EnableRateLimiting"),
            ReadRateLimitPerMinute = GetInt(
                configuration,
                120,
                "Economy:Api:ReadRateLimitPerMinute",
                "Economy:Node:ReadRateLimitPerMinute"),
            WriteRateLimitPerMinute = GetInt(
                configuration,
                30,
                "Economy:Api:WriteRateLimitPerMinute",
                "Economy:Node:WriteRateLimitPerMinute"),
            DeepHealthRateLimitPerMinute = GetInt(
                configuration,
                12,
                "Economy:Api:DeepHealthRateLimitPerMinute",
                "Economy:Node:DeepHealthRateLimitPerMinute"),
            MaxRequestBodyBytes = GetLong(
                configuration,
                1_048_576,
                "Economy:Api:MaxRequestBodyBytes",
                "Economy:Node:MaxRequestBodyBytes"),
            MaxTimeAdvanceDays = GetDouble(
                configuration,
                3650,
                "Economy:Api:MaxTimeAdvanceDays",
                "Economy:Node:MaxTimeAdvanceDays"),
            ExternalModelRoot = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_COMPONENTS_3DMODELS_ROOT"),
                "Economy:WebGl:ExternalModelRoot",
                "Economy:Node:ExternalModelRoot")
        };

        options.LedgerAdminAccount = ResolveLedgerAdminAccount(configuration);

        string? providerText = GetValue(
            configuration,
            null,
            "Economy:Persistence:Provider",
            "Economy:Node:PersistenceProvider");

        if (!string.IsNullOrWhiteSpace(providerText)
            && Enum.TryParse(providerText, ignoreCase: true, out EconomyNodePersistenceProvider provider))
        {
            options.PersistenceProvider = provider;
        }
        else if (!string.IsNullOrWhiteSpace(providerText))
        {
            throw new InvalidOperationException($"Unknown economy persistence provider: {providerText}.");
        }
        else if (!string.IsNullOrWhiteSpace(options.PostgreSqlConnectionString)
            && string.IsNullOrWhiteSpace(providerText))
        {
            options.PersistenceProvider = EconomyNodePersistenceProvider.PostgreSql;
        }

        return options;
    }

    private static EconomyNodeAdminAccount? ResolveLedgerAdminAccount(IConfiguration configuration)
    {
        string? keyId = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_KEY_ID"),
            "Economy:LedgerAdmin:KeyId",
            "Economy:Node:LedgerAdminKeyId");
        string? publicKey = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY"),
            "Economy:LedgerAdmin:PublicKey",
            "Economy:Node:LedgerAdminPublicKey");
        string? privateKey = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_PRIVATE_KEY"),
            "Economy:LedgerAdmin:PrivateKey",
            "Economy:Node:LedgerAdminPrivateKey");
        string? algorithmText = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_ALGORITHM"),
            "Economy:LedgerAdmin:Algorithm",
            "Economy:Node:LedgerAdminAlgorithm");

        bool hasLedgerAdminConfiguration =
            !string.IsNullOrWhiteSpace(keyId)
            || !string.IsNullOrWhiteSpace(publicKey)
            || !string.IsNullOrWhiteSpace(privateKey)
            || !string.IsNullOrWhiteSpace(algorithmText);
        if (!hasLedgerAdminConfiguration)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException("Ledger admin public and private key material must both be configured when a production ledger admin signer is enabled.");
        }

        SignatureAlgorithmId algorithm = SignatureAlgorithmId.EcdsaP256Sha256;
        if (!string.IsNullOrWhiteSpace(algorithmText)
            && !Enum.TryParse(algorithmText, ignoreCase: true, out algorithm))
        {
            throw new InvalidOperationException($"Unknown ledger admin signature algorithm: {algorithmText}.");
        }

        return EconomyNodeAdminAccount.FromText(
            algorithm,
            string.IsNullOrWhiteSpace(keyId) ? EconomyNodeHostDefaults.LedgerAdminKeyId : keyId,
            publicKey,
            privateKey);
    }

    private static string GetValue(IConfiguration configuration, string? fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return fallback ?? string.Empty;
    }

    private static string? GetOptionalValue(IConfiguration configuration, string? fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static List<string> GetStringList(IConfiguration configuration, params string[] keys)
    {
        foreach (string key in keys)
        {
            IConfigurationSection section = configuration.GetSection(key);
            string[] children = section
                .GetChildren()
                .Select(child => child.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToArray();
            if (children.Length > 0)
            {
                return children.ToList();
            }

            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            return value
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .ToList();
        }

        return [];
    }

    private static decimal GetDecimal(IConfiguration configuration, decimal fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Invalid decimal configuration value for {key}: {value}.");
        }

        return fallback;
    }

    private static bool GetBool(IConfiguration configuration, bool fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Invalid boolean configuration value for {key}: {value}.");
        }

        return fallback;
    }

    private static int GetInt(IConfiguration configuration, int fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Invalid integer configuration value for {key}: {value}.");
        }

        return fallback;
    }

    private static int? GetNullableInt(IConfiguration configuration, int? fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.Equals(value.Trim(), "null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Invalid integer configuration value for {key}: {value}.");
        }

        return fallback;
    }

    private static long GetLong(IConfiguration configuration, long fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Invalid integer configuration value for {key}: {value}.");
        }

        return fallback;
    }

    private static double GetDouble(IConfiguration configuration, double fallback, params string[] keys)
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Invalid floating-point configuration value for {key}: {value}.");
        }

        return fallback;
    }

    private static TEnum GetEnum<TEnum>(IConfiguration configuration, TEnum fallback, params string[] keys)
        where TEnum : struct, Enum
    {
        foreach (string key in keys)
        {
            string? value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (Enum.TryParse(value, ignoreCase: true, out TEnum parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Invalid enum configuration value for {key}: {value}.");
        }

        return fallback;
    }
}
