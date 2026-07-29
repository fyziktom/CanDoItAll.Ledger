using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Node;

public sealed class ConfiguredLedgerAdminSigner(
    LedgerNodeAdminAccount account,
    ISignatureProviderRegistry signatureProviderRegistry) : ILedgerAdminSigner
{
    public SignatureBundle Sign(HashHex payloadHash)
    {
        ISignatureProvider provider = signatureProviderRegistry.Get(account.Algorithm);
        byte[] payload = Encoding.UTF8.GetBytes(payloadHash.Value);
        byte[] signature = provider.Sign(payload, account.PrivateKeyMaterial);

        return new SignatureBundle(new[]
        {
            new SignatureEnvelope(
                account.Algorithm,
                account.KeyId,
                account.PublicKeyHash,
                payloadHash,
                signature,
                account.PublicKeyMaterial)
        });
    }
}
