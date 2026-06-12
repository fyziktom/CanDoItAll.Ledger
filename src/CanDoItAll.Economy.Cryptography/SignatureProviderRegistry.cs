using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public sealed class SignatureProviderRegistry : ISignatureProviderRegistry
{
    private readonly IReadOnlyDictionary<SignatureAlgorithmId, ISignatureProvider> providers;

    public SignatureProviderRegistry(IEnumerable<ISignatureProvider> providers)
    {
        this.providers = providers.ToDictionary(x => x.Algorithm);
    }

    public ISignatureProvider Get(SignatureAlgorithmId algorithm)
    {
        if (!providers.TryGetValue(algorithm, out ISignatureProvider? provider))
        {
            throw new NotSupportedException($"Signature algorithm is not registered: {algorithm}");
        }

        if (!provider.IsSupported)
        {
            throw new PlatformNotSupportedException($"Signature algorithm is registered but not supported on this runtime: {algorithm}");
        }

        return provider;
    }

    public static SignatureProviderRegistry CreateDefault(IVefSecp256K1SignatureAdapter? vefAdapter = null)
    {
        vefAdapter ??= new MissingVefSecp256K1SignatureAdapter();

        return new SignatureProviderRegistry(new ISignatureProvider[]
        {
            new EcdsaP256SignatureProvider(),
            new VefSecp256K1SignatureProvider(vefAdapter),
            new MLDsaSignatureProvider(SignatureAlgorithmId.MLDsa65),
            new SLHDsaSignatureProvider(SignatureAlgorithmId.SLHDsa128s)
        });
    }
}
