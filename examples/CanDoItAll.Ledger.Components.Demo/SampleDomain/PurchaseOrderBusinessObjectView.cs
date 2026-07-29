using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Demo.SampleDomain;

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
