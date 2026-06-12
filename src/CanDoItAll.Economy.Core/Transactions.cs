namespace CanDoItAll.Economy.Core;

public sealed record TokenTransfer(TokenId TokenId, Amount Amount);

public sealed record BusinessObjectRef(
    BusinessObjectId ObjectId,
    int Version,
    BusinessObjectOperation Operation,
    HashHex DataHash);

public sealed record TransactionInput(
    int InputIndex,
    OutPoint PreviousOutPoint,
    Address Address,
    SignatureBundle SignatureBundle,
    HashHex? SignaturePayloadHash = null);

public sealed record TransactionOutput(
    int OutputIndex,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token = null,
    BusinessObjectRef? BusinessObject = null,
    HashHex? MetadataHash = null);

public sealed record TransactionData(
    string? ContentType,
    HashHex? DataHash,
    string? InlineJson,
    string? ExternalRef,
    TransactionDataItemType ItemType = TransactionDataItemType.Generic);

public sealed record LedgerTransaction(
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    int Version,
    TransactionKind Kind,
    SigningMode SigningMode,
    DateTimeOffset Timestamp,
    IReadOnlyList<TransactionInput> Inputs,
    IReadOnlyList<TransactionOutput> Outputs,
    TransactionData? Data,
    TxId? TxId = null,
    HashHex? TxHash = null,
    TransactionStatus Status = TransactionStatus.Pending);

public sealed record ConfirmedTransaction(
    LedgerTransaction Transaction,
    long LedgerSequence,
    HashHex PreviousLedgerHash,
    HashHex LedgerEntryHash,
    SignatureBundle AdminSignature,
    DateTimeOffset ConfirmedAt);
