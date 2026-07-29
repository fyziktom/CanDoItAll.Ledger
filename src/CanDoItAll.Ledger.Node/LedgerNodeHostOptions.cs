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
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Ledger.Node;

public enum LedgerNodePersistenceProvider
{
    InMemory = 0,
    PostgreSql = 1
}

public enum LedgerNodeSecurityProfile
{
    Development = 0,
    Production = 1
}

public enum LedgerNodeApiSecurityMode
{
    Development = 0,
    RequireApiKey = 1,
    RequireJwtBearer = 2,
    RequireApiKeyOrJwt = 3
}

public enum LedgerNodeApiReadSecurityMode
{
    Public = 0,
    RequireApiKey = 1,
    RequireJwtBearer = 2,
    RequireApiKeyOrJwt = 3
}

public enum LedgerNodeSchemaManagementMode
{
    EnsureCreated = 0,
    Migrate = 1,
    None = 2
}

public sealed record LedgerNodeAdminAccount(
    SignatureAlgorithmId Algorithm,
    string KeyId,
    byte[] PublicKeyMaterial,
    byte[] PrivateKeyMaterial)
{
    public Address Address => Address.FromPublicKey(PublicKeyMaterial);

    public HashHex PublicKeyHash => Address.CalculatePublicKeyHash(PublicKeyMaterial);

    public static LedgerNodeAdminAccount FromText(
        SignatureAlgorithmId algorithm,
        string keyId,
        string publicKeyMaterial,
        string privateKeyMaterial)
        => new(
            algorithm,
            keyId.Trim(),
            DecodeKeyMaterial(publicKeyMaterial),
            DecodeKeyMaterial(privateKeyMaterial));

    private static byte[] DecodeKeyMaterial(string value)
    {
        string trimmed = value.Trim();
        try
        {
            byte[] decoded = Convert.FromBase64String(trimmed);
            if (decoded.Length > 0)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
        }

        return Encoding.UTF8.GetBytes(trimmed);
    }
}

public sealed class LedgerNodeHostOptions
{
    private LedgerNodeAdminAccount treasuryAdminAccount = LedgerNodeHostDefaults.CreateTreasuryAdminAccount();
    private string? treasuryAdminAddress;

    public string ApiVersion { get; set; } = LedgerNodeHostDefaults.ApiVersion;

    public LedgerNodeSecurityProfile SecurityProfile { get; set; } = LedgerNodeSecurityProfile.Development;

    public bool MapLegacyApiRoutes { get; set; } = true;

    public LedgerNodeApiSecurityMode ApiSecurityMode { get; set; } = LedgerNodeApiSecurityMode.Development;

    public LedgerNodeApiReadSecurityMode ReadSecurityMode { get; set; } = LedgerNodeApiReadSecurityMode.Public;

    public string ApiKeyHeaderName { get; set; } = LedgerNodeHostDefaults.ApiKeyHeaderName;

    public string? ApiKey { get; set; }

    public bool VpsHostingMode { get; set; }

    public LedgerNodeUiAuthMode UiAuthMode { get; set; } = LedgerNodeUiAuthMode.Disabled;

    public bool RequireTwoFactorAuthentication { get; set; }

    public bool SeedAdminUser { get; set; }

    public bool AllowExistingAdminUser { get; set; }

