using System.Text.Json.Serialization;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Sdk;

/// <param name="LegacyBusinessObjectCreationMarker">
/// Legacy compatibility marker serialized as <c>businessObjectId</c>. Its value never becomes
/// a BusinessObject id or lineage key; when present, the node generates an opaque lineage key
/// and the confirmed output outpoint becomes the public BusinessObject id.
/// </param>
public sealed record MintTransactionRequest(
    string Address,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal MainAmount,
    string? TokenId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? TokenAmount,
    string? DataItemType,
    string? DataJson,
    string? ExternalRef,
    [property: JsonPropertyName("businessObjectId")] string? LegacyBusinessObjectCreationMarker,
    string? ScenarioId,
    TokenDefinitionEnvelope? TokenDefinition = null);

/// <param name="LegacyBusinessObjectCreationMarker">
/// Legacy compatibility marker serialized as <c>businessObjectId</c>. Its value never becomes
/// a BusinessObject id or lineage key; when present, the node generates an opaque lineage key
/// and the confirmed output outpoint becomes the public BusinessObject id.
/// </param>
public sealed record TransferTransactionRequest(
    string FromAddress,
    string ToAddress,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal MainAmount,
    string? TokenId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? TokenAmount,
    string? DataItemType,
    string? DataJson,
    string? ExternalRef,
    [property: JsonPropertyName("businessObjectId")] string? LegacyBusinessObjectCreationMarker,
    string? ScenarioId);

public sealed record TokenAuthorityGrantRequest(
    string TokenNamespace,
    string Address,
    string? ScenarioId);

public sealed record PermissionGrantRequest(
    string TokenNamespace,
    string Address,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? Amount = 1m,
    bool? Repeatable = true,
    string? Action = PermissionTokenAuthority.MintNewTokenTypeAction,
    string? ScenarioId = null);

public sealed record LedgerBusinessObjectQueryRequest(
    string? ScenarioId,
    string? ObjectId,
    string? ObjectType,
    string? SchemaVersion,
    string? OwnerAddress,
    IReadOnlyDictionary<string, string>? StateProperties,
    int Skip = 0,
    int Take = 100);

public sealed record LedgerTransactionQueryRequest(
    string? ScenarioId,
    string? Address,
    string? BusinessObjectId,
    string? Kind,
    bool IncludeData = false,
    int Skip = 0,
    int Take = 100);

/// <summary>
/// Public node representation of a spendable output. A BusinessObject exposes its current
/// outpoint-derived id separately from the V1/V2 lineage key required to construct follow-up
/// transactions.
/// </summary>
public sealed record LedgerNodeSpendableOutput(
    LedgerAccountContext Context,
    OutPointKey OutPointKey,
    OutPoint OutPoint,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token,
    LedgerNodeSpendableBusinessObjectDetails? BusinessObject,
    DateTimeOffset CreatedAt);

public sealed record LedgerNodeSpendableBusinessObjectDetails(
    BusinessObjectId ObjectId,
    BusinessObjectLineageKey LineageKey,
    int Version,
    BusinessObjectOperation Operation,
    HashHex DataHash);

/// <summary>
/// Public transaction read contract. Its output BusinessObject metadata intentionally omits
/// the internal protocol lineage key and exposes only the canonical output-derived object id.
/// </summary>
public sealed record LedgerNodeLedgerTransactionSummary(
    LedgerAccountContext Context,
    TxId TxId,
    TransactionKind Kind,
    TransactionStatus Status,
    DateTimeOffset Timestamp,
    IReadOnlyList<Address> InputAddresses,
    IReadOnlyList<Address> OutputAddresses,
    IReadOnlyList<LedgerTransactionInputSummary> Inputs,
    IReadOnlyList<LedgerNodeLedgerTransactionOutputSummary> Outputs,
    TransactionData? Data = null,
    long? LedgerSequence = null);

public sealed record LedgerNodeLedgerTransactionOutputSummary(
    int OutputIndex,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token = null,
    LedgerBusinessObjectOutputDetails? BusinessObject = null,
    HashHex? MetadataHash = null);

public sealed record AdvanceTimeRequest(double Days, double Hours);

public sealed record AdvanceTimeResponse(DateTimeOffset UtcNow);

