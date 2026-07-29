using CanDoItAll.Ledger.BusinessObjects;

namespace CanDoItAll.Ledger.Components.Models;

[Flags]
public enum BusinessObjectCommerceFields
{
    Compact = 0,
    SaleMode = 1,
    DeliveredObject = 2,
    TransactionIds = 4,
    RejectionReason = 8,
    Detailed = SaleMode | DeliveredObject | TransactionIds | RejectionReason
}

/// <summary>
/// Normalized presentation row shared by payment, purchase, and receipt views.
/// </summary>
public sealed record BusinessObjectCommerceRow(
    ICommerceBusinessObjectView Source,
    IBusinessObjectView? RelatedObject,
    DateTimeOffset ActivityAt,
    string Kind,
    string KindLabel,
    string Icon,
    BusinessObjectCommerceRecordStatus Status,
    string StatusTone,
    decimal Amount,
    string Asset,
    string CounterpartyName,
    string CounterpartyAddress,
    string TargetName,
    string TargetObjectId,
    string? DeliveredName,
    string? DeliveredObjectId,
    BusinessObjectSaleMode SaleMode,
    string CurrentTransactionId,
    string? PaymentTransactionId,
    string? DispatchTransactionId,
    string? RejectionReason,
    string RecordTitle);
