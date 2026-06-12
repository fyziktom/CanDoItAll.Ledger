using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record UpdateBusinessObjectRequest(
    BusinessObjectId ObjectId,
    string StateJson,
    string? ExternalRef = null);

public sealed record CloseBusinessObjectRequest(
    BusinessObjectId ObjectId,
    string StateJson = "{}",
    string? ExternalRef = null);

public sealed record CloneBusinessObjectRequest(
    BusinessObjectId SourceObjectId,
    BusinessObjectId NewObjectId,
    string ObjectType,
    string StateJson,
    string SchemaVersion = "1.0",
    Address? OwnerAddress = null);

public sealed record MergeBusinessObjectsRequest(
    BusinessObjectId FirstObjectId,
    BusinessObjectId SecondObjectId,
    BusinessObjectId TargetObjectId,
    string StateJson,
    string? ExternalRef = null);

public sealed record BusinessObjectPaymentOutput(Address ReceiverAddress, Amount Amount);

public sealed record SplitBusinessObjectPaymentRequest(
    BusinessObjectId ObjectId,
    IReadOnlyList<BusinessObjectPaymentOutput> Payments,
    string? StateJson = null,
    string? ExternalRef = null);

public sealed record SetBusinessObjectPriceRequest(
    BusinessObjectId ObjectId,
    Amount PriceAmount,
    TokenId? TokenId,
    string StateJson,
    string? ExternalRef = null);
