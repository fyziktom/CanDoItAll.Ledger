using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CanDoItAll.Economy.Node;

public enum EconomyNodeUiAuthMode
{
    Disabled = 0,
    Cookie = 1
}

public enum EconomyNodeAuthPersistenceProvider
{
    InMemory = 0,
    Sqlite = 1,
    PostgreSql = 2
}

public enum EconomyNodeAuthSchemaManagementMode
{
    EnsureCreated = 0,
    Migrate = 1,
    None = 2
}

public static class EconomyNodeAuthPolicies
{
    public const string AdministratorRole = "Administrator";
    public const string Administrator = "economy-node-admin";
    public const string ReadApi = "economy-node-api-read";
    public const string WriteApi = "economy-node-api-write";
    public const string AdminApi = "economy-node-api-admin";
}

public static class EconomyNodeScopes
{
    public const string Read = "economy.read";
    public const string Write = "economy.write";
    public const string Admin = "economy.admin";
    public const string LegacyNodeApi = "node.api";
    public const string DefaultIssuedScopes = Read + " " + Write;

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        Read,
        Write,
        Admin,
        LegacyNodeApi
    };

    public static string NormalizeForIssuance(string? value)
    {
        string source = string.IsNullOrWhiteSpace(value) ? DefaultIssuedScopes : value;
        string[] scopes = Split(source)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ScopeOrder)
            .ThenBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
        string[] unknown = scopes.Where(scope => !Allowed.Contains(scope)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException($"Unknown JWT scope(s): {string.Join(", ", unknown)}.");
        }

        return string.Join(' ', scopes);
    }

    public static bool PrincipalHasScope(ClaimsPrincipal principal, string requiredScope)
    {
        HashSet<string> granted = principal.Claims
            .Where(claim => claim.Type is "scope" or "scp")
            .SelectMany(claim => Split(claim.Value))
            .ToHashSet(StringComparer.Ordinal);
        if (granted.Contains(LegacyNodeApi) || granted.Contains(Admin))
        {
            return true;
        }

        return requiredScope switch
        {
            Read => granted.Contains(Read) || granted.Contains(Write),
            Write => granted.Contains(Write),
            Admin => false,
            _ => false
        };
    }

    private static IEnumerable<string> Split(string value)
        => value.Split(
            [' ', '\t', '\r', '\n', ',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int ScopeOrder(string scope)
        => scope switch
        {
            Read => 0,
            Write => 1,
            Admin => 2,
            LegacyNodeApi => 3,
            _ => 4
        };
}

public sealed record EconomyNodeScopeRequirement(string RequiredScope) : IAuthorizationRequirement;

public sealed class EconomyNodeScopeAuthorizationHandler : AuthorizationHandler<EconomyNodeScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EconomyNodeScopeRequirement requirement)
    {
        if (EconomyNodeScopes.PrincipalHasScope(context.User, requirement.RequiredScope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class EconomyNodeUser : IdentityUser
{
}

public sealed class EconomyJwtTokenRecord
{
    [Key]
    [MaxLength(64)]
    public string Jti { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(450)]
    public string SubjectUserId { get; set; } = string.Empty;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    [MaxLength(450)]
    public string? RevokedByUserId { get; set; }

    [MaxLength(512)]
    public string Scopes { get; set; } = EconomyNodeScopes.DefaultIssuedScopes;

    [MaxLength(128)]
    public string TokenFingerprint { get; set; } = string.Empty;

    public DateTimeOffset? LastUsedAt { get; set; }
}

public sealed class EconomyNodeAuthDbContext : IdentityDbContext<EconomyNodeUser, IdentityRole, string>
{
    public EconomyNodeAuthDbContext(DbContextOptions<EconomyNodeAuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<EconomyJwtTokenRecord> JwtTokens => Set<EconomyJwtTokenRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<EconomyJwtTokenRecord>(entity =>
        {
            entity.ToTable("EconomyJwtTokens");
            entity.HasKey(x => x.Jti);
            entity.Property(x => x.Jti).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.SubjectUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Scopes).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TokenFingerprint).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.SubjectUserId);
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasIndex(x => x.ExpiresAt);
            entity.HasIndex(x => x.RevokedAt);
        });
    }
}

public sealed record EconomyNodeJwtIssuedToken(
    string Token,
    string Jti,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string Scopes);

public sealed record EconomyNodeJwtTokenSummary(
    string Jti,
    string Name,
    string SubjectUserId,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    string Scopes,
    DateTimeOffset? LastUsedAt)
{
    public bool IsActive(DateTimeOffset now)
        => RevokedAt is null && ExpiresAt > now;
}

