using System.Globalization;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Services;

internal static class TransactionUiPresentation
{
    public static string KindLabel(TransactionKind kind)
        => kind switch
        {
            TransactionKind.BusinessObject => "Business object",
            _ => kind.ToString()
        };

    public static string DataTypeLabel(TransactionDataItemType itemType)
        => itemType switch
        {
            TransactionDataItemType.TokenMetadata => "Token metadata",
            TransactionDataItemType.BusinessObject => "Business object",
            TransactionDataItemType.AccountSettings => "Account settings",
            TransactionDataItemType.ScenarioMetadata => "Scenario metadata",
            TransactionDataItemType.SnapshotMetadata => "Snapshot metadata",
            _ => itemType.ToString()
        };

    public static string StatusTone(TransactionStatus status)
        => status switch
        {
            TransactionStatus.Confirmed => "success",
            TransactionStatus.Pending => "warning",
            TransactionStatus.Rejected or TransactionStatus.Reverted => "danger",
            TransactionStatus.Archived => "neutral",
            _ => "neutral"
        };

    public static string StatusIcon(TransactionStatus status)
        => status switch
        {
            TransactionStatus.Confirmed => "verified",
            TransactionStatus.Pending => "pending",
            TransactionStatus.Rejected => "cancel",
            TransactionStatus.Reverted => "history",
            TransactionStatus.Archived => "archive",
            _ => "receipt_long"
        };

    public static string FormatLocalTimestamp(DateTimeOffset timestamp)
        => timestamp.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string FormatLocalTimestampWithSeconds(DateTimeOffset timestamp)
        => timestamp.ToLocalTime().ToString("dd MMM yyyy · HH:mm:ss", CultureInfo.InvariantCulture);
}
