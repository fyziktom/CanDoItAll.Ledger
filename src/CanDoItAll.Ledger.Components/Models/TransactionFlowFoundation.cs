using System.Text.Json;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public enum TransactionFlowFamily
{
    Payment,
    Token,
    Utxo,
    BusinessObject
}

public enum TransactionFlowDisposition
{
    Direct,
    Delegated
}

public enum BusinessObjectSourceMode
{
    Mine,
    Address,
    DirectReference
}

public sealed record BusinessObjectLookupRequest(
    BusinessObjectSourceMode SourceMode,
    string Query,
    int Take = 50);

public sealed record TransactionFlowDefinition(
    BusinessObjectTransactionMode Mode,
    TransactionFlowFamily Family,
    string Label,
    string Description,
    string Icon,
    TransactionFlowDisposition Disposition = TransactionFlowDisposition.Direct);

public static class TransactionFlowCatalog
{
    public static IReadOnlyList<TransactionFlowDefinition> All { get; } =
    [
        new(BusinessObjectTransactionMode.MainPayment, TransactionFlowFamily.Payment, "Payment", "Send the ledger's main currency.", "payments"),
        new(BusinessObjectTransactionMode.TokenTransfer, TransactionFlowFamily.Token, "Token transfer", "Send an available token balance.", "toll"),
        new(BusinessObjectTransactionMode.CreateRuleToken, TransactionFlowFamily.Token, "Create rule token", "Open the specialized authority-aware token creation flow.", "add_circle", TransactionFlowDisposition.Delegated),
        new(BusinessObjectTransactionMode.UtxoMerge, TransactionFlowFamily.Utxo, "Merge UTXOs", "Combine two or more plain main-currency outputs.", "merge"),
        new(BusinessObjectTransactionMode.UtxoSplit, TransactionFlowFamily.Utxo, "Split UTXOs", "Allocate selected plain outputs across one or more receivers.", "call_split"),
        new(BusinessObjectTransactionMode.Create, TransactionFlowFamily.BusinessObject, "Create object", "Create a new business object with typed metadata and state.", "note_add"),
        new(BusinessObjectTransactionMode.TransferObject, TransactionFlowFamily.BusinessObject, "Transfer object", "Select an owned object and transfer it to another address.", "move_up"),
        new(BusinessObjectTransactionMode.UpdateObject, TransactionFlowFamily.BusinessObject, "Update object", "Select an owned object and publish its next state.", "edit_note"),
        new(BusinessObjectTransactionMode.CloseObject, TransactionFlowFamily.BusinessObject, "Close object", "Select an owned object and record its final state.", "archive"),
        new(BusinessObjectTransactionMode.CloneObject, TransactionFlowFamily.BusinessObject, "Clone object", "Find a source object, preview it, and create a new object from it.", "content_copy"),
        new(BusinessObjectTransactionMode.MergeObjects, TransactionFlowFamily.BusinessObject, "Merge objects", "Select two compatible owned objects and create their replacement.", "join_full"),
        new(BusinessObjectTransactionMode.SplitBusinessObjectPayment, TransactionFlowFamily.BusinessObject, "Split object payment", "Select an object and allocate its payment to multiple receivers.", "splitscreen")
    ];

    private static readonly IReadOnlyDictionary<BusinessObjectTransactionMode, TransactionFlowDefinition> ByMode
        = All.ToDictionary(item => item.Mode);

