using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Demo.SampleDomain;

public sealed record PurchaseOrderBusinessObjectView(
    LedgerAccountContext Context,
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    string? StateJson,
    string Supplier,
    Amount TotalAmount,
    string Status,
    DateTimeOffset RequiredByUtc,
    DateTimeOffset CreatedAt,
    DateTimeOffset ViewedAt) : IBusinessObjectView;
