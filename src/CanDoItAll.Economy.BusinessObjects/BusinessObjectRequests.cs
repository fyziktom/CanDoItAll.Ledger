using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record CreateBusinessObjectRequest(
    BusinessObjectId ObjectId,
    string ObjectType,
    string StateJson,
    string SchemaVersion = "1.0",
    Address? OwnerAddress = null,
    Amount? MainAmount = null,
    TokenTransfer? Token = null,
    TransactionDataItemType ItemType = TransactionDataItemType.BusinessObject,
    string ContentType = "application/json",
    string? ExternalRef = null,
    OutPoint? SourceOutPoint = null);

public sealed record TransferBusinessObjectRequest(
    BusinessObjectId ObjectId,
    Address ReceiverAddress,
    Amount? MainAmount = null,
    string? StateJson = null,
    string? ExternalRef = null);

public sealed record PaymentRequest(
    Address ReceiverAddress,
    Amount Amount);

public sealed record TokenTransferRequest(
    TokenId TokenId,
    Amount TokenAmount,
    Address ReceiverAddress,
    Amount? MainAmount = null);

public sealed record TokenMintRequest(
    TokenId TokenId,
    Amount TokenAmount,
    Address? ReceiverAddress = null,
    Amount? MainAmount = null,
    OutPoint? PermissionSourceOutPoint = null);

public sealed record PermissionIssueRequest(
    Address ReceiverAddress,
    string TokenNamespace,
    Amount Amount,
    bool Repeatable = true,
    string Action = PermissionTokenAuthority.MintNewTokenTypeAction,
    OutPoint? SourceOutPoint = null);

public sealed record RuleTokenCreateRequest(
    AccountRuleDefinition Rule,
    Address? OwnerAddress = null,
    OutPoint? PermissionSourceOutPoint = null);