    public static TransactionFlowDefinition Get(BusinessObjectTransactionMode mode)
        => ByMode.TryGetValue(mode, out TransactionFlowDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown transaction mode.");

    public static IReadOnlyList<TransactionFlowDefinition> ForFamily(TransactionFlowFamily family)
        => All.Where(item => item.Family == family).ToArray();
}

public sealed record TransactionFlowIssue(string Code, string Message, string? Field = null);

public sealed record TransactionFlowValidation(
    IReadOnlyList<TransactionFlowIssue> Errors,
    IReadOnlyList<TransactionFlowIssue> Warnings)
{
    public bool IsValid => Errors.Count == 0;

    public static TransactionFlowValidation Valid { get; } = new([], []);
}

public sealed record TransactionDraftValidationContext(
    LedgerAccountBalance? Balance = null,
    IReadOnlyList<LedgerSpendableOutput>? SpendableOutputs = null,
    IReadOnlyList<IBusinessObjectView>? BusinessObjects = null)
{
    public IReadOnlyList<LedgerSpendableOutput> SpendableOutputsOrEmpty => SpendableOutputs ?? [];

    public IReadOnlyList<IBusinessObjectView> BusinessObjectsOrEmpty => BusinessObjects ?? [];
}

public static class TransactionDraftValidator
{
    public static TransactionFlowValidation Validate(
        BusinessObjectTransactionDraft draft,
        TransactionDraftValidationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        context ??= new TransactionDraftValidationContext();

        var errors = new List<TransactionFlowIssue>();
        var warnings = new List<TransactionFlowIssue>();

        switch (draft.SelectedMode)
        {
            case BusinessObjectTransactionMode.Create:
                RequireText(draft.ObjectType, "objectType.required", "Object type is required.", "objectType", errors);
                RequireText(draft.SchemaVersion, "schema.required", "Schema version is required.", "schemaVersion", errors);
                RequireAddress(draft.OwnerAddress, "owner", errors);
                RequireNonNegative(draft.MainAmount, "mainAmount", errors);
                RequireJson(draft.StateJson, required: true, "stateJson", errors);
                if (draft.TokenId is not null)
                {
                    RequirePositive(draft.TokenAmount, "tokenAmount", errors);
                }
                break;

            case BusinessObjectTransactionMode.TransferObject:
                RequireObjectId(draft.ObjectId, "objectId", errors);
                RequireWorkspaceObject(draft.ObjectId, context, "objectId", errors);
                RequireAddress(draft.ReceiverAddress, "receiver", errors);
                RequireNonNegative(draft.MainAmount, "mainAmount", errors);
                RequireJson(draft.StateJson, required: false, "stateJson", errors);
                break;

            case BusinessObjectTransactionMode.UpdateObject:
            case BusinessObjectTransactionMode.CloseObject:
                RequireObjectId(draft.ObjectId, "objectId", errors);
                RequireWorkspaceObject(draft.ObjectId, context, "objectId", errors);
                RequireJson(draft.StateJson, required: true, "stateJson", errors);
                break;

            case BusinessObjectTransactionMode.CloneObject:
                RequireObjectId(draft.SourceObjectId, "sourceObjectId", errors);
                RequireText(draft.ObjectType, "objectType.required", "Object type is required.", "objectType", errors);
                RequireText(draft.SchemaVersion, "schema.required", "Schema version is required.", "schemaVersion", errors);
                RequireJson(draft.StateJson, required: true, "stateJson", errors);
                break;

            case BusinessObjectTransactionMode.MergeObjects:
                RequireObjectId(draft.SourceObjectId, "sourceObjectId", errors);
                RequireObjectId(draft.SecondObjectId, "secondObjectId", errors);
                RequireWorkspaceObject(draft.SourceObjectId, context, "sourceObjectId", errors);
                RequireWorkspaceObject(draft.SecondObjectId, context, "secondObjectId", errors);
                RequireJson(draft.StateJson, required: true, "stateJson", errors);
                RequireDistinctObjectIds(draft, errors);
                RequireCompatibleWorkspaceObjects(draft, context, errors);
                break;

            case BusinessObjectTransactionMode.SplitBusinessObjectPayment:
                RequireObjectId(draft.ObjectId, "objectId", errors);
                RequireWorkspaceObject(draft.ObjectId, context, "objectId", errors);
                RequireAddress(draft.PaymentReceiverAddress, "primaryReceiver", errors);
                RequirePositive(draft.PaymentAmount, "paymentAmount", errors);
                RequireAddress(draft.ReceiverAddress, "secondaryReceiver", errors);
                RequirePositive(draft.SecondaryPaymentAmount, "secondaryPaymentAmount", errors);
                RequireJson(draft.StateJson, required: false, "stateJson", errors);
                break;

            case BusinessObjectTransactionMode.MainPayment:
                RequireAddress(draft.PaymentReceiverAddress, "receiver", errors);
                RequirePositive(draft.PaymentAmount, "amount", errors);
                if (context.Balance is not null && draft.PaymentAmount.Value > context.Balance.MainBalance.Value)
                {
                    errors.Add(new("amount.balance", "Amount exceeds the available main balance.", "amount"));
                }
                break;

            case BusinessObjectTransactionMode.TokenTransfer:
                if (draft.TokenId is null || string.IsNullOrWhiteSpace(draft.TokenId.Value.Value))
                {
                    errors.Add(new("token.required", "Select a token to send.", "tokenId"));
                }
                RequirePositive(draft.TokenAmount, "tokenAmount", errors);
                RequireAddress(draft.PaymentReceiverAddress, "receiver", errors);
                RequireNonNegative(draft.MainAmount, "mainAmount", errors);
                ValidateTokenBalance(draft, context.Balance, errors);
                break;

            case BusinessObjectTransactionMode.CreateRuleToken:
                errors.Add(new("flow.delegated", "Use the specialized Create token flow so authority and rule checks can be completed."));
                break;

            case BusinessObjectTransactionMode.UtxoMerge:
                ValidateSelectedPlainUtxos(draft, context, minimumCount: 2, "utxo.merge.minimum", errors);
                if (!string.IsNullOrWhiteSpace(draft.PaymentReceiverAddress.Value))
                {
                    RequireAddress(draft.PaymentReceiverAddress, "receiver", errors);
                }
                break;

            case BusinessObjectTransactionMode.UtxoSplit:
                ValidateSelectedPlainUtxos(draft, context, minimumCount: 1, "utxo.split.minimum", errors);
                ValidateSplitOutputs(draft, context, errors);
                break;

            default:
                errors.Add(new("mode.unsupported", "This transaction mode is not supported by the composer."));
                break;
        }

        if (string.IsNullOrWhiteSpace(draft.ExternalRef))
        {
            warnings.Add(new("externalRef.empty", "An external reference is recommended for audit traceability.", "externalRef"));
        }

        return new(errors, warnings);
    }

