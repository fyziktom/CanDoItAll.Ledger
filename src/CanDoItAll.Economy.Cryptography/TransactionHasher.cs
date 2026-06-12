using System.Text;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public sealed class TransactionHasher(ITransactionCanonicalizer canonicalizer) : ITransactionHasher
{
    public TxId CalculateTxId(LedgerTransaction transaction)
    {
        var core = canonicalizer.GetTransactionCoreBytes(transaction);
        return new TxId(Hex.Sha256Hex(core).Value);
    }

    public HashHex CalculateTxHash(LedgerTransaction transaction)
    {
        var submitted = canonicalizer.GetSubmittedTransactionBytes(transaction);
        return Hex.Sha256Hex(submitted);
    }

    public OutPointKey CalculateOutPointKey(LedgerId ledgerId, ScenarioId? scenarioId, OutPoint outPoint, Address address)
    {
        var value = string.Join('|',
            "cdia.economy.outpoint.v1",
            ledgerId.Value,
            scenarioId?.Value ?? string.Empty,
            outPoint.TxId.Value,
            outPoint.OutputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            address.Value);

        return new OutPointKey(Hex.Sha256Hex(Encoding.UTF8.GetBytes(value)));
    }
}
