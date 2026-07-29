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
public static class LedgerNodeHostDefaults
{
    public const string ApiVersion = "v1";
    public const string ApiKeyHeaderName = "X-CDA-Ledger-Key";
    public const string LedgerAdminKeyId = "node-ledger-admin";
    public const string TreasuryAdminKeyId = "node-treasury-admin";
    public const string DefaultAuthInMemoryDatabaseName = "candoitall-ledger-node-auth";
    public const string DevelopmentAdminEmail = "admin@admin.com";
    public const string DevelopmentAdminPassword = "Admin@1234";
    public const string JwtIssuer = "CanDoItAll.Ledger.Node";
    public const string JwtAudience = "CanDoItAll.Ledger.Node.Api";
    public const string DefaultPostgreSqlConnectionString =
        "Host=localhost;Port=5432;Username=cdaledger;Password=cdaledger;Database=candoitall_ledger";
    public const string TreasuryAdminPublicKeyMaterial = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEiKdPXbQOUX1Mq3/YFQvwO8eZSBkFlj2rlI97UbpjN2YS84gs1yFzx6xPui82CqP/il5kfENZ3DrkLe/5TJBFww==";
    public const string TreasuryAdminPrivateKeyMaterial = "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgbD817dzRHKrA7hAcd1JFa+CECY3t1mCJn3sE1rq9YZahRANCAASIp09dtA5RfUyrf9gVC/A7x5lIGQWWPauUj3tRumM3ZhLziCzXIXPHrE+6LzYKo/+KXmR8Q1ncOuQt7/lMkEXD";

    public static string TreasuryAdminAddress => CreateTreasuryAdminAccount().Address.Value;

    public static LedgerNodeAdminAccount CreateTreasuryAdminAccount()
        => LedgerNodeAdminAccount.FromText(
            SignatureAlgorithmId.EcdsaP256Sha256,
            TreasuryAdminKeyId,
            TreasuryAdminPublicKeyMaterial,
            TreasuryAdminPrivateKeyMaterial);
}