    private static void ValidateSelectedPlainUtxos(
        BusinessObjectTransactionDraft draft,
        TransactionDraftValidationContext context,
        int minimumCount,
        string minimumCode,
        ICollection<TransactionFlowIssue> errors)
    {
        IReadOnlyList<OutPoint> selected = draft.SelectedUtxosOrEmpty;
        if (selected.Count < minimumCount)
        {
            errors.Add(new(minimumCode, $"Select at least {minimumCount} plain UTXO{(minimumCount == 1 ? string.Empty : "s")}.", "selectedUtxos"));
        }

        if (selected.Distinct().Count() != selected.Count)
        {
            errors.Add(new("utxo.duplicate", "Each UTXO can be selected only once.", "selectedUtxos"));
        }

        if (context.SpendableOutputs is null)
        {
            return;
        }

        IReadOnlyList<LedgerSpendableOutput> available = context.SpendableOutputsOrEmpty
            .Where(output => selected.Contains(output.OutPoint))
            .ToArray();
        if (available.Count != selected.Distinct().Count())
        {
            errors.Add(new("utxo.unavailable", "One or more selected UTXOs are no longer available.", "selectedUtxos"));
        }

        if (available.Any(output => output.Token is not null || output.BusinessObject is not null))
        {
            errors.Add(new("utxo.plain", "Only plain main-currency UTXOs can be used here.", "selectedUtxos"));
        }

        if (available.Count > 0 && available.Sum(output => output.MainAmount.Value) <= 0m)
        {
            errors.Add(new("utxo.total", "The selected UTXO total must be positive.", "selectedUtxos"));
        }
    }

    private static void ValidateSplitOutputs(
        BusinessObjectTransactionDraft draft,
        TransactionDraftValidationContext context,
        ICollection<TransactionFlowIssue> errors)
    {
        IReadOnlyList<UtxoSplitOutput> outputs = draft.UtxoSplitOutputsOrEmpty;
        if (outputs.Count == 0)
        {
            errors.Add(new("utxo.split.outputs", "Add at least one split output.", "splitOutputs"));
            return;
        }

        for (var index = 0; index < outputs.Count; index++)
        {
            RequireAddress(outputs[index].ReceiverAddress, $"output{index + 1}Receiver", errors);
            RequirePositive(outputs[index].Amount, $"output{index + 1}Amount", errors);
        }

        if (context.SpendableOutputs is null)
        {
            return;
        }

        decimal selectedTotal = context.SpendableOutputsOrEmpty
            .Where(output => draft.SelectedUtxosOrEmpty.Contains(output.OutPoint))
            .Sum(output => output.MainAmount.Value);
        decimal outputTotal = outputs.Sum(output => output.Amount.Value);
        if (outputTotal > selectedTotal)
        {
            errors.Add(new("utxo.split.overspend", "Split output total exceeds the selected UTXO amount.", "splitOutputs"));
        }
    }