    public bool UseDevelopmentAdminDefaults { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public LedgerNodeAuthPersistenceProvider AuthPersistenceProvider { get; set; } = LedgerNodeAuthPersistenceProvider.InMemory;

    public LedgerNodeAuthSchemaManagementMode AuthSchemaManagementMode { get; set; } = LedgerNodeAuthSchemaManagementMode.EnsureCreated;

    public string AuthInMemoryDatabaseName { get; set; } = LedgerNodeHostDefaults.DefaultAuthInMemoryDatabaseName;

    public string? AuthSqliteConnectionString { get; set; }

    public string? AuthPostgreSqlConnectionString { get; set; }

    public string JwtIssuer { get; set; } = LedgerNodeHostDefaults.JwtIssuer;

    public string JwtAudience { get; set; } = LedgerNodeHostDefaults.JwtAudience;

    public string? JwtSigningKey { get; set; }

    public int JwtDefaultLifetimeMinutes { get; set; } = 60;

    public int JwtMaxLifetimeDays { get; set; } = 30;

    public bool JwtRequireActiveTokenRecord { get; set; } = true;

    public string? DataProtectionKeyRingPath { get; set; }

    public string? DataProtectionCertificatePath { get; set; }

    public string? DataProtectionCertificatePassword { get; set; }

    public bool UseForwardedHeaders { get; set; }

    public bool EnableHttpsRedirection { get; set; }

    public bool EnableHsts { get; set; }

    public List<string> CorsAllowedOrigins { get; set; } = [];

    public LedgerNodePersistenceProvider PersistenceProvider { get; set; } = LedgerNodePersistenceProvider.InMemory;

    public string InMemoryDatabaseName { get; set; } = LedgerEfCoreServiceCollectionExtensions.DefaultInMemoryDatabaseName;

    public string? PostgreSqlConnectionString { get; set; }

    public string? PostgreSqlMigrationsAssembly { get; set; }

    public int? PostgreSqlCommandTimeoutSeconds { get; set; } = 30;

    public bool PostgreSqlEnableRetryOnFailure { get; set; }

    public LedgerNodeSchemaManagementMode SchemaManagementMode { get; set; } = LedgerNodeSchemaManagementMode.EnsureCreated;

    public bool BootstrapTreasuryAdmin { get; set; } = true;

    public bool AllowLedgerReset { get; set; } = true;

    public bool EnforceTokenCreationPolicy { get; set; } = true;

    public LedgerNodeAdminAccount TreasuryAdminAccount
    {
        get => treasuryAdminAccount;
        set
        {
            treasuryAdminAccount = value;
            treasuryAdminAddress = value.Address.Value;
        }
    }

    public string TreasuryAdminAddress
    {
        get => treasuryAdminAddress ?? treasuryAdminAccount.Address.Value;
        set => treasuryAdminAddress = value;
    }

    public LedgerNodeAdminAccount? LedgerAdminAccount { get; set; }

    public bool UsesDevelopmentLedgerAdminSigner => LedgerAdminAccount is null;

    public decimal TreasuryAdminMainAmount { get; set; } = decimal.MaxValue;

    public bool EnableRateLimiting { get; set; } = true;

    public int ReadRateLimitPerMinute { get; set; } = 120;

    public int WriteRateLimitPerMinute { get; set; } = 30;

    public int DeepHealthRateLimitPerMinute { get; set; } = 12;

    public long MaxRequestBodyBytes { get; set; } = 1_048_576;

    public double MaxTimeAdvanceDays { get; set; } = 3650;

    public string? ExternalModelRoot { get; set; }

    public string NormalizedApiVersion => NormalizeApiVersion(ApiVersion);

    public string VersionedApiBasePath => $"/api/{NormalizedApiVersion}";

    public string? LegacyApiBasePath => MapLegacyApiRoutes ? "/api" : null;

    public bool IsProductionSecurityProfile => SecurityProfile == LedgerNodeSecurityProfile.Production;

    public bool RequiresTrustedAdminVerification => IsProductionSecurityProfile;

    public bool UsesCookieUiAuth => UiAuthMode == LedgerNodeUiAuthMode.Cookie;

    public bool UsesJwtApiAuth =>
        ApiSecurityMode is LedgerNodeApiSecurityMode.RequireJwtBearer or LedgerNodeApiSecurityMode.RequireApiKeyOrJwt
        || ReadSecurityMode is LedgerNodeApiReadSecurityMode.RequireJwtBearer or LedgerNodeApiReadSecurityMode.RequireApiKeyOrJwt;

    public bool UsesAuthPersistence => UsesCookieUiAuth || UsesJwtApiAuth;

    public bool UsesAspNetAuthentication => UsesCookieUiAuth || UsesJwtApiAuth;

    public void ApplyDevelopmentAdminDefaults()
    {
        if (!UseDevelopmentAdminDefaults)
        {
            return;
        }

        if (UiAuthMode == LedgerNodeUiAuthMode.Disabled)
        {
            UiAuthMode = LedgerNodeUiAuthMode.Cookie;
        }

        SeedAdminUser = true;
        AdminEmail = string.IsNullOrWhiteSpace(AdminEmail)
            ? LedgerNodeHostDefaults.DevelopmentAdminEmail
            : AdminEmail;
        AdminPassword = string.IsNullOrWhiteSpace(AdminPassword)
            ? LedgerNodeHostDefaults.DevelopmentAdminPassword
            : AdminPassword;
    }

    public void ValidateAdminSeedOptions()
    {
        if (string.IsNullOrWhiteSpace(AdminEmail))
        {
            throw new InvalidOperationException("Ledger node admin email must be configured when admin seeding is enabled.");
        }

        if (string.IsNullOrWhiteSpace(AdminPassword))
        {
            throw new InvalidOperationException("Ledger node admin password must be configured when admin seeding is enabled.");
        }
    }

    public string ResolveAuthSqliteConnectionString()
        => string.IsNullOrWhiteSpace(AuthSqliteConnectionString)
            ? "Data Source=ledger-node-auth.db"
            : AuthSqliteConnectionString.Trim();

    public string ResolveAuthPostgreSqlConnectionString()
        => string.IsNullOrWhiteSpace(AuthPostgreSqlConnectionString)
            ? PostgreSqlConnectionString ?? string.Empty
            : AuthPostgreSqlConnectionString.Trim();

    public string ResolveDataProtectionKeyRingPath()
    {
        if (!string.IsNullOrWhiteSpace(DataProtectionKeyRingPath))
        {
            return DataProtectionKeyRingPath.Trim();
        }

        return OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "DataProtectionKeys")
            : "/var/lib/candoitall-ledger/dpkeys";
    }

