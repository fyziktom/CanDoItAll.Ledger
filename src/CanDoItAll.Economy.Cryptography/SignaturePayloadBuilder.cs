using System.Text;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public sealed class SignaturePayloadBuilder(ITransactionHasher hasher) : ISignaturePayloadBuilder
{
    public HashHex BuildIndependentInputPayloadHash(LedgerTransaction transaction, TransactionInput input)
    {
        var txId = transaction.TxId ?? hasher.CalculateTxId(transaction);
        var payload = string.Join('|',
            "cdia.economy.input.independent.v1",
            txId.Value,
            input.InputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            input.PreviousOutPoint.TxId.Value,
            input.PreviousOutPoint.OutputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            input.Address.Value);

        return Hex.Sha256Hex(Encoding.UTF8.GetBytes(payload));
    }

    public HashHex BuildOrderedInputPayloadHash(LedgerTransaction transaction, TransactionInput input)
    {
        var txId = transaction.TxId ?? hasher.CalculateTxId(transaction);
        var previousSignatures = transaction.Inputs
            .Where(x => x.InputIndex < input.InputIndex)
            .OrderBy(x => x.InputIndex)
            .SelectMany(x => x.SignatureBundle.Signatures)
            .OrderBy(x => x.KeyId, StringComparer.Ordinal)
            .ThenBy(x => x.Algorithm.ToString(), StringComparer.Ordinal)
            .Select(x => $"{x.Algorithm}:{x.KeyId}:{x.PayloadHash.Value}:{Convert.ToBase64String(x.SignatureBytes)}");

        var payload = string.Join('|',
            new[]
            {
                "cdia.economy.input.ordered.v1",
                txId.Value,
                input.InputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                input.PreviousOutPoint.TxId.Value,
                input.PreviousOutPoint.OutputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                input.Address.Value
            }.Concat(previousSignatures));

        return Hex.Sha256Hex(Encoding.UTF8.GetBytes(payload));
    }

    public HashHex BuildLedgerEntryPayloadHash(ConfirmedTransaction confirmedTransaction)
    {
        var payload = string.Join('|',
            "cdia.economy.ledger-entry.v1",
            confirmedTransaction.PreviousLedgerHash.Value,
            confirmedTransaction.LedgerSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            confirmedTransaction.Transaction.TxId?.Value ?? string.Empty,
            confirmedTransaction.Transaction.TxHash?.Value ?? string.Empty,
            confirmedTransaction.ConfirmedAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

        return Hex.Sha256Hex(Encoding.UTF8.GetBytes(payload));
    }
}
