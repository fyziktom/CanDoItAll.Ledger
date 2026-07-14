using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record UpdateBusinessObjectRequest(
    BusinessObjectId ObjectId,
    string StateJson,
    string? ExternalRef = null,
    string? ObjectType = null,
    string? SchemaVersion = null);

public sealed record CloseBusinessObjectRequest(
    BusinessObjectId ObjectId,
    string StateJson = "{}",
    string? ExternalRef = null,
    string? ObjectType = null,
    string? SchemaVersion = null);

public sealed record CloneBusinessObjectRequest(
    BusinessObjectId SourceObjectId,
    string ObjectType,
    string StateJson,
    string SchemaVersion = "1.0",
    Address? OwnerAddress = null,
    TokenTransfer? Token = null,
    OutPoint? SourceOutPoint = null)
{
    internal BusinessObjectLineageKey? CompatibilityLineageKey { get; init; }

    [Obsolete("Caller-selected clone ids are deprecated. Use the overload without NewObjectId; public identity is assigned from the confirmed output.")]
    public CloneBusinessObjectRequest(
        BusinessObjectId SourceObjectId,
        BusinessObjectId NewObjectId,
        string ObjectType,
        string StateJson,
        string SchemaVersion = "1.0",
        Address? OwnerAddress = null,
        TokenTransfer? Token = null,
        OutPoint? SourceOutPoint = null)
        : this(SourceObjectId, ObjectType, StateJson, SchemaVersion, OwnerAddress, Token, SourceOutPoint)
    {
        CompatibilityLineageKey = new BusinessObjectLineageKey(NewObjectId.Value);
    }
}

public sealed record MergeBusinessObjectsRequest(
    BusinessObjectId FirstObjectId,
    BusinessObjectId SecondObjectId,
    string ObjectType,
    string StateJson,
    string SchemaVersion = "1.0",
    string? ExternalRef = null)
{
    internal BusinessObjectLineageKey? CompatibilityLineageKey { get; init; }

    [Obsolete("Caller-selected merge ids are deprecated. Use the overload without TargetObjectId; public identity is assigned from the confirmed output.")]
    public MergeBusinessObjectsRequest(
        BusinessObjectId FirstObjectId,
        BusinessObjectId SecondObjectId,
        BusinessObjectId TargetObjectId,
        string StateJson,
        string? ExternalRef = null)
        : this(FirstObjectId, SecondObjectId, "business-object", StateJson, "1.0", ExternalRef)
    {
        CompatibilityLineageKey = new BusinessObjectLineageKey(TargetObjectId.Value);
    }
}

public sealed record BusinessObjectPaymentOutput(Address ReceiverAddress, Amount Amount);

public sealed record SplitBusinessObjectPaymentRequest(
    BusinessObjectId ObjectId,
    IReadOnlyList<BusinessObjectPaymentOutput> Payments,
    string? StateJson = null,
    string? ExternalRef = null,
    string? ObjectType = null,
    string? SchemaVersion = null);

public sealed record SetBusinessObjectPriceRequest(
    BusinessObjectId ObjectId,
    Amount PriceAmount,
    TokenId? TokenId,
    string StateJson,
    string? ExternalRef = null,
    string? ObjectType = null,
    string? SchemaVersion = null);
