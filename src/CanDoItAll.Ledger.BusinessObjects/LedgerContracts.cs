using System.Text.Json.Serialization;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.BusinessObjects;

public sealed record LedgerAccountContext(LedgerId LedgerId, ScenarioId? ScenarioId = null);

public sealed record LedgerAddressQuery(LedgerAccountContext Context, Address Address);

public sealed record LedgerBusinessObjectQuery(
    LedgerAccountContext Context,
    BusinessObjectId? ObjectId = null,
    string? ObjectType = null,
    string? SchemaVersion = null,
    Address? OwnerAddress = null,
    IReadOnlyDictionary<string, string>? StateProperties = null,
    int Skip = 0,
    int Take = 100);

public sealed record LedgerTransactionQuery(
    LedgerAccountContext Context,
    Address? Address = null,
    BusinessObjectId? BusinessObjectId = null,
    TransactionKind? Kind = null,
    bool IncludeData = false,
    int Skip = 0,
    int Take = 100);

public sealed record LedgerSpendableOutput(
    LedgerAccountContext Context,
    OutPointKey OutPointKey,
    OutPoint OutPoint,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token,
    BusinessObjectRef? BusinessObject,
    DateTimeOffset CreatedAt);

public sealed record LedgerAccountBalance(
    LedgerAccountContext Context,
    Address Address,
    Amount MainBalance,
    IReadOnlyDictionary<TokenId, Amount> TokenBalances,
    int UtxoCount,
    DateTimeOffset AsOfUtc);

public sealed record LedgerBusinessObjectHolding(
    BusinessObjectId ObjectId,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    DateTimeOffset CreatedAt,
    string ObjectType = "business-object",
    string SchemaVersion = "1.0",
    string? StateJson = null,
    string? ExternalRef = null,
    TransactionDataItemType ItemType = TransactionDataItemType.BusinessObject,
    IReadOnlyList<BusinessObjectDataReference>? References = null);

public sealed record LedgerTokenType(
    TokenId TokenId,
    Amount TotalAmount,
    Amount SourceAmount,
    Amount UniqueAmount,
    int SourceUtxoCount,
    int UniqueUtxoCount,
    int TotalUtxoCount);

public sealed record LedgerPermissionSource(
    LedgerSpendableOutput Output,
    PermissionGrant Grant)
{
    public OutPoint OutPoint => Output.OutPoint;

    public Amount Amount => Output.Token?.Amount ?? Amount.Zero;

    public bool AllowsMint(TokenId tokenId)
        => string.Equals(Grant.Action, PermissionTokenAuthority.MintNewTokenTypeAction, StringComparison.Ordinal)
            && PermissionTokenAuthority.IsTokenInNamespace(tokenId, Grant.Namespace);

    public bool AllowsMintNamespace(string tokenNamespace)
        => string.Equals(Grant.Action, PermissionTokenAuthority.MintNewTokenTypeAction, StringComparison.Ordinal)
            && PermissionTokenAuthority.IsTokenInNamespace(new TokenId(tokenNamespace), Grant.Namespace);

    public bool AllowsRuleCreation(string ruleNamespace)
        => string.Equals(Grant.Action, RuleTokenAuthority.CreateRuleTokenAction, StringComparison.Ordinal)
            && PermissionTokenAuthority.IsTokenInNamespace(new TokenId(ruleNamespace), Grant.Namespace);
}

public sealed record LedgerTransactionInputSummary
{
    public LedgerTransactionInputSummary(
        int inputIndex,
        OutPoint previousOutPoint,
        Address address,
        OutPointKey? outPointKey = null,
        SignatureBundle? signatureBundle = null,
        HashHex? signaturePayloadHash = null)
    {
        InputIndex = inputIndex;
        PreviousOutPoint = previousOutPoint;
        Address = address;
        OutPointKey = outPointKey;
        SignatureBundle = signatureBundle ?? SignatureBundle.Empty;
        SignaturePayloadHash = signaturePayloadHash;
    }

    public int InputIndex { get; init; }

    public OutPoint PreviousOutPoint { get; init; }

    public Address Address { get; init; }

    public OutPointKey? OutPointKey { get; init; }

    public SignatureBundle SignatureBundle { get; init; }

    public HashHex? SignaturePayloadHash { get; init; }
}

/// <summary>
/// Public identity and immutable version metadata for a BusinessObject transaction output.
/// The object id is always the containing transaction output's canonical outpoint.
/// </summary>
public sealed record LedgerBusinessObjectOutputDetails(
    BusinessObjectId ObjectId,
    int Version,
    BusinessObjectOperation Operation,
    HashHex DataHash);

public sealed record LedgerTransactionOutputSummary(
    int OutputIndex,
    Address Address,
    Amount MainAmount,
    TokenTransfer? Token = null,
    [property: JsonIgnore] BusinessObjectRef? BusinessObject = null,
    HashHex? MetadataHash = null,
    [property: JsonPropertyName("businessObject")] LedgerBusinessObjectOutputDetails? BusinessObjectDetails = null)
{
    [JsonIgnore]
    public bool HasBusinessObject => BusinessObject is not null || BusinessObjectDetails is not null;

    [JsonIgnore]
    public int? BusinessObjectVersion => BusinessObjectDetails?.Version ?? BusinessObject?.Version;

    [JsonIgnore]
    public BusinessObjectOperation? BusinessObjectOperation => BusinessObjectDetails?.Operation ?? BusinessObject?.Operation;

    [JsonIgnore]
    public HashHex? BusinessObjectDataHash => BusinessObjectDetails?.DataHash ?? BusinessObject?.DataHash;
}

public sealed record LedgerTransactionSummary(
    LedgerAccountContext Context,
    TxId TxId,
    TransactionKind Kind,
    TransactionStatus Status,
    DateTimeOffset Timestamp,
    IReadOnlyList<Address> InputAddresses,
    IReadOnlyList<Address> OutputAddresses,
    IReadOnlyList<LedgerTransactionInputSummary> Inputs,
    IReadOnlyList<LedgerTransactionOutputSummary> Outputs,
    TransactionData? Data = null,
    long? LedgerSequence = null);

public interface ILedgerGateway
{
    Task<LedgerSubmitResult> SubmitTransactionAsync(LedgerTransaction transaction, CancellationToken cancellationToken);
}

public interface ILedgerReadClient
{
    Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(LedgerAddressQuery query, CancellationToken cancellationToken);

    Task<LedgerAccountBalance> GetBalanceAsync(LedgerAddressQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(LedgerAddressQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(LedgerAddressQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAddressQuery query,
        bool includeData,
        CancellationToken cancellationToken)
        => GetTransactionsAsync(query, cancellationToken);

    Task<LedgerState?> GetLedgerStateAsync(
        LedgerAccountContext context,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Ledger state reads are not supported by this read client.");

    Task<LedgerTransactionSummary?> GetTransactionAsync(
        LedgerAccountContext context,
        TxId txId,
        bool includeData,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Transaction detail reads are not supported by this read client.");

    Task<LedgerBusinessObjectHolding?> GetBusinessObjectAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("BusinessObject detail reads are not supported by this read client.");

    Task<IReadOnlyList<LedgerBusinessObjectHolding>> QueryBusinessObjectsAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("BusinessObject query reads are not supported by this read client.");

    Task<IReadOnlyList<LedgerTransactionSummary>> QueryTransactionsAsync(
        LedgerTransactionQuery query,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Transaction query reads are not supported by this read client.");
}
