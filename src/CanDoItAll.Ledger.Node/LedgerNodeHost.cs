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

namespace CanDoItAll.Ledger.Node;


public static partial class LedgerNodeHost
{
    private const string ReadRateLimitPolicy = "ledger-node-read";
    private const string WriteRateLimitPolicy = "ledger-node-write";
    private const string DeepHealthRateLimitPolicy = "ledger-node-deep-health";

    public static WebApplicationBuilder CreateBuilder(
        string[]? args = null,
        Action<LedgerNodeHostOptions>? configure = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args ?? Array.Empty<string>(),
            ApplicationName = typeof(LedgerNodeHost).Assembly.GetName().Name
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.Services.AddLedgerNode(builder.Configuration, configure);
        return builder;
    }

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        builder.Environment.ApplicationName = typeof(LedgerNodeHost).Assembly.GetName().Name!;
        WebApplication app = builder.Build();
        ConfigurePipeline(app);
        return app;
    }

    public static async Task<WebApplication> StartAsync(
        string[]? args = null,
        Action<LedgerNodeHostOptions>? configure = null,
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
        LedgerDbContext db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<LedgerDbContext>>()
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (db)
        {
            await ApplySchemaManagementAsync(
                    db,
                    scope.ServiceProvider.GetRequiredService<LedgerNodeHostOptions>(),
                    scope.ServiceProvider.GetRequiredService<ILedgerSchemaMigrator>(),
                    cancellationToken)
                .ConfigureAwait(false);
            LedgerNodeHostOptions options = scope.ServiceProvider.GetRequiredService<LedgerNodeHostOptions>();
            if (options.BootstrapTreasuryAdmin)
            {
                LedgerExecutor executor = scope.ServiceProvider.GetRequiredService<LedgerExecutor>();
                ITimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<ITimeProvider>();
                await LedgerNodeBootstrapper.BootstrapTreasuryAdminAsync(db, executor, timeProvider, options, cancellationToken).ConfigureAwait(false);
            }
        }

        await InitializeAuthAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InitializeAuthAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        LedgerNodeHostOptions options = services.GetRequiredService<LedgerNodeHostOptions>();
        if (!options.UsesAuthPersistence)
        {
            return;
        }

        LedgerNodeAuthDbContext db = services.GetRequiredService<LedgerNodeAuthDbContext>();
        await ApplyAuthSchemaManagementAsync(db, options, cancellationToken).ConfigureAwait(false);

        if (!options.UsesCookieUiAuth)
        {
            return;
        }

        RoleManager<IdentityRole> roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<LedgerNodeUser> userManager = services.GetRequiredService<UserManager<LedgerNodeUser>>();

        if (!await roleManager.RoleExistsAsync(LedgerNodeAuthPolicies.AdministratorRole).ConfigureAwait(false))
        {
            IdentityResult createRole = await roleManager
                .CreateAsync(new IdentityRole(LedgerNodeAuthPolicies.AdministratorRole))
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
        UserManager<LedgerNodeUser> userManager,
        LedgerNodeHostOptions options)
    {
        options.ValidateAdminSeedOptions();
        string email = options.AdminEmail!.Trim();
        LedgerNodeUser? user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new LedgerNodeUser
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

        if (!await userManager.IsInRoleAsync(user, LedgerNodeAuthPolicies.AdministratorRole).ConfigureAwait(false))
        {
            IdentityResult addRole = await userManager.AddToRoleAsync(user, LedgerNodeAuthPolicies.AdministratorRole).ConfigureAwait(false);
            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException($"Unable to assign economy node administrator role: {FormatIdentityErrors(addRole)}");
            }
        }
    }

    private static string FormatIdentityErrors(IdentityResult result)
        => string.Join("; ", result.Errors.Select(error => error.Description));






    private static LedgerNodeHostOptions ResolveOptions(IConfiguration configuration)
    {
        string treasuryKeyId = GetValue(
            configuration,
            LedgerNodeHostDefaults.TreasuryAdminKeyId,
            "Ledger:Treasury:AdminKeyId",
            "Ledger:Node:TreasuryAdminKeyId");
        string treasuryPublicKey = GetValue(
            configuration,
            LedgerNodeHostDefaults.TreasuryAdminPublicKeyMaterial,
            "Ledger:Treasury:AdminPublicKey",
            "Ledger:Node:TreasuryAdminPublicKey");
        string treasuryPrivateKey = GetValue(
            configuration,
            LedgerNodeHostDefaults.TreasuryAdminPrivateKeyMaterial,
            "Ledger:Treasury:AdminPrivateKey",
            "Ledger:Node:TreasuryAdminPrivateKey");
        LedgerNodeAdminAccount treasuryAdminAccount = LedgerNodeAdminAccount.FromText(
            SignatureAlgorithmId.EcdsaP256Sha256,
            treasuryKeyId,
            treasuryPublicKey,
            treasuryPrivateKey);

        string? apiKey = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_API_KEY"),
            "Ledger:Api:Key",
            "Ledger:Node:ApiKey");

        var options = new LedgerNodeHostOptions
        {
            ApiVersion = GetValue(
                configuration,
                LedgerNodeHostDefaults.ApiVersion,
                "Ledger:Api:Version",
                "Ledger:Node:ApiVersion"),
            SecurityProfile = GetEnum(
                configuration,
                LedgerNodeSecurityProfile.Development,
                "Ledger:Security:Profile",
                "Ledger:Node:SecurityProfile"),
            MapLegacyApiRoutes = GetBool(
                configuration,
                true,
                "Ledger:Api:MapLegacyRoutes",
                "Ledger:Node:MapLegacyApiRoutes"),
            ApiSecurityMode = GetEnum(
                configuration,
                LedgerNodeApiSecurityMode.Development,
                "Ledger:Auth:Api:WriteMode",
                "Ledger:Auth:Api:Mode",
                "Ledger:Api:SecurityMode",
                "Ledger:Node:ApiSecurityMode"),
            ReadSecurityMode = GetEnum(
                configuration,
                LedgerNodeApiReadSecurityMode.Public,
                "Ledger:Auth:Api:ReadMode",
                "Ledger:Auth:Api:Mode",
                "Ledger:Api:ReadSecurityMode",
                "Ledger:Node:ReadSecurityMode"),
            ApiKeyHeaderName = GetValue(
                configuration,
                LedgerNodeHostDefaults.ApiKeyHeaderName,
                "Ledger:Api:KeyHeaderName",
                "Ledger:Node:ApiKeyHeaderName"),
            ApiKey = apiKey,
            VpsHostingMode = GetBool(
                configuration,
                false,
                "Ledger:Hosting:VpsMode",
                "Ledger:Auth:VpsMode"),
            UiAuthMode = GetEnum(
                configuration,
                LedgerNodeUiAuthMode.Disabled,
                "Ledger:Auth:Ui:Mode",
                "Ledger:Node:UiAuthMode"),
            RequireTwoFactorAuthentication = GetBool(
                configuration,
                false,
                "Ledger:Auth:Ui:RequireTwoFactor",
                "Ledger:Auth:Ui:RequireTwoFactorAuthentication",
                "Ledger:Node:RequireTwoFactorAuthentication"),
            SeedAdminUser = GetBool(
                configuration,
                false,
                "Ledger:Auth:Ui:SeedAdmin",
                "Ledger:Auth:Ui:SeedAdminUser",
                "Ledger:Node:SeedAdminUser"),
            AllowExistingAdminUser = GetBool(
                configuration,
                false,
                "Ledger:Auth:Ui:AllowExistingAdmin",
                "Ledger:Auth:Ui:AllowExistingAdminUser",
                "Ledger:Node:AllowExistingAdminUser"),
            UseDevelopmentAdminDefaults = GetBool(
                configuration,
                false,
                "Ledger:Auth:Ui:UseDevelopmentAdminDefaults",
                "Ledger:Node:UseDevelopmentAdminDefaults"),
            AdminEmail = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_ADMIN_EMAIL"),
                "Ledger:Auth:Ui:AdminEmail",
                "Ledger:Node:AdminEmail"),
            AdminPassword = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_ADMIN_PASSWORD"),
                "Ledger:Auth:Ui:AdminPassword",
                "Ledger:Node:AdminPassword"),
            AuthPersistenceProvider = GetEnum(
                configuration,
                LedgerNodeAuthPersistenceProvider.InMemory,
                "Ledger:Auth:Persistence:Provider",
                "Ledger:Node:AuthPersistenceProvider"),
            AuthSchemaManagementMode = GetEnum(
                configuration,
                LedgerNodeAuthSchemaManagementMode.EnsureCreated,
                "Ledger:Auth:Persistence:SchemaManagementMode",
                "Ledger:Node:AuthSchemaManagementMode"),
            AuthInMemoryDatabaseName = GetValue(
                configuration,
                LedgerNodeHostDefaults.DefaultAuthInMemoryDatabaseName,
                "Ledger:Auth:Persistence:InMemoryDatabaseName",
                "Ledger:Node:AuthInMemoryDatabaseName"),
            AuthSqliteConnectionString = GetOptionalValue(
                configuration,
                null,
                "Ledger:Auth:Sqlite:ConnectionString",
                "Ledger:Auth:Persistence:SqliteConnectionString"),
            AuthPostgreSqlConnectionString = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_AUTH_CONNECTION"),
                "Ledger:Auth:PostgreSql:ConnectionString",
                "Ledger:Auth:Persistence:PostgreSqlConnectionString"),
            JwtIssuer = GetValue(
                configuration,
                LedgerNodeHostDefaults.JwtIssuer,
                "Ledger:Auth:Jwt:Issuer",
                "Ledger:Node:JwtIssuer"),
            JwtAudience = GetValue(
                configuration,
                LedgerNodeHostDefaults.JwtAudience,
                "Ledger:Auth:Jwt:Audience",
                "Ledger:Node:JwtAudience"),
            JwtSigningKey = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_JWT_SIGNING_KEY"),
                "Ledger:Auth:Jwt:SigningKey",
                "Ledger:Node:JwtSigningKey"),
            JwtDefaultLifetimeMinutes = GetInt(
                configuration,
                60,
                "Ledger:Auth:Jwt:DefaultLifetimeMinutes",
                "Ledger:Node:JwtDefaultLifetimeMinutes"),
            JwtMaxLifetimeDays = GetInt(
                configuration,
                30,
                "Ledger:Auth:Jwt:MaxLifetimeDays",
                "Ledger:Node:JwtMaxLifetimeDays"),
            JwtRequireActiveTokenRecord = GetBool(
                configuration,
                true,
                "Ledger:Auth:Jwt:RequireActiveTokenRecord",
                "Ledger:Node:JwtRequireActiveTokenRecord"),
            DataProtectionKeyRingPath = GetOptionalValue(
                configuration,
                null,
                "Ledger:Auth:DataProtection:KeyRingPath",
                "DataProtection:KeyRingPath"),
            DataProtectionCertificatePath = GetOptionalValue(
                configuration,
                null,
                "Ledger:Auth:DataProtection:CertificatePath",
                "DataProtection:CertificatePath"),
            DataProtectionCertificatePassword = GetOptionalValue(
                configuration,
                null,
                "Ledger:Auth:DataProtection:CertificatePassword",
                "DataProtection:CertificatePassword"),
            UseForwardedHeaders = GetBool(
                configuration,
                false,
                "Ledger:Hosting:UseForwardedHeaders",
                "Ledger:Node:UseForwardedHeaders"),
            EnableHttpsRedirection = GetBool(
                configuration,
                false,
                "Ledger:Hosting:EnableHttpsRedirection",
                "Ledger:Node:EnableHttpsRedirection"),
            EnableHsts = GetBool(
                configuration,
                false,
                "Ledger:Hosting:EnableHsts",
                "Ledger:Node:EnableHsts"),
            CorsAllowedOrigins = GetStringList(
                configuration,
                "Ledger:Api:CorsAllowedOrigins",
                "Ledger:Node:CorsAllowedOrigins"),
            InMemoryDatabaseName = GetValue(
                configuration,
                LedgerEfCoreServiceCollectionExtensions.DefaultInMemoryDatabaseName,
                "Ledger:InMemory:DatabaseName",
                "Ledger:Persistence:InMemoryDatabaseName"),
            PostgreSqlConnectionString = GetValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_CONNECTION"),
                "Ledger:PostgreSql:ConnectionString"),
            PostgreSqlMigrationsAssembly = configuration["Ledger:PostgreSql:MigrationsAssembly"],
            PostgreSqlCommandTimeoutSeconds = GetNullableInt(
                configuration,
                30,
                "Ledger:PostgreSql:CommandTimeoutSeconds"),
            PostgreSqlEnableRetryOnFailure = GetBool(
                configuration,
                false,
                "Ledger:PostgreSql:EnableRetryOnFailure"),
            SchemaManagementMode = GetEnum(
                configuration,
                LedgerNodeSchemaManagementMode.EnsureCreated,
                "Ledger:Persistence:SchemaManagementMode",
                "Ledger:Node:SchemaManagementMode"),
            BootstrapTreasuryAdmin = GetBool(
                configuration,
                true,
                "Ledger:Treasury:Bootstrap",
                "Ledger:Node:BootstrapTreasuryAdmin"),
            AllowLedgerReset = GetBool(
                configuration,
                true,
                "Ledger:Api:AllowLedgerReset",
                "Ledger:Node:AllowLedgerReset"),
            EnforceTokenCreationPolicy = GetBool(
                configuration,
                true,
                "Ledger:TokenCreationPolicy:Enabled",
                "Ledger:Node:EnforceTokenCreationPolicy"),
            TreasuryAdminAccount = treasuryAdminAccount,
            TreasuryAdminAddress = GetValue(
                configuration,
                treasuryAdminAccount.Address.Value,
                "Ledger:Treasury:AdminAddress",
                "Ledger:Node:TreasuryAdminAddress"),
            TreasuryAdminMainAmount = GetDecimal(
                configuration,
                decimal.MaxValue,
                "Ledger:Treasury:AdminMainAmount",
                "Ledger:Node:TreasuryAdminMainAmount"),
            EnableRateLimiting = GetBool(
                configuration,
                true,
                "Ledger:Api:EnableRateLimiting",
                "Ledger:Node:EnableRateLimiting"),
            ReadRateLimitPerMinute = GetInt(
                configuration,
                120,
                "Ledger:Api:ReadRateLimitPerMinute",
                "Ledger:Node:ReadRateLimitPerMinute"),
            WriteRateLimitPerMinute = GetInt(
                configuration,
                30,
                "Ledger:Api:WriteRateLimitPerMinute",
                "Ledger:Node:WriteRateLimitPerMinute"),
            DeepHealthRateLimitPerMinute = GetInt(
                configuration,
                12,
                "Ledger:Api:DeepHealthRateLimitPerMinute",
                "Ledger:Node:DeepHealthRateLimitPerMinute"),
            MaxRequestBodyBytes = GetLong(
                configuration,
                1_048_576,
                "Ledger:Api:MaxRequestBodyBytes",
                "Ledger:Node:MaxRequestBodyBytes"),
            MaxTimeAdvanceDays = GetDouble(
                configuration,
                3650,
                "Ledger:Api:MaxTimeAdvanceDays",
                "Ledger:Node:MaxTimeAdvanceDays"),
            ExternalModelRoot = GetOptionalValue(
                configuration,
                Environment.GetEnvironmentVariable("CANDOITALL_COMPONENTS_3DMODELS_ROOT"),
                "Ledger:WebGl:ExternalModelRoot",
                "Ledger:Node:ExternalModelRoot")
        };

        options.LedgerAdminAccount = ResolveLedgerAdminAccount(configuration);

        string? providerText = GetValue(
            configuration,
            null,
            "Ledger:Persistence:Provider",
            "Ledger:Node:PersistenceProvider");

        if (!string.IsNullOrWhiteSpace(providerText)
            && Enum.TryParse(providerText, ignoreCase: true, out LedgerNodePersistenceProvider provider))
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
            options.PersistenceProvider = LedgerNodePersistenceProvider.PostgreSql;
        }

        return options;
    }

    private static LedgerNodeAdminAccount? ResolveLedgerAdminAccount(IConfiguration configuration)
    {
        string? keyId = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_LEDGER_ADMIN_KEY_ID"),
            "Ledger:LedgerAdmin:KeyId",
            "Ledger:Node:LedgerAdminKeyId");
        string? publicKey = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_LEDGER_ADMIN_PUBLIC_KEY"),
            "Ledger:LedgerAdmin:PublicKey",
            "Ledger:Node:LedgerAdminPublicKey");
        string? privateKey = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_LEDGER_ADMIN_PRIVATE_KEY"),
            "Ledger:LedgerAdmin:PrivateKey",
            "Ledger:Node:LedgerAdminPrivateKey");
        string? algorithmText = GetOptionalValue(
            configuration,
            Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_LEDGER_ADMIN_ALGORITHM"),
            "Ledger:LedgerAdmin:Algorithm",
            "Ledger:Node:LedgerAdminAlgorithm");

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

        return LedgerNodeAdminAccount.FromText(
            algorithm,
            string.IsNullOrWhiteSpace(keyId) ? LedgerNodeHostDefaults.LedgerAdminKeyId : keyId,
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
