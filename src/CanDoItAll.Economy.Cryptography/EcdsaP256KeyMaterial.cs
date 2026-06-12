using System.Security.Cryptography;

namespace CanDoItAll.Economy.Cryptography;

public static class EcdsaP256KeyMaterial
{
    public static (byte[] PublicKeyMaterial, byte[] PrivateKeyMaterial) CreateKeyPair()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key.ExportSubjectPublicKeyInfo(), key.ExportPkcs8PrivateKey());
    }

    public static bool CanImport(byte[] publicKeyMaterial, byte[] privateKeyMaterial)
    {
        if (!CanImportPublicKey(publicKeyMaterial))
        {
            return false;
        }

        try
        {
            using ECDsa key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(privateKeyMaterial, out _);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static bool CanImportPublicKey(byte[] publicKeyMaterial)
    {
        try
        {
            using ECDsa key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(publicKeyMaterial, out _);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    internal static ECDsa CreateSigningKey(ReadOnlySpan<byte> privateKeyMaterial)
    {
        ECDsa key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(privateKeyMaterial, out _);
        return key;
    }

    internal static ECDsa CreateVerificationKey(ReadOnlySpan<byte> publicKeyMaterial)
    {
        ECDsa key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(publicKeyMaterial, out _);
        return key;
    }

    internal static ECDiffieHellman CreateAgreementPrivateKey(ReadOnlySpan<byte> privateKeyMaterial)
    {
        ECDiffieHellman key = ECDiffieHellman.Create();
        key.ImportPkcs8PrivateKey(privateKeyMaterial, out _);
        return key;
    }

    internal static ECDiffieHellman CreateAgreementPublicKey(ReadOnlySpan<byte> publicKeyMaterial)
    {
        ECDiffieHellman key = ECDiffieHellman.Create();
        key.ImportSubjectPublicKeyInfo(publicKeyMaterial, out _);
        return key;
    }
}
