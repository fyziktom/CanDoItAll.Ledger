namespace CanDoItAll.Economy.Core;

public sealed record RegisteredAddress(
    LedgerId LedgerId,
    Address Address,
    SignatureAlgorithmId KeyAlgorithm,
    byte[] PublicKeyMaterial,
    HashHex PublicKeyHash,
    string? DisplayName,
    AddressStatus Status,
    SignaturePolicy SignaturePolicy,
    SignatureBundle AdminSignature,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UnspentOutput(
    OutPointKey OutPointKey,
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    OutPoint OutPoint,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token,
    BusinessObjectRef? BusinessObject,
    DateTimeOffset CreatedAt);

public sealed record SpentOutput(
    OutPointKey OutPointKey,
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    OutPoint OutPoint,
    Address Address,
    TxId SpentInTxId,
    DateTimeOffset SpentAt,
    SignatureBundle AdminSignature);

public sealed record AddressBalanceProjection(
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    Address Address,
    Amount MainBalance,
    IReadOnlyDictionary<TokenId, Amount> TokenBalances,
    int UtxoCount,
    long LastLedgerSequence,
    HashHex StateHash,
    SignatureBundle AdminSignature,
    DateTimeOffset UpdatedAt);

public sealed record LedgerState(
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    long CurrentSequence,
    HashHex CurrentLedgerHash,
    DateTimeOffset UpdatedAt);

public sealed record LedgerSnapshot(
    string SnapshotId,
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    long LedgerSequence,
    HashHex LedgerHash,
    HashHex UtxoSetHash,
    HashHex AddressStateHash,
    HashHex BusinessObjectStateHash,
    DateTimeOffset CreatedAt,
    SignatureBundle AdminSignature);
