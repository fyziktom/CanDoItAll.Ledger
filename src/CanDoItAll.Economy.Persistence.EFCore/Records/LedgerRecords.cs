namespace CanDoItAll.Economy.Persistence.EFCore.Records;

public sealed class LedgerStateRecord
{
    public string StateKey { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public long CurrentSequence { get; set; }
    public string CurrentLedgerHash { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LedgerTransactionRecord
{
    public string TxId { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public long? LedgerSequence { get; set; }
    public int Version { get; set; }
    public int Kind { get; set; }
    public int SigningMode { get; set; }
    public int Status { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? PreviousLedgerHash { get; set; }
    public string? LedgerEntryHash { get; set; }
    public string? TxHash { get; set; }
    public int? DataItemType { get; set; }
    public string? DataContentType { get; set; }
    public string? DataHash { get; set; }
    public string? InlineJson { get; set; }
    public string? ExternalRef { get; set; }
    public string? TokenDefinitionJson { get; set; }
    public string? AdminSignatureJson { get; set; }
}

public sealed class LedgerTransactionInputRecord
{
    public long Id { get; set; }
    public string TxId { get; set; } = string.Empty;
    public int InputIndex { get; set; }
    public string PreviousTxId { get; set; } = string.Empty;
    public int PreviousOutputIndex { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? OutPointKey { get; set; }
    public string? SignaturePayloadHash { get; set; }
    public string? SignatureBundleJson { get; set; }
}

public sealed class LedgerTransactionOutputRecord
{
    public long Id { get; set; }
    public string TxId { get; set; } = string.Empty;
    public int OutputIndex { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal MainAmount { get; set; }
    public string? TokenId { get; set; }
    public decimal? TokenAmount { get; set; }
    public string? BusinessObjectId { get; set; }
    public int? BusinessObjectVersion { get; set; }
    public int? BusinessObjectOperation { get; set; }
    public string? BusinessObjectDataHash { get; set; }
    public string? MetadataHash { get; set; }
}

public sealed class UnspentOutputRecord
{
    public string OutPointKey { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public string TxId { get; set; } = string.Empty;
    public int OutputIndex { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal MainAmount { get; set; }
    public string? TokenId { get; set; }
    public decimal? TokenAmount { get; set; }
    public string? BusinessObjectId { get; set; }
    public int? BusinessObjectVersion { get; set; }
    public int? BusinessObjectOperation { get; set; }
    public string? BusinessObjectDataHash { get; set; }
    public string? MetadataHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SpentOutputRecord
{
    public string OutPointKey { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public string TxId { get; set; } = string.Empty;
    public int OutputIndex { get; set; }
    public string Address { get; set; } = string.Empty;
    public string SpentInTxId { get; set; } = string.Empty;
    public DateTimeOffset SpentAt { get; set; }
    public string? AdminSignatureJson { get; set; }
}

public sealed class AddressStateProjectionRecord
{
    public string ProjectionKey { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal MainBalance { get; set; }
    public string TokenBalancesJson { get; set; } = "{}";
    public int UtxoCount { get; set; }
    public long LastLedgerSequence { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public string? AdminSignatureJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LedgerTokenTypeRecord
{
    public string TokenTypeKey { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public string TokenId { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; } = 1;
    public string? DefinitionHash { get; set; }
    public string? TokenNamespace { get; set; }
    public string? Symbol { get; set; }
    public string? IssuerAddress { get; set; }
    public decimal? InitialSupply { get; set; }
    public int? DecimalPlaces { get; set; }
    public int Classification { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? DisplayExternalRef { get; set; }
    public string CreatedTxId { get; set; } = string.Empty;
    public int CreatedOutputIndex { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RegisteredAddressRecord
{
    public string AddressKey { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int KeyAlgorithm { get; set; }
    public byte[] PublicKeyMaterial { get; set; } = [];
    public string PublicKeyHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int Status { get; set; }
    public string SignaturePolicyJson { get; set; } = "{}";
    public string? AdminSignatureJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class BusinessObjectRecord
{
    public string BusinessObjectKey { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public string CurrentOwnerAddress { get; set; } = string.Empty;
    public string CurrentState { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
    public string CurrentOutPointTxId { get; set; } = string.Empty;
    public int CurrentOutPointIndex { get; set; }
    public string CreatedTxId { get; set; } = string.Empty;
    public string UpdatedTxId { get; set; } = string.Empty;
}

public sealed class BusinessObjectVersionRecord
{
    public long Id { get; set; }
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public string ObjectId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string TxId { get; set; } = string.Empty;
    public string OutPointTxId { get; set; } = string.Empty;
    public int OutPointIndex { get; set; }
    public string OwnerAddress { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string DataHash { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LedgerSnapshotRecord
{
    public string SnapshotId { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string? ScenarioId { get; set; }
    public long LedgerSequence { get; set; }
    public string LedgerHash { get; set; } = string.Empty;
    public string UtxoSetHash { get; set; } = string.Empty;
    public string AddressStateHash { get; set; } = string.Empty;
    public string BusinessObjectStateHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? AdminSignatureJson { get; set; }
}
