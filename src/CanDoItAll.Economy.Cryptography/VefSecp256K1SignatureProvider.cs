using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public interface IVefSecp256K1SignatureAdapter
{
    byte[] SignSha256(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial);

    bool VerifySha256(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial);
}

public sealed class MissingVefSecp256K1SignatureAdapter : IVefSecp256K1SignatureAdapter
{
    public byte[] SignSha256(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
        => throw new InvalidOperationException("Register a real VEF secp256k1 signing adapter before signing transactions.");

    public bool VerifySha256(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
        => throw new InvalidOperationException("Register a real VEF secp256k1 signing adapter before verifying transactions.");
}

public sealed class VefSecp256K1SignatureProvider(IVefSecp256K1SignatureAdapter adapter) : ISignatureProvider
{
    public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.VefSecp256K1Sha256;

    public bool IsSupported => true;

    public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
        => adapter.SignSha256(payload, privateKeyMaterial);

    public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
        => adapter.VerifySha256(payload, signature, publicKeyMaterial);
}