public sealed record LedgerResetResponse(string Status);

public sealed record TransactionReceipt(string TxId, long LedgerSequence);

public sealed record TokenAuthorityGrantReceipt(
    string GrantObjectId,
    string TokenNamespace,
    string Address,
    string MintTxId,
    string TransferTxId,
    long TransferLedgerSequence);

public sealed record PermissionGrantReceipt(
    string PermissionObjectId,
    string Action,
    string TokenNamespace,
    bool Repeatable,
    decimal Amount,
    string AmountText,
    string Address,
    string TxId,
    long LedgerSequence);

public sealed record NodeBootstrapInfo(
    string LedgerId,
    bool BootstrapEnabled,
    bool GenesisCreated,
    string AdminAddress,
    string KeyId,
    string Algorithm,
    string PublicKeyHash,
    string PublicKeyMaterial,
    string PublicKeyMaterialBase64,
    decimal TreasuryMainAmount,
    string TreasuryMainAmountText,
    bool TokenCreationPolicyEnabled,
    string ApiVersion,
    string ApiBasePath,
    string? LegacyApiBasePath,
    string SecurityProfile,
    string WriteAuthorizationMode,
    string ReadAuthorizationMode,
    string UiAuthorizationMode,
    bool TwoFactorRequired,
    string AuthPersistenceProvider,
    bool JwtActiveTokenRecordRequired,
    string LedgerAdminSignerMode,
    bool LedgerResetEnabled,
    string SchemaManagementMode,
    bool TrustedAdminVerificationRequired);

public sealed record NodeHealthResponse(
    string Status,
    DateTimeOffset UtcNow,
    string StorageProvider,
    string LedgerId,
    long LatestLedgerSequence,
    string LatestLedgerHash,
    DateTimeOffset? LedgerStateUpdatedAt,
    string VerificationStatus,
    DateTimeOffset VerificationCheckedAt,
    int VerificationIssueCount,
    IReadOnlyList<string> VerificationIssueCodes,
    string ProjectionVerificationStatus,
    string ProjectionHash,
    int ProjectionIssueCount,
    IReadOnlyList<string> ProjectionIssueCodes,
    bool BootstrapEnabled,
    bool GenesisCreated,
    string ApiVersion,
    string ApiBasePath,
    string? LegacyApiBasePath,
    string SecurityProfile,
    string WriteAuthorizationMode,
    string ReadAuthorizationMode,
    string UiAuthorizationMode,
    bool TwoFactorRequired,
    string AuthPersistenceProvider,
    bool JwtActiveTokenRecordRequired,
    string LedgerAdminSignerMode,
    bool LedgerResetEnabled,
    string SchemaManagementMode,
    bool TrustedAdminVerificationRequired);

public sealed record AccountSummary(
    string Address,
    decimal MainBalance,
    string MainBalanceText,
    IReadOnlyDictionary<string, decimal> TokenBalances,
    IReadOnlyDictionary<string, string> TokenBalanceTexts,
    int UtxoCount);

public sealed record TransactionSummary(string TxId, long LedgerSequence, string Kind, string Status, DateTimeOffset ConfirmedAt, string? DataItemType);

public sealed record TransactionDetails(
    string TxId,
    long LedgerSequence,
    string Kind,
    string Status,
    DateTimeOffset Timestamp,
    DateTimeOffset? ConfirmedAt,
    string? TxHash,
    string? LedgerEntryHash,
    string? DataItemType,
    string? DataContentType,
    string? DataHash,
    string? InlineJson,
    string? ExternalRef,
    IReadOnlyList<TransactionInputDetails> Inputs,
    IReadOnlyList<TransactionOutputDetails> Outputs);

public sealed record TransactionInputDetails(
    int InputIndex,
    string PreviousTxId,
    int PreviousOutputIndex,
    string Address,
    string? OutPointKey,
    string? SignaturePayloadHash,
    SignatureBundle SignatureBundle);

public sealed record TransactionOutputDetails(
    int OutputIndex,
    string Address,
    decimal MainAmount,
    string? TokenId,
    decimal? TokenAmount,
    string? BusinessObjectId,
    int? BusinessObjectVersion,
    string? BusinessObjectOperation,
    string? BusinessObjectDataHash);
