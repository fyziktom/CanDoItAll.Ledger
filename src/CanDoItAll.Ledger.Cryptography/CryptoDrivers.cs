using System.Security.Cryptography;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Cryptography;

public interface ILedgerCryptoDriver
{
    ValueTask<byte[]> GetRandomBytesAsync(int length, CancellationToken cancellationToken = default);

    ValueTask<byte[]> Sha256Async(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    ValueTask<byte[]> Pbkdf2Sha256Async(
        ReadOnlyMemory<byte> password,
        ReadOnlyMemory<byte> salt,
        int iterations,
        int byteCount,
        CancellationToken cancellationToken = default);

    ValueTask<byte[]> AesGcmEncryptAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> plaintext,
        ReadOnlyMemory<byte> associatedData,
        CancellationToken cancellationToken = default);

    ValueTask<byte[]> AesGcmDecryptAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> ciphertextWithTag,
        ReadOnlyMemory<byte> associatedData,
        CancellationToken cancellationToken = default);

    ValueTask<byte[]> DeriveEcdhP256Sha256KeyAsync(
        ReadOnlyMemory<byte> privateKeyMaterial,
        ReadOnlyMemory<byte> otherPublicKeyMaterial,
        ReadOnlyMemory<byte> secretAppend,
        CancellationToken cancellationToken = default);

    ValueTask<EcdsaP256KeyPair> CreateEcdsaP256KeyPairAsync(CancellationToken cancellationToken = default);

    ValueTask<byte[]> SignEcdsaP256Sha256Async(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> privateKeyMaterial,
        CancellationToken cancellationToken = default);

    ValueTask<bool> VerifyEcdsaP256Sha256Async(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> signature,
        ReadOnlyMemory<byte> publicKeyMaterial,
        CancellationToken cancellationToken = default);
}

public sealed record EcdsaP256KeyPair(byte[] PublicKeyMaterial, byte[] PrivateKeyMaterial);

public sealed class DotNetLedgerCryptoDriver : ILedgerCryptoDriver
{
    private const int AesGcmTagSize = 16;
    private const int WebCryptoP256SignatureLength = 64;

    public ValueTask<byte[]> GetRandomBytesAsync(int length, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Random byte length must be positive.");
        }

        return ValueTask.FromResult(RandomNumberGenerator.GetBytes(length));
    }

    public ValueTask<byte[]> Sha256Async(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SHA256.HashData(data.Span));
    }

    public ValueTask<byte[]> Pbkdf2Sha256Async(
        ReadOnlyMemory<byte> password,
        ReadOnlyMemory<byte> salt,
        int iterations,
        int byteCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (password.IsEmpty)
        {
            throw new ArgumentException("Password material is required.", nameof(password));
        }

        if (salt.IsEmpty)
        {
            throw new ArgumentException("Salt is required.", nameof(salt));
        }

        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "PBKDF2 iterations must be positive.");
        }

        if (byteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Derived key byte count must be positive.");
        }

        byte[] derived = Rfc2898DeriveBytes.Pbkdf2(password.Span, salt.Span, iterations, HashAlgorithmName.SHA256, byteCount);
        return ValueTask.FromResult(derived);
    }

    public ValueTask<byte[]> AesGcmEncryptAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> plaintext,
        ReadOnlyMemory<byte> associatedData,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAesGcmInputs(key, nonce);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AesGcmTagSize];
        using var aes = new AesGcm(key.ToArray(), AesGcmTagSize);
        aes.Encrypt(nonce.Span, plaintext.Span, ciphertext, tag, associatedData.Span);

        byte[] combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);
        return ValueTask.FromResult(combined);
    }

    public ValueTask<byte[]> AesGcmDecryptAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> ciphertextWithTag,
        ReadOnlyMemory<byte> associatedData,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAesGcmInputs(key, nonce);
        if (ciphertextWithTag.Length <= AesGcmTagSize)
        {
            throw new CryptographicException("Ciphertext must include an AES-GCM authentication tag.");
        }

        int ciphertextLength = ciphertextWithTag.Length - AesGcmTagSize;
        ReadOnlySpan<byte> ciphertext = ciphertextWithTag.Span[..ciphertextLength];
        ReadOnlySpan<byte> tag = ciphertextWithTag.Span[ciphertextLength..];
        byte[] plaintext = new byte[ciphertextLength];

        using var aes = new AesGcm(key.ToArray(), AesGcmTagSize);
        aes.Decrypt(nonce.Span, ciphertext, tag, plaintext, associatedData.Span);
        return ValueTask.FromResult(plaintext);
    }

    public ValueTask<byte[]> DeriveEcdhP256Sha256KeyAsync(
        ReadOnlyMemory<byte> privateKeyMaterial,
        ReadOnlyMemory<byte> otherPublicKeyMaterial,
        ReadOnlyMemory<byte> secretAppend,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (privateKeyMaterial.IsEmpty)
        {
            throw new ArgumentException("ECDH private key material is required.", nameof(privateKeyMaterial));
        }

        if (otherPublicKeyMaterial.IsEmpty)
        {
            throw new ArgumentException("ECDH public key material is required.", nameof(otherPublicKeyMaterial));
        }

        using ECDiffieHellman privateKey = EcdsaP256KeyMaterial.CreateAgreementPrivateKey(privateKeyMaterial.Span);
        using ECDiffieHellman publicKey = EcdsaP256KeyMaterial.CreateAgreementPublicKey(otherPublicKeyMaterial.Span);
        byte[] key = privateKey.DeriveKeyFromHash(
            publicKey.PublicKey,
            HashAlgorithmName.SHA256,
            secretPrepend: null,
            secretAppend: secretAppend.ToArray());
        return ValueTask.FromResult(key);
    }

    public ValueTask<EcdsaP256KeyPair> CreateEcdsaP256KeyPairAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
        return ValueTask.FromResult(new EcdsaP256KeyPair(publicKey, privateKey));
    }

    public ValueTask<byte[]> SignEcdsaP256Sha256Async(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> privateKeyMaterial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using ECDsa key = EcdsaP256KeyMaterial.CreateSigningKey(privateKeyMaterial.Span);
        byte[] signature = key.SignData(
            payload.ToArray(),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return ValueTask.FromResult(signature);
    }

    public ValueTask<bool> VerifyEcdsaP256Sha256Async(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> signature,
        ReadOnlyMemory<byte> publicKeyMaterial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using ECDsa key = EcdsaP256KeyMaterial.CreateVerificationKey(publicKeyMaterial.Span);
            bool verified = signature.Length == WebCryptoP256SignatureLength
                ? key.VerifyData(
                    payload.ToArray(),
                    signature.ToArray(),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
                : key.VerifyData(payload.ToArray(), signature.ToArray(), HashAlgorithmName.SHA256);
            return ValueTask.FromResult(verified);
        }
        catch (CryptographicException)
        {
            return ValueTask.FromResult(false);
        }
    }

    private static void EnsureAesGcmInputs(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> nonce)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("AES-GCM key must be 32 bytes.", nameof(key));
        }

        if (nonce.Length != 12)
        {
            throw new ArgumentException("AES-GCM nonce must be 12 bytes.", nameof(nonce));
        }
    }
}
