using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record LedgerPublicKeyDiscoveryResult(
    Address Address,
    byte[] PublicKeyMaterial,
    HashHex PublicKeyHash,
    TxId TxId,
    int InputIndex,
    DateTimeOffset TransactionTimestamp);

public sealed class LedgerTransactionPublicKeyDiscovery(ILedgerReadClient ledgerReadClient)
{
    public async Task<LedgerPublicKeyDiscoveryResult?> FindPublicKeyAsync(
        LedgerAccountContext context,
        Address address,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
            .QueryTransactionsAsync(
                new LedgerTransactionQuery(
                    context,
                    Address: address,
                    IncludeData: false,
                    Skip: 0,
                    Take: 100),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (LedgerTransactionSummary transaction in transactions.OrderByDescending(item => item.Timestamp))
        {
            foreach (LedgerTransactionInputSummary input in transaction.Inputs.OrderBy(item => item.InputIndex))
            {
                if (input.Address != address)
                {
                    continue;
                }

                foreach (SignatureEnvelope signature in input.SignatureBundle.Signatures)
                {
                    byte[]? publicKey = signature.PublicKeyMaterial;
                    if (publicKey is not { Length: > 0 })
                    {
                        continue;
                    }

                    if (!PublicKeyMatchesAddress(publicKey, address, signature.PublicKeyHash))
                    {
                        continue;
                    }

                    return new LedgerPublicKeyDiscoveryResult(
                        address,
                        publicKey.ToArray(),
                        signature.PublicKeyHash,
                        transaction.TxId,
                        input.InputIndex,
                        transaction.Timestamp);
                }
            }
        }

        return null;
    }

    private static bool PublicKeyMatchesAddress(
        byte[] publicKeyMaterial,
        Address address,
        HashHex expectedPublicKeyHash)
    {
        try
        {
            return address.MatchesPublicKey(publicKeyMaterial)
                && Address.CalculatePublicKeyHash(publicKeyMaterial) == expectedPublicKeyHash;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
