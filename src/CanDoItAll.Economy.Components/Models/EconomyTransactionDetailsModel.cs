using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record EconomyTransactionDetailsModel(
    TxId TxId,
    HashHex? TxHash,
    TransactionKind Kind,
    TransactionStatus Status,
    SigningMode SigningMode,
    DateTimeOffset Timestamp,
    TransactionData? Data,
    IReadOnlyList<EconomyTransactionInputDetails> Inputs,
    IReadOnlyList<EconomyTransactionOutputDetails> Outputs);

public sealed record EconomyTransactionInputDetails(
    int InputIndex,
    OutPoint PreviousOutPoint,
    Address Address,
    Amount? MainAmount,
    TokenTransfer? Token,
    BusinessObjectRef? BusinessObject,
    SignatureBundle SignatureBundle,
    HashHex? SignaturePayloadHash = null,
    OutPointKey? OutPointKey = null)
{
    public int SignatureCount => SignatureBundle.Signatures.Count;
}

public sealed record EconomyTransactionOutputDetails(
    int OutputIndex,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token,
    BusinessObjectRef? BusinessObject,
    HashHex? MetadataHash);