    private static void ValidateTokenBalance(
        BusinessObjectTransactionDraft draft,
        LedgerAccountBalance? balance,
        ICollection<TransactionFlowIssue> errors)
    {
        if (balance is null || draft.TokenId is null || draft.TokenAmount is null)
        {
            return;
        }

        balance.TokenBalances.TryGetValue(draft.TokenId.Value, out Amount available);
        if (draft.TokenAmount.Value.Value > available.Value)
        {
            errors.Add(new("token.balance", "Token amount exceeds the available balance.", "tokenAmount"));
        }

        if (draft.MainAmount.Value > balance.MainBalance.Value)
        {
            errors.Add(new("main.balance", "Attached main amount exceeds the available balance.", "mainAmount"));
        }
    }

    private static void RequireDistinctObjectIds(
        BusinessObjectTransactionDraft draft,
        ICollection<TransactionFlowIssue> errors)
    {
        string?[] ids = [draft.SourceObjectId?.Value, draft.SecondObjectId?.Value];
        string[] populated = ids.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToArray();
        if (populated.Distinct(StringComparer.Ordinal).Count() != populated.Length)
        {
            errors.Add(new("object.merge.distinct", "Merge sources must be different."));
        }
    }

    private static void RequireWorkspaceObject(
        BusinessObjectId? value,
        TransactionDraftValidationContext context,
        string field,
        ICollection<TransactionFlowIssue> errors)
    {
        if (context.BusinessObjects is null || value is null || string.IsNullOrWhiteSpace(value.Value.Value))
        {
            return;
        }

        if (!context.BusinessObjectsOrEmpty.Any(item => BusinessObjectId.FromOutPoint(item.CurrentOutPoint) == value.Value))
        {
            errors.Add(new("object.unavailable", "Select a business object available in this account.", field));
        }
    }

    private static void RequireCompatibleWorkspaceObjects(
        BusinessObjectTransactionDraft draft,
        TransactionDraftValidationContext context,
        ICollection<TransactionFlowIssue> errors)
    {
        if (context.BusinessObjects is null || draft.SourceObjectId is null || draft.SecondObjectId is null)
        {
            return;
        }

        IBusinessObjectView? first = context.BusinessObjectsOrEmpty.FirstOrDefault(
            item => BusinessObjectId.FromOutPoint(item.CurrentOutPoint) == draft.SourceObjectId.Value);
        IBusinessObjectView? second = context.BusinessObjectsOrEmpty.FirstOrDefault(
            item => BusinessObjectId.FromOutPoint(item.CurrentOutPoint) == draft.SecondObjectId.Value);
        if (first is null || second is null)
        {
            return;
        }

        if (!first.ObjectType.Equals(second.ObjectType, StringComparison.Ordinal)
            || !first.SchemaVersion.Equals(second.SchemaVersion, StringComparison.Ordinal))
        {
            errors.Add(new(
                "object.merge.compatibility",
                "Select two business objects with the same type and schema version before merging.",
                "selectedObjects"));
        }
    }

    private static void RequireObjectId(
        BusinessObjectId? value,
        string field,
        ICollection<TransactionFlowIssue> errors)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Value.Value))
        {
            errors.Add(new("object.required", "Select or enter a business object.", field));
        }
    }

    private static void RequireText(
        string? value,
        string code,
        string message,
        string field,
        ICollection<TransactionFlowIssue> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new(code, message, field));
        }
    }

    private static void RequireAddress(
        Address value,
        string field,
        ICollection<TransactionFlowIssue> errors)
    {
        if (string.IsNullOrWhiteSpace(value.Value))
        {
            errors.Add(new("receiver.required", "A receiver address is required.", field));
        }
        else if (!Address.IsValid(value.Value))
        {
            errors.Add(new("address.invalid", "Enter a valid cdia1 address.", field));
        }
    }

    private static void RequirePositive(
        Amount? value,
        string field,
        ICollection<TransactionFlowIssue> errors)
    {
        if (value is null || value.Value.Value <= 0m)
        {
            errors.Add(new("amount.positive", "Amount must be greater than zero.", field));
        }
    }

    private static void RequireNonNegative(
        Amount value,
        string field,
        ICollection<TransactionFlowIssue> errors)
    {
        if (value.Value < 0m)
        {
            errors.Add(new("amount.nonNegative", "Amount cannot be negative.", field));
        }
    }

    private static void RequireJson(
        string? value,
        bool required,
        string field,
        ICollection<TransactionFlowIssue> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors.Add(new("json.required", "State JSON is required.", field));
            }
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                errors.Add(new("json.object", "State JSON must contain an object at its root.", field));
            }
        }
        catch (JsonException)
        {
            errors.Add(new("json.invalid", "State JSON is not valid JSON.", field));
        }
    }
}
