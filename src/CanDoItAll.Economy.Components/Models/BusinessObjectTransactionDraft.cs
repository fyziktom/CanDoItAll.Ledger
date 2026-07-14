using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public enum BusinessObjectTransactionMode
{
    Create = 0,
    TransferObject = 1,
    UpdateObject = 2,
    CloseObject = 3,
    CloneObject = 4,
    MergeObjects = 5,
    SplitBusinessObjectPayment = 6,
    MainPayment = 7,
    TokenTransfer = 8,
    CreateRuleToken = 9,
    UtxoMerge = 10,
    UtxoSplit = 11
}

public sealed record BusinessObjectTransactionDraft(
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    Address OwnerAddress,
    Address ReceiverAddress,
    Address PaymentReceiverAddress,
    Amount MainAmount,
    Amount PaymentAmount,
    string StateJson,
    string ExternalRef,
    BusinessObjectTransactionMode SelectedMode = BusinessObjectTransactionMode.Create,
    BusinessObjectId? SourceObjectId = null,
    BusinessObjectId? SecondObjectId = null,
    TokenId? TokenId = null,
    Amount? TokenAmount = null,
    Amount? SecondaryPaymentAmount = null,
    IReadOnlyList<OutPoint>? SelectedUtxos = null,
    IReadOnlyList<UtxoSplitOutput>? UtxoSplitOutputs = null)
{
    public IReadOnlyList<OutPoint> SelectedUtxosOrEmpty => SelectedUtxos ?? [];

    public IReadOnlyList<UtxoSplitOutput> UtxoSplitOutputsOrEmpty => UtxoSplitOutputs ?? [];
}

public sealed record EconomyTransactionExecutionResult(
    BusinessObjectTransactionMode Mode,
    bool Accepted,
    TxId? TxId,
    long? LedgerSequence,
    string Message,
    int? BatchIndex = null,
    string? CorrelationExternalRef = null)
{
    public BusinessObjectId? ResultBusinessObjectId
        => Accepted && TxId is { } txId && Mode is
            BusinessObjectTransactionMode.Create
            or BusinessObjectTransactionMode.TransferObject
            or BusinessObjectTransactionMode.UpdateObject
            or BusinessObjectTransactionMode.CloneObject
            or BusinessObjectTransactionMode.MergeObjects
            or BusinessObjectTransactionMode.SplitBusinessObjectPayment
            or BusinessObjectTransactionMode.CreateRuleToken
                ? BusinessObjectId.FromOutPoint(txId, 0)
                : null;
}

public sealed record BusinessObjectBatchTransactionRequest(
    string Label,
    IReadOnlyList<BusinessObjectTransactionDraft> Drafts,
    string Summary,
    string RecipientMode)
{
    public (int DraftIndex, string? ExternalRef) CorrelationFor(int draftIndex)
    {
        if (draftIndex < 0 || draftIndex >= Drafts.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(draftIndex));
        }

        string? externalRef = string.IsNullOrWhiteSpace(Drafts[draftIndex].ExternalRef)
            ? null
            : Drafts[draftIndex].ExternalRef;
        return (draftIndex, externalRef);
    }
}

public sealed record EconomyTransactionBatchExecutionResult(
    bool Accepted,
    int AcceptedCount,
    int SubmittedCount,
    IReadOnlyList<EconomyTransactionExecutionResult> Results,
    string Message);
