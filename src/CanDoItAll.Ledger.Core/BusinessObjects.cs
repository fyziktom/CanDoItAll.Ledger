namespace CanDoItAll.Ledger.Core;

public sealed record BusinessObject(
    BusinessObjectId ObjectId,
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    string ObjectType,
    int CurrentVersion,
    Address CurrentOwnerAddress,
    string CurrentState,
    HashHex CurrentHash,
    OutPoint CurrentOutPoint,
    TxId CreatedTxId,
    TxId UpdatedTxId);

public sealed record BusinessObjectVersion(
    BusinessObjectId ObjectId,
    int Version,
    TxId TxId,
    OutPoint OutPoint,
    Address OwnerAddress,
    string State,
    string SchemaVersion,
    HashHex DataHash,
    string? DataJson,
    DateTimeOffset CreatedAt);
