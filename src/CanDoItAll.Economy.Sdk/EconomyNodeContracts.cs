using System.Text.Json.Serialization;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Sdk;

public sealed record MintTransactionRequest(
    string Address,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal MainAmount,
    string? TokenId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? TokenAmount,
    string? DataItemType,
    string? DataJson,
    string? ExternalRef,
    string? BusinessObjectId,
    string? ScenarioId,
    TokenDefinitionEnvelope? TokenDefinition = null);

public sealed record TransferTransactionRequest(
    string FromAddress,
    string ToAddress,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal MainAmount,
    string? TokenId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? TokenAmount,
    string? DataItemType,
    string? DataJson,
    string? ExternalRef,
    string? BusinessObjectId,
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
