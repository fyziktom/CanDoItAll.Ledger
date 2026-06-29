using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record AccountBackupExportRequest(
    string DisplayName,
    string? Password = null,
    string? Notes = null);

public sealed record AccountBackupImportRequest(
    AccountBackupDocument Document,
    string? Password = null);

public sealed record AccountBackupDocument(
    string Format,
    string DisplayName,
    Address Address,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    HashHex PublicKeyHash,
    string PublicKeyMaterial,
    string PrivateKeyProtection,
    string PrivateKeyMaterial,
    string? Salt,
    string? Nonce,
    string? Tag,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

public interface IAccountBackupService
{
    Task<AccountBackupDocument> ExportAsync(
        IAccountKeySource keySource,
        AccountBackupExportRequest request,
        CancellationToken cancellationToken);

    IAccountKeySource Import(AccountBackupImportRequest request);

    string Serialize(AccountBackupDocument document);

    AccountBackupDocument Deserialize(string json);
}

public sealed class AccountBackupService(ITimeProvider timeProvider) : IAccountBackupService
{
    public const string BackupFormat = "candoitall-economy-account-backup-v1";
    public const string NoProtection = "none";
    public const string PasswordProtection = "aes-gcm-pbkdf2-sha256";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<AccountBackupDocument> ExportAsync(
        IAccountKeySource keySource,
        AccountBackupExportRequest request,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> privateKey = await keySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        ProtectedSecret protectedSecret = Protect(privateKey.Span, request.Password);

        return new AccountBackupDocument(
            BackupFormat,
            request.DisplayName,
            keySource.Address,
            keySource.Algorithm,
            keySource.KeyId,
            keySource.PublicKeyHash,
            Convert.ToBase64String(keySource.PublicKeyMaterial.Span),
            protectedSecret.Protection,
            Convert.ToBase64String(protectedSecret.CipherBytes),
            protectedSecret.Salt is null ? null : Convert.ToBase64String(protectedSecret.Salt),
            protectedSecret.Nonce is null ? null : Convert.ToBase64String(protectedSecret.Nonce),
            protectedSecret.Tag is null ? null : Convert.ToBase64String(protectedSecret.Tag),
            request.Notes,
            timeProvider.UtcNow);
    }

    public IAccountKeySource Import(AccountBackupImportRequest request)
    {
        if (!string.Equals(request.Document.Format, BackupFormat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported account backup format: {request.Document.Format}.");
        }

        byte[] publicKey = Convert.FromBase64String(request.Document.PublicKeyMaterial);
        byte[] privateKey = Unprotect(request.Document, request.Password);
        EnsurePrivateKeyMatchesPublicKey(request.Document.Algorithm, publicKey, privateKey);

        return new InMemoryAccountKeySource(
            request.Document.Address,
            request.Document.Algorithm,
            request.Document.KeyId,
            request.Document.PublicKeyHash,
            publicKey,
            privateKey);
    }

    public string Serialize(AccountBackupDocument document)
        => JsonSerializer.Serialize(document, SerializerOptions);

    public AccountBackupDocument Deserialize(string json)
        => JsonSerializer.Deserialize<AccountBackupDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Account backup JSON did not contain a document.");

    private static ProtectedSecret Protect(ReadOnlySpan<byte> privateKey, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new ProtectedSecret(NoProtection, privateKey.ToArray(), null, null, null);
        }

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] tag = new byte[16];
        byte[] cipher = new byte[privateKey.Length];
        byte[] key = DeriveKey(password, salt);

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, privateKey, cipher, tag);
        CryptographicOperations.ZeroMemory(key);
        return new ProtectedSecret(PasswordProtection, cipher, salt, nonce, tag);
    }

    private static byte[] Unprotect(AccountBackupDocument document, string? password)
    {
        if (string.Equals(document.PrivateKeyProtection, NoProtection, StringComparison.Ordinal))
        {
            return Convert.FromBase64String(document.PrivateKeyMaterial);
        }

        if (!string.Equals(document.PrivateKeyProtection, PasswordProtection, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported private key protection: {document.PrivateKeyProtection}.");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("Password is required to import this account backup.");
        }

        byte[] salt = Convert.FromBase64String(document.Salt ?? throw new InvalidOperationException("Encrypted backup is missing salt."));
        byte[] nonce = Convert.FromBase64String(document.Nonce ?? throw new InvalidOperationException("Encrypted backup is missing nonce."));
        byte[] tag = Convert.FromBase64String(document.Tag ?? throw new InvalidOperationException("Encrypted backup is missing tag."));
        byte[] cipher = Convert.FromBase64String(document.PrivateKeyMaterial);
        byte[] privateKey = new byte[cipher.Length];
        byte[] key = DeriveKey(password, salt);

        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, privateKey);
        CryptographicOperations.ZeroMemory(key);
        return privateKey;
    }

    private static byte[] DeriveKey(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            210_000,
            HashAlgorithmName.SHA256,
            32);

    private static void EnsurePrivateKeyMatchesPublicKey(
        SignatureAlgorithmId algorithm,
        byte[] publicKeyMaterial,
        byte[] privateKeyMaterial)
    {
        if (algorithm != SignatureAlgorithmId.EcdsaP256Sha256)
        {
            return;
        }

        byte[] exportedPublicKey = EcdsaP256KeyMaterial.ExportPublicKeyFromPrivateKey(privateKeyMaterial);
        if (!publicKeyMaterial.SequenceEqual(exportedPublicKey))
        {
            throw new CryptographicException("Backup private key does not match public key material.");
        }
    }

    private sealed record ProtectedSecret(
        string Protection,
        byte[] CipherBytes,
        byte[]? Salt,
        byte[]? Nonce,
        byte[]? Tag);
}
