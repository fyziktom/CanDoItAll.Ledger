using System.Text;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;

namespace CanDoItAll.Ledger.Node;

public sealed class DevelopmentLedgerAdminSigner : ILedgerAdminSigner
{
    public SignatureBundle Sign(HashHex payloadHash)
    {
        byte[] publicKeyMaterial = "development-admin-public-key"u8.ToArray();
        HashHex publicKeyHash = Hex.Sha256Hex(publicKeyMaterial);
        byte[] signatureBytes = Encoding.UTF8.GetBytes($"development-signature:{payloadHash.Value}");

        return new SignatureBundle(new[]
        {
            new SignatureEnvelope(
                SignatureAlgorithmId.VefSecp256K1Sha256,
                "development-admin",
                publicKeyHash,
                payloadHash,
                signatureBytes,
                publicKeyMaterial)
        });
    }
}
