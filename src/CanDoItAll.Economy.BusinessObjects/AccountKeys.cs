using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public interface IAccountKeySource
{
    Address Address { get; }

    SignatureAlgorithmId Algorithm { get; }

    string KeyId { get; }

    HashHex PublicKeyHash { get; }

    ReadOnlyMemory<byte> PublicKeyMaterial { get; }

    ValueTask<ReadOnlyMemory<byte>> GetPrivateKeyMaterialAsync(CancellationToken cancellationToken);
}

public sealed class InMemoryAccountKeySource(
    Address address,
    SignatureAlgorithmId algorithm,
    string keyId,
    HashHex publicKeyHash,
    ReadOnlyMemory<byte> publicKeyMaterial,
    ReadOnlyMemory<byte> privateKeyMaterial) : IAccountKeySource
{
    public InMemoryAccountKeySource(
        SignatureAlgorithmId algorithm,
        string keyId,
        ReadOnlyMemory<byte> publicKeyMaterial,
        ReadOnlyMemory<byte> privateKeyMaterial)
        : this(
            Address.FromPublicKey(publicKeyMaterial.Span),
            algorithm,
            keyId,
            Address.CalculatePublicKeyHash(publicKeyMaterial.Span),
            publicKeyMaterial,
            privateKeyMaterial)
    {
    }

    public Address Address { get; } = EnsureAddressMatchesPublicKey(address, publicKeyMaterial);

    public SignatureAlgorithmId Algorithm { get; } = algorithm;

    public string KeyId { get; } = keyId;

    public HashHex PublicKeyHash { get; } = EnsurePublicKeyHashMatches(publicKeyHash, publicKeyMaterial);

    public ReadOnlyMemory<byte> PublicKeyMaterial { get; } = publicKeyMaterial;

    public ValueTask<ReadOnlyMemory<byte>> GetPrivateKeyMaterialAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(privateKeyMaterial);

    private static Address EnsureAddressMatchesPublicKey(Address address, ReadOnlyMemory<byte> publicKeyMaterial)
    {
        Address derivedAddress = Address.FromPublicKey(publicKeyMaterial.Span);
        if (address != derivedAddress)
        {
            throw new ArgumentException(
                $"Account address {address.Value} does not match public key material. Expected {derivedAddress.Value}.",
                nameof(address));
        }

        return address;
    }

    private static HashHex EnsurePublicKeyHashMatches(HashHex publicKeyHash, ReadOnlyMemory<byte> publicKeyMaterial)
    {
        HashHex derivedHash = Address.CalculatePublicKeyHash(publicKeyMaterial.Span);
        if (publicKeyHash != derivedHash)
        {
            throw new ArgumentException(
                $"Public key hash {publicKeyHash.Value} does not match public key material. Expected {derivedHash.Value}.",
                nameof(publicKeyHash));
        }

        return publicKeyHash;
    }
}

public interface IAccountInputSigner
{
    Task<SignatureEnvelope> SignAsync(IAccountKeySource keySource, HashHex payloadHash, CancellationToken cancellationToken);
}

public sealed class AccountInputSigner(ISignatureProviderRegistry signatureProviderRegistry) : IAccountInputSigner
{
    public async Task<SignatureEnvelope> SignAsync(IAccountKeySource keySource, HashHex payloadHash, CancellationToken cancellationToken)
    {
        ISignatureProvider provider = signatureProviderRegistry.Get(keySource.Algorithm);
        ReadOnlyMemory<byte> privateKeyMaterial = await keySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        byte[] signature = provider.Sign(System.Text.Encoding.UTF8.GetBytes(payloadHash.Value), privateKeyMaterial.Span);

        return new SignatureEnvelope(
            keySource.Algorithm,
            keySource.KeyId,
            keySource.PublicKeyHash,
            payloadHash,
            signature,
            keySource.PublicKeyMaterial.ToArray());
    }
}
