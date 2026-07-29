using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Cryptography;

public sealed record AccountVaultCreateRequest(
    string VaultId,
    string Password,
    string? DisplayName = null,
    string? KeyId = null,
    int KdfIterations = AccountVaultOptions.DefaultKdfIterations);

public sealed record AccountVaultImportRequest(
    string VaultId,
    string Password,
    Address Address,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    HashHex PublicKeyHash,
    byte[] PublicKeyMaterial,
    byte[] PrivateKeyMaterial,
    string? DisplayName = null,
    int KdfIterations = AccountVaultOptions.DefaultKdfIterations);

public sealed record AccountVaultCreationResult(
    AccountVaultRecord Record,
    AccountVaultUnlockedKey UnlockedKey);

public sealed record AccountVaultUnlockedKey(
    string VaultId,
    string? DisplayName,
    string KeyId,
    Address Address,
    SignatureAlgorithmId Algorithm,
    HashHex PublicKeyHash,
    byte[] PublicKeyMaterial,
    byte[] PrivateKeyMaterial);

public sealed record AccountVaultRecord(
    string VaultId,
    string? DisplayName,
    string KeyId,
    Address Address,
    SignatureAlgorithmId Algorithm,
    string PublicKeyHash,
    string PublicKeyMaterialBase64,
    string EncryptedPrivateKeyBase64,
    string KdfSaltBase64,
    string NonceBase64,
    int KdfIterations,
    string KdfAlgorithm,
    string EncryptionAlgorithm,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AccountVaultOptions
{
    public const int DefaultKdfIterations = 150_000;
    public const int SaltByteLength = 16;
    public const int NonceByteLength = 12;
    public const int DerivedKeyByteLength = 32;

    public int KdfIterations { get; init; } = DefaultKdfIterations;
}

public interface IAccountVaultStore
{
    ValueTask SaveAsync(AccountVaultRecord record, CancellationToken cancellationToken = default);

    ValueTask<AccountVaultRecord?> GetAsync(string vaultId, CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(string vaultId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAccountVaultStore : IAccountVaultStore
{
    private readonly ConcurrentDictionary<string, AccountVaultRecord> records = new(StringComparer.Ordinal);

    public ValueTask SaveAsync(AccountVaultRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(record);
        records[record.VaultId] = record;
        return ValueTask.CompletedTask;
    }

    public ValueTask<AccountVaultRecord?> GetAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureVaultId(vaultId);
        records.TryGetValue(vaultId, out AccountVaultRecord? record);
        return ValueTask.FromResult(record);
    }

    public ValueTask DeleteAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureVaultId(vaultId);
        records.TryRemove(vaultId, out _);
        return ValueTask.CompletedTask;
    }

    private static void EnsureVaultId(string vaultId)
    {
        if (string.IsNullOrWhiteSpace(vaultId))
        {
            throw new ArgumentException("Vault id is required.", nameof(vaultId));
        }
    }
}

public sealed class AccountVaultService
{
    private const string KdfAlgorithm = "PBKDF2-SHA256";
    private const string EncryptionAlgorithm = "AES-256-GCM";
    private static readonly byte[] PrivateKeyCheckPayload = Encoding.UTF8.GetBytes("CanDoItAll.Ledger.AccountVault.PrivateKeyCheck.v1");

    private readonly ILedgerCryptoDriver cryptoDriver;
    private readonly IAccountVaultStore store;
    private readonly AccountVaultOptions options;
    private readonly TimeProvider timeProvider;

    public AccountVaultService(
        ILedgerCryptoDriver cryptoDriver,
        IAccountVaultStore store,
        AccountVaultOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        this.cryptoDriver = cryptoDriver ?? throw new ArgumentNullException(nameof(cryptoDriver));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.options = options ?? new AccountVaultOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<AccountVaultCreationResult> CreateAsync(
        AccountVaultCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureVaultId(request.VaultId);
        EnsurePassword(request.Password);
        int iterations = request.KdfIterations > 0 ? request.KdfIterations : options.KdfIterations;
        EnsureIterations(iterations);

        EcdsaP256KeyPair keyPair = await cryptoDriver.CreateEcdsaP256KeyPairAsync(cancellationToken).ConfigureAwait(false);
        Address address = await LedgerAddressCodec.FromPublicKeyAsync(keyPair.PublicKeyMaterial, cryptoDriver, cancellationToken).ConfigureAwait(false);
        HashHex publicKeyHash = await LedgerAddressCodec.CalculatePublicKeyHashAsync(keyPair.PublicKeyMaterial, cryptoDriver, cancellationToken).ConfigureAwait(false);
        return await SaveKeyAsync(
            request.VaultId,
            request.Password,
            request.DisplayName,
            request.KeyId ?? request.VaultId,
            address,
            SignatureAlgorithmId.EcdsaP256Sha256,
            publicKeyHash,
            keyPair.PublicKeyMaterial,
            keyPair.PrivateKeyMaterial,
            iterations,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<AccountVaultCreationResult> ImportAsync(
        AccountVaultImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureVaultId(request.VaultId);
        EnsurePassword(request.Password);
        EnsureKeyId(request.KeyId);
        int iterations = request.KdfIterations > 0 ? request.KdfIterations : options.KdfIterations;
        EnsureIterations(iterations);

        if (request.Algorithm != SignatureAlgorithmId.EcdsaP256Sha256)
        {
            throw new NotSupportedException($"Account vault algorithm '{request.Algorithm}' is not supported.");
        }

        Address derivedAddress = await LedgerAddressCodec.FromPublicKeyAsync(request.PublicKeyMaterial, cryptoDriver, cancellationToken).ConfigureAwait(false);
        if (derivedAddress != request.Address)
        {
            throw new ArgumentException(
                $"Account address {request.Address.Value} does not match public key material. Expected {derivedAddress.Value}.",
                nameof(request));
        }

        HashHex derivedPublicKeyHash = await LedgerAddressCodec.CalculatePublicKeyHashAsync(request.PublicKeyMaterial, cryptoDriver, cancellationToken).ConfigureAwait(false);
        if (derivedPublicKeyHash != request.PublicKeyHash)
        {
            throw new ArgumentException(
                $"Public key hash {request.PublicKeyHash.Value} does not match public key material. Expected {derivedPublicKeyHash.Value}.",
                nameof(request));
        }

        await VerifyKeyPairAsync(request.PublicKeyMaterial, request.PrivateKeyMaterial, cancellationToken).ConfigureAwait(false);

        return await SaveKeyAsync(
            request.VaultId,
            request.Password,
            request.DisplayName,
            request.KeyId,
            request.Address,
            request.Algorithm,
            request.PublicKeyHash,
            request.PublicKeyMaterial,
            request.PrivateKeyMaterial,
            iterations,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AccountVaultCreationResult> SaveKeyAsync(
        string vaultId,
        string password,
        string? displayName,
        string keyId,
        Address address,
        SignatureAlgorithmId algorithm,
        HashHex publicKeyHash,
        byte[] publicKeyMaterial,
        byte[] privateKeyMaterial,
        int iterations,
        CancellationToken cancellationToken)
    {
        EnsureKeyId(keyId);
        string publicKeyBase64 = Convert.ToBase64String(publicKeyMaterial);

        byte[] salt = await cryptoDriver.GetRandomBytesAsync(AccountVaultOptions.SaltByteLength, cancellationToken).ConfigureAwait(false);
        byte[] nonce = await cryptoDriver.GetRandomBytesAsync(AccountVaultOptions.NonceByteLength, cancellationToken).ConfigureAwait(false);
        byte[] derivedKey = await DeriveVaultKeyAsync(password, salt, iterations, cancellationToken).ConfigureAwait(false);
        byte[] associatedData = BuildAssociatedData(
            vaultId,
            keyId,
            address,
            algorithm,
            publicKeyHash.Value,
            publicKeyBase64,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce),
            iterations);

        byte[] encryptedPrivateKey = await cryptoDriver.AesGcmEncryptAsync(
            derivedKey,
            nonce,
            privateKeyMaterial,
            associatedData,
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = timeProvider.GetUtcNow();
        var record = new AccountVaultRecord(
            vaultId,
            displayName,
            keyId,
            address,
            algorithm,
            publicKeyHash.Value,
            publicKeyBase64,
            Convert.ToBase64String(encryptedPrivateKey),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce),
            iterations,
            KdfAlgorithm,
            EncryptionAlgorithm,
            now,
            now);

        await store.SaveAsync(record, cancellationToken).ConfigureAwait(false);

        var unlockedKey = new AccountVaultUnlockedKey(
            record.VaultId,
            record.DisplayName,
            record.KeyId,
            record.Address,
            record.Algorithm,
            publicKeyHash,
            publicKeyMaterial,
            privateKeyMaterial);

        return new AccountVaultCreationResult(record, unlockedKey);
    }

    public async ValueTask<AccountVaultUnlockedKey> UnlockAsync(string vaultId, string password, CancellationToken cancellationToken = default)
    {
        EnsureVaultId(vaultId);
        EnsurePassword(password);

        AccountVaultRecord record = await store.GetAsync(vaultId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Account vault '{vaultId}' was not found.");

        EnsureRecord(record);
        byte[] salt = Convert.FromBase64String(record.KdfSaltBase64);
        byte[] nonce = Convert.FromBase64String(record.NonceBase64);
        byte[] publicKeyMaterial = Convert.FromBase64String(record.PublicKeyMaterialBase64);
        byte[] encryptedPrivateKey = Convert.FromBase64String(record.EncryptedPrivateKeyBase64);
        byte[] derivedKey = await DeriveVaultKeyAsync(password, salt, record.KdfIterations, cancellationToken).ConfigureAwait(false);
        byte[] associatedData = BuildAssociatedData(record);

        byte[] privateKeyMaterial;
        try
        {
            privateKeyMaterial = await cryptoDriver.AesGcmDecryptAsync(
                derivedKey,
                nonce,
                encryptedPrivateKey,
                associatedData,
                cancellationToken).ConfigureAwait(false);
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException("Unable to decrypt account vault private key.", exception);
        }

        await VerifyUnlockedKeyAsync(record, publicKeyMaterial, privateKeyMaterial, cancellationToken).ConfigureAwait(false);

        return new AccountVaultUnlockedKey(
            record.VaultId,
            record.DisplayName,
            record.KeyId,
            record.Address,
            record.Algorithm,
            new HashHex(record.PublicKeyHash),
            publicKeyMaterial,
            privateKeyMaterial);
    }

    public ValueTask DeleteAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        EnsureVaultId(vaultId);
        return store.DeleteAsync(vaultId, cancellationToken);
    }

    private async ValueTask<byte[]> DeriveVaultKeyAsync(
        string password,
        byte[] salt,
        int iterations,
        CancellationToken cancellationToken)
    {
        EnsureIterations(iterations);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        return await cryptoDriver.Pbkdf2Sha256Async(
            passwordBytes,
            salt,
            iterations,
            AccountVaultOptions.DerivedKeyByteLength,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask VerifyUnlockedKeyAsync(
        AccountVaultRecord record,
        byte[] publicKeyMaterial,
        byte[] privateKeyMaterial,
        CancellationToken cancellationToken)
    {
        if (record.Algorithm != SignatureAlgorithmId.EcdsaP256Sha256)
        {
            throw new NotSupportedException($"Account vault algorithm '{record.Algorithm}' is not supported.");
        }

        HashHex publicKeyHash = await LedgerAddressCodec.CalculatePublicKeyHashAsync(publicKeyMaterial, cryptoDriver, cancellationToken).ConfigureAwait(false);
        if (!publicKeyHash.Value.Equals(record.PublicKeyHash, StringComparison.Ordinal))
        {
            throw new CryptographicException("Vault public key does not match the vault public key hash.");
        }

        Address derivedAddress = await LedgerAddressCodec.FromPublicKeyAsync(publicKeyMaterial, cryptoDriver, cancellationToken).ConfigureAwait(false);
        if (derivedAddress != record.Address)
        {
            throw new CryptographicException("Vault public key does not match the vault address.");
        }

        byte[] signature = await cryptoDriver.SignEcdsaP256Sha256Async(
            PrivateKeyCheckPayload,
            privateKeyMaterial,
            cancellationToken).ConfigureAwait(false);
        bool verified = await cryptoDriver.VerifyEcdsaP256Sha256Async(
            PrivateKeyCheckPayload,
            signature,
            publicKeyMaterial,
            cancellationToken).ConfigureAwait(false);
        if (!verified)
        {
            throw new CryptographicException("Decrypted private key cannot sign for the vault public key.");
        }
    }

    private async ValueTask VerifyKeyPairAsync(
        byte[] publicKeyMaterial,
        byte[] privateKeyMaterial,
        CancellationToken cancellationToken)
    {
        byte[] signature = await cryptoDriver.SignEcdsaP256Sha256Async(
            PrivateKeyCheckPayload,
            privateKeyMaterial,
            cancellationToken).ConfigureAwait(false);
        bool verified = await cryptoDriver.VerifyEcdsaP256Sha256Async(
            PrivateKeyCheckPayload,
            signature,
            publicKeyMaterial,
            cancellationToken).ConfigureAwait(false);
        if (!verified)
        {
            throw new CryptographicException("Private key material cannot sign for the public key material.");
        }
    }

    private static void EnsureRecord(AccountVaultRecord record)
    {
        if (!string.Equals(record.KdfAlgorithm, KdfAlgorithm, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Account vault KDF '{record.KdfAlgorithm}' is not supported.");
        }

        if (!string.Equals(record.EncryptionAlgorithm, EncryptionAlgorithm, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Account vault encryption '{record.EncryptionAlgorithm}' is not supported.");
        }

        EnsureIterations(record.KdfIterations);
    }

    private static byte[] BuildAssociatedData(AccountVaultRecord record)
        => BuildAssociatedData(
            record.VaultId,
            record.KeyId,
            record.Address,
            record.Algorithm,
            record.PublicKeyHash,
            record.PublicKeyMaterialBase64,
            record.KdfSaltBase64,
            record.NonceBase64,
            record.KdfIterations);

    private static byte[] BuildAssociatedData(
        string vaultId,
        string keyId,
        Address address,
        SignatureAlgorithmId algorithm,
        string publicKeyHash,
        string publicKeyMaterialBase64,
        string kdfSaltBase64,
        string nonceBase64,
        int kdfIterations)
    {
        string metadata = string.Join(
            "\n",
            "CanDoItAll.Ledger.AccountVault.v1",
            vaultId,
            keyId,
            address.Value,
            ((int)algorithm).ToString(CultureInfo.InvariantCulture),
            publicKeyHash,
            publicKeyMaterialBase64,
            kdfSaltBase64,
            nonceBase64,
            kdfIterations.ToString(CultureInfo.InvariantCulture),
            KdfAlgorithm,
            EncryptionAlgorithm);
        return Encoding.UTF8.GetBytes(metadata);
    }

    private static void EnsureVaultId(string vaultId)
    {
        if (string.IsNullOrWhiteSpace(vaultId))
        {
            throw new ArgumentException("Vault id is required.", nameof(vaultId));
        }
    }

    private static void EnsurePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }
    }

    private static void EnsureKeyId(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("Key id is required.", nameof(keyId));
        }
    }

    private static void EnsureIterations(int iterations)
    {
        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "KDF iterations must be positive.");
        }
    }
}
