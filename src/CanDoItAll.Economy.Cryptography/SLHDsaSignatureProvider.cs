using System.Text;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public sealed class SLHDsaSignatureProvider(SignatureAlgorithmId algorithm) : ISignatureProvider
{
    public SignatureAlgorithmId Algorithm { get; } = algorithm;

    public bool IsSupported
    {
        get
        {
#if NET10_0_OR_GREATER
#pragma warning disable SYSLIB5006
            return System.Security.Cryptography.SlhDsa.IsSupported;
#pragma warning restore SYSLIB5006
#else
            return false;
#endif
        }
    }

    public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
    {
#if NET10_0_OR_GREATER
#pragma warning disable SYSLIB5006
        using var key = System.Security.Cryptography.SlhDsa.ImportFromPem(Encoding.UTF8.GetString(privateKeyMaterial));
        return key.SignData(payload.ToArray());
#pragma warning restore SYSLIB5006
#else
        throw new PlatformNotSupportedException("SLH-DSA requires .NET 10 or later and platform support.");
#endif
    }

    public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
    {
#if NET10_0_OR_GREATER
#pragma warning disable SYSLIB5006
        using var key = System.Security.Cryptography.SlhDsa.ImportFromPem(Encoding.UTF8.GetString(publicKeyMaterial));
        return key.VerifyData(payload.ToArray(), signature.ToArray());
#pragma warning restore SYSLIB5006
#else
        return false;
#endif
    }
}
