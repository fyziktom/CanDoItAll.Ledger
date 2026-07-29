using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record LedgerTransactionDetailsModel(
    TxId TxId,
    HashHex? TxHash,
    TransactionKind Kind,
    TransactionStatus Status,
    SigningMode SigningMode,
    DateTimeOffset Timestamp,
    TransactionData? Data,
    IReadOnlyList<LedgerTransactionInputDetails> Inputs,
    IReadOnlyList<LedgerTransactionOutputDetails> Outputs,
    long? LedgerSequence = null,
    HashHex? LedgerEntryHash = null);

public sealed record LedgerTransactionInputDetails(
    int InputIndex,
    OutPoint PreviousOutPoint,
    Address Address,
    Amount? MainAmount,
    TokenTransfer? Token,
    LedgerTransactionBusinessObjectDetails? BusinessObject,
    SignatureBundle SignatureBundle,
    HashHex? SignaturePayloadHash = null,
    OutPointKey? OutPointKey = null)
{
    public int SignatureCount => SignatureBundle.Signatures.Count;
}

public sealed record LedgerTransactionOutputDetails(
    int OutputIndex,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token,
    LedgerTransactionBusinessObjectDetails? BusinessObject,
    HashHex? MetadataHash);

/// <summary>
/// BusinessObject protocol metadata shown by transaction UI. Public identity is deliberately
/// absent: an input BO id is its previous outpoint and an output BO id is the transaction
/// outpoint, so UI adapters never need to reconstruct or expose the internal lineage key.
/// </summary>
public sealed record LedgerTransactionBusinessObjectDetails(
    int Version,
    BusinessObjectOperation Operation,
    HashHex DataHash);
