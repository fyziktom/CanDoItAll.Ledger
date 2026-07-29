namespace CanDoItAll.Ledger.Core;

public sealed record SignatureEnvelope(
    SignatureAlgorithmId Algorithm,
    string KeyId,
    HashHex PublicKeyHash,
    HashHex PayloadHash,
    byte[] SignatureBytes,
    byte[]? PublicKeyMaterial = null);

public sealed record SignatureBundle(IReadOnlyList<SignatureEnvelope> Signatures)
{
    public static SignatureBundle Empty { get; } = new(Array.Empty<SignatureEnvelope>());
}

public sealed record SignaturePolicy(
    IReadOnlySet<SignatureAlgorithmId> AcceptedAlgorithms,
    IReadOnlySet<SignatureAlgorithmId> RequiredAlgorithms,
    bool RequireAllSignatures);

public interface ISignatureProvider
{
    SignatureAlgorithmId Algorithm { get; }

    bool IsSupported { get; }

    byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial);

    bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial);
}

public interface ISignatureProviderRegistry
{
    ISignatureProvider Get(SignatureAlgorithmId algorithm);
}
