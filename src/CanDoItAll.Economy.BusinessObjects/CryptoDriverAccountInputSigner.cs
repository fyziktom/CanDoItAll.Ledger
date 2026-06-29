using System.Text;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed class CryptoDriverAccountInputSigner(IEconomyCryptoDriver cryptoDriver) : IAccountInputSigner
{
    public async Task<SignatureEnvelope> SignAsync(
        IAccountKeySource keySource,
        HashHex payloadHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keySource);
        if (keySource.Algorithm != SignatureAlgorithmId.EcdsaP256Sha256)
        {
            throw new NotSupportedException($"Browser account signing supports {SignatureAlgorithmId.EcdsaP256Sha256} only.");
        }

        ReadOnlyMemory<byte> privateKeyMaterial = await keySource
            .GetPrivateKeyMaterialAsync(cancellationToken)
            .ConfigureAwait(false);
        byte[] signature = await cryptoDriver
            .SignEcdsaP256Sha256Async(
                Encoding.UTF8.GetBytes(payloadHash.Value),
                privateKeyMaterial,
                cancellationToken)
            .ConfigureAwait(false);

        return new SignatureEnvelope(
            keySource.Algorithm,
            keySource.KeyId,
            keySource.PublicKeyHash,
            payloadHash,
            signature,
            keySource.PublicKeyMaterial.ToArray());
    }
}
