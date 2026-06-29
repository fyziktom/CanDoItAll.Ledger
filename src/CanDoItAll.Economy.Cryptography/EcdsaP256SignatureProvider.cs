using System.Security.Cryptography;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public sealed class EcdsaP256SignatureProvider : ISignatureProvider
{
    private const int WebCryptoP256SignatureLength = 64;

    public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

    public bool IsSupported => true;

    public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
    {
        using ECDsa key = EcdsaP256KeyMaterial.CreateSigningKey(privateKeyMaterial);
        return key.SignData(payload.ToArray(), HashAlgorithmName.SHA256);
    }

    public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
    {
        try
        {
            using ECDsa key = EcdsaP256KeyMaterial.CreateVerificationKey(publicKeyMaterial);
            if (signature.Length == WebCryptoP256SignatureLength &&
                key.VerifyData(
                    payload.ToArray(),
                    signature.ToArray(),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                return true;
            }

            return key.VerifyData(payload.ToArray(), signature.ToArray(), HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