    public void ValidateApiOptions()
    {
        _ = NormalizedApiVersion;

        if (string.IsNullOrWhiteSpace(ApiKeyHeaderName))
        {
            throw new InvalidOperationException("Ledger node API key header name cannot be empty.");
        }

        if ((ApiSecurityMode == LedgerNodeApiSecurityMode.RequireApiKey
                || ApiSecurityMode == LedgerNodeApiSecurityMode.RequireApiKeyOrJwt)
            && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Ledger node API key must be configured when API security mode requires an API key.");
        }

        if ((ReadSecurityMode == LedgerNodeApiReadSecurityMode.RequireApiKey
                || ReadSecurityMode == LedgerNodeApiReadSecurityMode.RequireApiKeyOrJwt)
            && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Ledger node API key must be configured when read security mode requires an API key.");
        }

        if (UsesJwtApiAuth)
        {
            if (string.IsNullOrWhiteSpace(JwtIssuer))
            {
                throw new InvalidOperationException("Ledger node JWT issuer must be configured when JWT API auth is enabled.");
            }

            if (string.IsNullOrWhiteSpace(JwtAudience))
            {
                throw new InvalidOperationException("Ledger node JWT audience must be configured when JWT API auth is enabled.");
            }

            _ = LedgerNodeJwtService.CreateSigningKey(this);
        }

        if (JwtDefaultLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("Ledger node default JWT lifetime must be positive.");
        }

        if (JwtMaxLifetimeDays <= 0)
        {
            throw new InvalidOperationException("Ledger node max JWT lifetime days must be positive.");
        }

        if (JwtDefaultLifetimeMinutes > TimeSpan.FromDays(JwtMaxLifetimeDays).TotalMinutes)
        {
            throw new InvalidOperationException("Ledger node default JWT lifetime cannot exceed max JWT lifetime.");
        }

        if (UsesCookieUiAuth)
        {
            if (SeedAdminUser)
            {
                ValidateAdminSeedOptions();
            }
            else if (!AllowExistingAdminUser && VpsHostingMode)
            {
                throw new InvalidOperationException("Ledger node VPS UI auth requires an existing admin account override or admin seeding configuration.");
            }
        }

        if (ReadRateLimitPerMinute <= 0)
        {
            throw new InvalidOperationException("Ledger node read rate limit must be positive.");
        }

        if (WriteRateLimitPerMinute <= 0)
        {
            throw new InvalidOperationException("Ledger node write rate limit must be positive.");
        }

        if (DeepHealthRateLimitPerMinute <= 0)
        {
            throw new InvalidOperationException("Ledger node deep health rate limit must be positive.");
        }

        if (MaxRequestBodyBytes <= 0)
        {
            throw new InvalidOperationException("Ledger node max request body size must be positive.");
        }

        if (MaxTimeAdvanceDays <= 0)
        {
            throw new InvalidOperationException("Ledger node max time advance days must be positive.");
        }

        if (PostgreSqlCommandTimeoutSeconds is <= 0)
        {
            throw new InvalidOperationException("Ledger node PostgreSQL command timeout must be positive when configured.");
        }

        if (UsesAuthPersistence && AuthPersistenceProvider == LedgerNodeAuthPersistenceProvider.Sqlite)
        {
            _ = ResolveAuthSqliteConnectionString();
        }

        if (UsesAuthPersistence && AuthPersistenceProvider == LedgerNodeAuthPersistenceProvider.PostgreSql)
        {
            string? connectionString = ResolveAuthPostgreSqlConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Ledger node auth PostgreSQL connection string must be configured.");
            }
        }
    }

    public void ValidateProductionSecurityOptions(
        LedgerNodeAdminAccount treasuryAdminAccount,
        LedgerNodeAdminAccount? ledgerAdminAccount)
    {
        if (!IsProductionSecurityProfile)
        {
            return;
        }

        var errors = new List<string>();
        if (ApiSecurityMode == LedgerNodeApiSecurityMode.Development)
        {
            errors.Add("write API security mode must require an API key or JWT");
        }

        if (ReadSecurityMode == LedgerNodeApiReadSecurityMode.Public)
        {
            errors.Add("read API security mode must require an API key or JWT");
        }

        if (UseDevelopmentAdminDefaults)
        {
            errors.Add("development admin defaults must be disabled");
        }

        if (string.Equals(AdminPassword, LedgerNodeHostDefaults.DevelopmentAdminPassword, StringComparison.Ordinal))
        {
            errors.Add("development admin password cannot be used");
        }

        if (VpsHostingMode)
        {
            if (UiAuthMode != LedgerNodeUiAuthMode.Cookie)
            {
                errors.Add("VPS hosting mode must require cookie UI authentication");
            }

            if (ApiSecurityMode != LedgerNodeApiSecurityMode.RequireJwtBearer
                && ApiSecurityMode != LedgerNodeApiSecurityMode.RequireApiKeyOrJwt)
            {
                errors.Add("VPS hosting mode must enable JWT write API authentication");
            }

            if (ReadSecurityMode != LedgerNodeApiReadSecurityMode.RequireJwtBearer
                && ReadSecurityMode != LedgerNodeApiReadSecurityMode.RequireApiKeyOrJwt)
            {
                errors.Add("VPS hosting mode must enable JWT read API authentication");
            }

            if (AuthPersistenceProvider == LedgerNodeAuthPersistenceProvider.InMemory)
            {
                errors.Add("VPS hosting mode cannot use in-memory auth persistence");
            }

            if (string.IsNullOrWhiteSpace(DataProtectionKeyRingPath))
            {
                errors.Add("VPS hosting mode must configure a data protection key ring path");
            }
        }

        if (PersistenceProvider == LedgerNodePersistenceProvider.InMemory)
        {
            errors.Add("in-memory persistence is not allowed");
        }

        if (PersistenceProvider == LedgerNodePersistenceProvider.PostgreSql
            && (string.IsNullOrWhiteSpace(PostgreSqlConnectionString)
                || string.Equals(PostgreSqlConnectionString.Trim(), LedgerNodeHostDefaults.DefaultPostgreSqlConnectionString, StringComparison.Ordinal)))
        {
            errors.Add("PostgreSQL connection string must be explicitly configured and cannot use the development default");
        }

        if (SchemaManagementMode == LedgerNodeSchemaManagementMode.EnsureCreated)
        {
            errors.Add("schema management cannot use EnsureCreated");
        }

        if (MapLegacyApiRoutes)
        {
            errors.Add("legacy /api routes must be disabled");
        }

        if (ledgerAdminAccount is null)
        {
            errors.Add("ledger admin account must be configured");
        }

        if (IsDefaultTreasuryAdminAccount(treasuryAdminAccount))
        {
            errors.Add("default treasury admin key material is not allowed");
        }

        if (TreasuryAdminMainAmount == decimal.MaxValue)
        {
            errors.Add("treasury bootstrap amount cannot be decimal.MaxValue");
        }

        if (AllowLedgerReset)
        {
            errors.Add("ledger reset endpoint must be disabled");
        }

        if (!EnforceTokenCreationPolicy)
        {
            errors.Add("token creation policy must be enforced");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Ledger node production security profile is not satisfied: "
                + string.Join("; ", errors)
                + ".");
        }
    }

    public LedgerNodeAdminAccount? ValidateAndGetLedgerAdminAccount()
    {
        if (LedgerAdminAccount is null)
        {
            return null;
        }

        ValidateAdminKeyMaterial(LedgerAdminAccount, "Ledger admin");
        return LedgerAdminAccount;
    }

    internal LedgerNodeAdminAccount ValidateAndGetTreasuryAdminAccount()
    {
        ValidateAdminKeyMaterial(TreasuryAdminAccount, "Treasury admin");

        string configuredAddress = TreasuryAdminAddress.Trim();
        if (!Address.TryParse(configuredAddress, out Address parsedAddress))
        {
            throw new InvalidOperationException("Treasury admin address must be a valid derived address.");
        }

        if (parsedAddress != TreasuryAdminAccount.Address)
        {
            throw new InvalidOperationException(
                $"Treasury admin address {parsedAddress.Value} does not match the configured public key. Expected {TreasuryAdminAccount.Address.Value}.");
        }

        treasuryAdminAddress = parsedAddress.Value;
        return TreasuryAdminAccount;
    }

    private static void ValidateAdminKeyMaterial(LedgerNodeAdminAccount account, string name)
    {
        if (string.IsNullOrWhiteSpace(account.KeyId))
        {
            throw new InvalidOperationException($"{name} key id cannot be empty.");
        }

        if (account.PublicKeyMaterial.Length == 0)
        {
            throw new InvalidOperationException($"{name} public key material cannot be empty.");
        }

        if (account.PrivateKeyMaterial.Length == 0)
        {
            throw new InvalidOperationException($"{name} private key material cannot be empty.");
        }
    }

    private static bool IsDefaultTreasuryAdminAccount(LedgerNodeAdminAccount account)
    {
        LedgerNodeAdminAccount defaults = LedgerNodeHostDefaults.CreateTreasuryAdminAccount();
        return string.Equals(account.KeyId, defaults.KeyId, StringComparison.Ordinal)
            && account.PublicKeyMaterial.SequenceEqual(defaults.PublicKeyMaterial)
            && account.PrivateKeyMaterial.SequenceEqual(defaults.PrivateKeyMaterial);
    }

    private static string NormalizeApiVersion(string value)
    {
        string normalized = value.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Ledger node API version cannot be empty.");
        }

        if (normalized.Contains('/', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Ledger node API version must be a single path segment.");
        }

        return normalized;
    }
}
