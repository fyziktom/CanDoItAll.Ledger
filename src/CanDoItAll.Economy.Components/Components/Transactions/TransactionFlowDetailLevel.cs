namespace CanDoItAll.Economy.Components;

/// <summary>
/// Controls how much ledger protocol detail is exposed by <c>TransactionInputOutputFlow</c>.
/// Compact is intended for summaries; Detailed adds outpoints, metadata, and business-object identity.
/// </summary>
public enum TransactionFlowDetailLevel
{
    Compact,
    Detailed
}

/// <summary>
/// Optional domain annotation for a flow node. The flow owns layout while callers provide
/// transaction-specific meaning such as mint authority or permission return.
/// </summary>
public sealed record TransactionFlowItemPresentation(
    string? Label = null,
    string Tone = "neutral",
    string? SupportingText = null);