public sealed class EconomyNodeJwtService
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    private readonly EconomyNodeAuthDbContext db;
    private readonly EconomyNodeHostOptions options;

    public EconomyNodeJwtService(EconomyNodeAuthDbContext db, EconomyNodeHostOptions options)
    {
        this.db = db;
        this.options = options;
    }

    public async Task<EconomyNodeJwtIssuedToken> CreateTokenAsync(
        ClaimsPrincipal adminUser,
        string name,
        TimeSpan lifetime,
        string? scopes,
        CancellationToken cancellationToken)
    {
        string normalizedName = NormalizeTokenName(name);
        string normalizedScopes = NormalizeScopes(scopes);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("JWT lifetime must be positive.");
        }

        TimeSpan maxLifetime = TimeSpan.FromDays(options.JwtMaxLifetimeDays);
        if (lifetime > maxLifetime)
        {
            throw new InvalidOperationException(
                $"JWT lifetime cannot exceed {options.JwtMaxLifetimeDays.ToString(CultureInfo.InvariantCulture)} days.");
        }

        string subjectUserId = GetRequiredUserId(adminUser);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = now.Add(lifetime);
        string jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectUserId),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("scope", normalizedScopes),
            new(ClaimTypes.NameIdentifier, subjectUserId)
        };

        string? displayName = adminUser.Identity?.Name
            ?? adminUser.FindFirstValue(ClaimTypes.Email)
            ?? adminUser.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, displayName));
        }

        var credentials = new SigningCredentials(CreateSigningKey(options), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);
        string tokenText = TokenHandler.WriteToken(token);

        db.JwtTokens.Add(new EconomyJwtTokenRecord
        {
            Jti = jti,
            Name = normalizedName,
            SubjectUserId = subjectUserId,
            CreatedByUserId = subjectUserId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            Scopes = normalizedScopes,
            TokenFingerprint = Fingerprint(tokenText)
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EconomyNodeJwtIssuedToken(tokenText, jti, normalizedName, now, expiresAt, normalizedScopes);
    }

    public async Task<IReadOnlyList<EconomyNodeJwtTokenSummary>> ListTokensAsync(CancellationToken cancellationToken)
    {
        return await db.JwtTokens
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new EconomyNodeJwtTokenSummary(
                x.Jti,
                x.Name,
                x.SubjectUserId,
                x.CreatedByUserId,
                x.CreatedAt,
                x.ExpiresAt,
                x.RevokedAt,
                x.Scopes,
                x.LastUsedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RevokeTokenAsync(
        string jti,
        ClaimsPrincipal adminUser,
        CancellationToken cancellationToken)
    {
        string normalizedJti = string.IsNullOrWhiteSpace(jti)
            ? throw new InvalidOperationException("JWT id is required.")
            : jti.Trim();

        EconomyJwtTokenRecord token = await db.JwtTokens
            .SingleOrDefaultAsync(x => x.Jti == normalizedJti, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("JWT record was not found.");

        token.RevokedAt ??= DateTimeOffset.UtcNow;
        token.RevokedByUserId ??= GetRequiredUserId(adminUser);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ValidateActiveTokenAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!options.JwtRequireActiveTokenRecord)
        {
            return true;
        }

        string? jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti)
            ?? principal.FindFirstValue("jti");
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        EconomyJwtTokenRecord? record = await db.JwtTokens
            .SingleOrDefaultAsync(x => x.Jti == jti, cancellationToken)
            .ConfigureAwait(false);
        if (record is null || record.RevokedAt is not null || record.ExpiresAt <= now)
        {
            return false;
        }

        record.LastUsedAt = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public static SymmetricSecurityKey CreateSigningKey(EconomyNodeHostOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.JwtSigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        byte[] keyBytes = Encoding.UTF8.GetBytes(options.JwtSigningKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }

    public static TokenValidationParameters CreateValidationParameters(EconomyNodeHostOptions options)
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = options.JwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(options),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireSignedTokens = true,
            RequireExpirationTime = true
        };

    private static string NormalizeTokenName(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? "node-api-token"
            : value.Trim();
        if (normalized.Length > 160)
        {
            throw new InvalidOperationException("JWT token name is too long.");
        }

        return normalized;
    }

    private static string NormalizeScopes(string? value)
    {
        string normalized = EconomyNodeScopes.NormalizeForIssuance(value);
        if (normalized.Length > 512)
        {
            throw new InvalidOperationException("JWT scopes value is too long.");
        }

        return normalized;
    }

    private static string GetRequiredUserId(ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
           ?? throw new InvalidOperationException("Authenticated user id is missing.");

    private static string Fingerprint(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
