using System.Globalization;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Services;

public sealed class BusinessObjectCommerceDispatcher
{
    private const int DefaultMaxPaymentsPerRun = 3;
    private readonly Func<DateTimeOffset> utcNow;

    public BusinessObjectCommerceDispatcher(Func<DateTimeOffset>? utcNow = null)
    {
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<BusinessObjectCommerceDispatchResult> DispatchPendingAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        int maxPaymentsPerRun = DefaultMaxPaymentsPerRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(workspace);

        if (maxPaymentsPerRun <= 0)
        {
            return BusinessObjectCommerceDispatchResult.Empty;
        }

        var actions = new List<BusinessObjectCommerceDispatchActionResult>();
        Dictionary<string, int> soldCounters = CreateSoldCounters(workspace);
        int processedPayments = 0;

        foreach (ICommerceBusinessObjectView paymentView in GetPaymentViews(workspace))
        {
            cancellationToken.ThrowIfCancellationRequested();
            BusinessObjectPaymentRecord payment = paymentView.Commerce.Payment!;
            if (!IsSellerPayment(payment, workspace.Address))
            {
                continue;
            }

            if (payment.Status == BusinessObjectCommerceRecordStatus.Dispatched)
            {
                await EnsureDispatchedArtifactsAsync(adapter, workspace, paymentView, payment, actions, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (payment.Status != BusinessObjectCommerceRecordStatus.Pending)
            {
                continue;
            }

            if (processedPayments >= maxPaymentsPerRun)
            {
                break;
            }

            processedPayments++;
            await DispatchPendingPaymentAsync(
                    adapter,
                    workspace,
                    paymentView,
                    payment,
                    soldCounters,
                    actions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new BusinessObjectCommerceDispatchResult(actions);
    }

    private async Task DispatchPendingPaymentAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        ICommerceBusinessObjectView paymentView,
        BusinessObjectPaymentRecord payment,
        Dictionary<string, int> soldCounters,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        if (!Address.TryParse(payment.BuyerAddress, out Address buyerAddress))
        {
            await RejectPaymentAsync(adapter, paymentView, payment, "Buyer address is invalid.", actions, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        BusinessObjectId targetObjectId = new(payment.TargetObjectId);
        BusinessObjectId paymentObjectId = new(payment.PaymentObjectId);
        BusinessObjectId deliveredObjectId = payment.SaleMode == BusinessObjectSaleMode.Copy
            ? BusinessObjectCommercePayloadBuilder.CreateCloneObjectId(targetObjectId, paymentObjectId)
            : targetObjectId;
        LedgerTransactionSummary? existingDelivery = FindDeliveryTransaction(workspace, deliveredObjectId, buyerAddress);
        IBusinessObjectView? target = FindBusinessObject(workspace, targetObjectId);

        if (target is null && existingDelivery is null)
        {
            await RejectPaymentAsync(
                    adapter,
                    paymentView,
                    payment,
                    "Target BusinessObject is not owned by the seller and no delivery transaction was found.",
                    actions,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        BusinessObjectSaleTerms? sale = null;
        if (target is not null)
        {
            BusinessObjectCommercePayloadBuilder.TryGetSaleTerms(target.StateJson, out sale);
            string? validationError = ValidateSale(payment, sale, workspace.Address);
            if (validationError is not null && existingDelivery is null)
            {
                await RejectPaymentAsync(adapter, paymentView, payment, validationError, actions, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        int currentSoldCount = soldCounters.GetValueOrDefault(payment.TargetObjectId, sale?.SoldCount ?? 0);
        if (payment.SaleMode == BusinessObjectSaleMode.Copy
            && existingDelivery is null
            && sale?.MaxClones is int maxClones
            && currentSoldCount >= maxClones)
        {
            await RejectPaymentAsync(
                    adapter,
                    paymentView,
                    payment,
                    $"Copy sale max clone count {maxClones.ToString(CultureInfo.InvariantCulture)} has been reached.",
                    actions,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        TxId dispatchTxId = existingDelivery?.TxId
            ?? await SubmitDeliveryAsync(
                    adapter,
                    target ?? throw new InvalidOperationException("Target BusinessObject is required for new delivery."),
                    payment,
                    buyerAddress,
                    deliveredObjectId,
                    sale,
                    actions,
                    cancellationToken)
                .ConfigureAwait(false);

        if (payment.SaleMode == BusinessObjectSaleMode.Copy && target is not null && sale is not null)
        {
            int nextSoldCount = Math.Max(currentSoldCount + (existingDelivery is null ? 1 : 0), sale.SoldCount);
            soldCounters[payment.TargetObjectId] = nextSoldCount;
            await UpdateCopySaleCounterAsync(adapter, target, sale, nextSoldCount, actions, cancellationToken)
                .ConfigureAwait(false);
        }

        BusinessObjectPaymentRecord completedPayment = CompletePayment(payment, deliveredObjectId, dispatchTxId);
        await EnsureReceiptAsync(adapter, workspace, completedPayment, actions, cancellationToken).ConfigureAwait(false);
        await EnsurePurchaseAsync(adapter, workspace, completedPayment, actions, cancellationToken).ConfigureAwait(false);
        await UpdatePaymentAsync(adapter, paymentView, completedPayment, "payment-dispatched", actions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TxId> SubmitDeliveryAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        IBusinessObjectView target,
        BusinessObjectPaymentRecord payment,
        Address buyerAddress,
        BusinessObjectId deliveredObjectId,
        BusinessObjectSaleTerms? sale,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        BusinessObjectTransactionDraft draft = payment.SaleMode == BusinessObjectSaleMode.Copy
            ? CreateCloneDraft(target, buyerAddress, deliveredObjectId)
            : CreateOriginalTransferDraft(target, buyerAddress, sale);

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(payment.PaymentObjectId, payment.SaleMode == BusinessObjectSaleMode.Copy ? "clone-dispatch" : "original-dispatch", result));
        EnsureAccepted(result, "BusinessObject delivery");
        return result.TxId ?? throw new InvalidOperationException("Accepted dispatch did not return a transaction id.");
    }

    private static BusinessObjectTransactionDraft CreateCloneDraft(
        IBusinessObjectView target,
        Address buyerAddress,
        BusinessObjectId deliveredObjectId)
    {
        string cloneStateJson = BusinessObjectCommercePayloadBuilder.ClearSaleTermsStateJson(
            target.StateJson,
            target.ObjectId.Value);
        return new BusinessObjectTransactionDraft(
            deliveredObjectId,
            target.ObjectType,
            target.SchemaVersion,
            buyerAddress,
            buyerAddress,
            buyerAddress,
            Amount.Zero,
            Amount.Zero,
            cloneStateJson,
            $"bo-commerce://clone/{deliveredObjectId.Value}",
            BusinessObjectTransactionMode.CloneObject,
            SourceObjectId: target.ObjectId,
            TargetObjectId: deliveredObjectId);
    }

    private static BusinessObjectTransactionDraft CreateOriginalTransferDraft(
        IBusinessObjectView target,
        Address buyerAddress,
        BusinessObjectSaleTerms? sale)
    {
        string stateJson = target.StateJson ?? "{}";
        if (sale is not null)
        {
            BusinessObjectSaleTerms soldTerms = sale with
            {
                Status = BusinessObjectSaleStatus.Sold,
                SoldCount = Math.Max(sale.SoldCount, 1)
            };
            stateJson = BusinessObjectCommercePayloadBuilder.WithSaleTermsStateJson(
                target.StateJson,
                target.ObjectId.Value,
                soldTerms);
        }

        return new BusinessObjectTransactionDraft(
            target.ObjectId,
            target.ObjectType,
            target.SchemaVersion,
            buyerAddress,
            buyerAddress,
            buyerAddress,
            Amount.Zero,
            Amount.Zero,
            stateJson,
            $"bo-commerce://transfer/{target.ObjectId.Value}",
            BusinessObjectTransactionMode.TransferObject,
            SourceObjectId: target.ObjectId);
    }

    private async Task UpdateCopySaleCounterAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        IBusinessObjectView target,
        BusinessObjectSaleTerms sale,
        int nextSoldCount,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        BusinessObjectSaleStatus nextStatus = sale.MaxClones is int maxClones && nextSoldCount >= maxClones
            ? BusinessObjectSaleStatus.Sold
            : BusinessObjectSaleStatus.Listed;
        if (sale.SoldCount == nextSoldCount && sale.Status == nextStatus)
        {
            return;
        }

        BusinessObjectSaleTerms nextSale = sale with
        {
            SoldCount = nextSoldCount,
            Status = nextStatus
        };
        string stateJson = BusinessObjectCommercePayloadBuilder.WithSaleTermsStateJson(
            target.StateJson,
            target.ObjectId.Value,
            nextSale);
        BusinessObjectTransactionDraft draft = CreateUpdateDraft(
            target.ObjectId,
            target.ObjectType,
            target.SchemaVersion,
            target.OwnerAddress,
            stateJson,
            $"bo-commerce://sale/{target.ObjectId.Value}");

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(target.ObjectId.Value, "sale-counter-update", result));
        EnsureAccepted(result, "Copy sale counter update");
    }

    private async Task EnsureDispatchedArtifactsAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        ICommerceBusinessObjectView paymentView,
        BusinessObjectPaymentRecord payment,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payment.DeliveredObjectId) || string.IsNullOrWhiteSpace(payment.DispatchTxId))
        {
            return;
        }

        await EnsureReceiptAsync(adapter, workspace, payment, actions, cancellationToken).ConfigureAwait(false);
        await EnsurePurchaseAsync(adapter, workspace, payment, actions, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(payment.ReceiptObjectId) || string.IsNullOrWhiteSpace(payment.PurchaseObjectId))
        {
            BusinessObjectPaymentRecord normalized = payment with
            {
                ReceiptObjectId = payment.ReceiptObjectId ?? BusinessObjectCommercePayloadBuilder.CreateReceiptObjectId(new BusinessObjectId(payment.PaymentObjectId)).Value,
                PurchaseObjectId = payment.PurchaseObjectId ?? BusinessObjectCommercePayloadBuilder.CreatePurchaseObjectId(new BusinessObjectId(payment.PaymentObjectId)).Value,
                UpdatedAtUtc = utcNow()
            };
            await UpdatePaymentAsync(adapter, paymentView, normalized, "payment-link-normalize", actions, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task EnsureReceiptAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        BusinessObjectPaymentRecord payment,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payment.DeliveredObjectId) || string.IsNullOrWhiteSpace(payment.DispatchTxId))
        {
            return;
        }

        BusinessObjectId paymentObjectId = new(payment.PaymentObjectId);
        BusinessObjectId receiptObjectId = string.IsNullOrWhiteSpace(payment.ReceiptObjectId)
            ? BusinessObjectCommercePayloadBuilder.CreateReceiptObjectId(paymentObjectId)
            : new BusinessObjectId(payment.ReceiptObjectId);
        if (HasObjectOrTransactionOutput(workspace, receiptObjectId))
        {
            return;
        }

        BusinessObjectReceiptRecord receipt = BusinessObjectCommercePayloadBuilder.CreateReceiptRecord(
            receiptObjectId,
            payment with { ReceiptObjectId = receiptObjectId.Value },
            new BusinessObjectId(payment.DeliveredObjectId),
            new TxId(payment.DispatchTxId),
            utcNow());
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreateReceiptPayload(receipt));
        BusinessObjectTransactionDraft draft = CreateCreateDraft(
            receiptObjectId,
            "receipt",
            new Address(payment.SellerAddress),
            stateJson,
            $"bo-commerce://receipt/{receiptObjectId.Value}");

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(payment.PaymentObjectId, "receipt-create", result));
        EnsureAccepted(result, "Receipt create");
    }

    private async Task EnsurePurchaseAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        BusinessObjectPaymentRecord payment,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        BusinessObjectId paymentObjectId = new(payment.PaymentObjectId);
        BusinessObjectId purchaseObjectId = string.IsNullOrWhiteSpace(payment.PurchaseObjectId)
            ? BusinessObjectCommercePayloadBuilder.CreatePurchaseObjectId(paymentObjectId)
            : new BusinessObjectId(payment.PurchaseObjectId);
        if (HasObjectOrTransactionOutput(workspace, purchaseObjectId))
        {
            return;
        }

        BusinessObjectPurchaseRecord purchase = BusinessObjectCommercePayloadBuilder.CreatePurchaseRecord(
            purchaseObjectId,
            payment with { PurchaseObjectId = purchaseObjectId.Value },
            utcNow());
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePurchasePayload(purchase));
        BusinessObjectTransactionDraft draft = CreateCreateDraft(
            purchaseObjectId,
            "purchase",
            new Address(payment.BuyerAddress),
            stateJson,
            $"bo-commerce://purchase/{purchaseObjectId.Value}");

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(payment.PaymentObjectId, "purchase-create", result));
        EnsureAccepted(result, "Purchase create");
    }

    private async Task RejectPaymentAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        ICommerceBusinessObjectView paymentView,
        BusinessObjectPaymentRecord payment,
        string reason,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        BusinessObjectPaymentRecord rejected = payment with
        {
            Status = BusinessObjectCommerceRecordStatus.Rejected,
            RejectionReason = reason,
            UpdatedAtUtc = utcNow()
        };
        await UpdatePaymentAsync(adapter, paymentView, rejected, "payment-rejected", actions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpdatePaymentAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        ICommerceBusinessObjectView paymentView,
        BusinessObjectPaymentRecord payment,
        string action,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment));
        BusinessObjectTransactionDraft draft = CreateUpdateDraft(
            paymentView.ObjectId,
            paymentView.ObjectType,
            paymentView.SchemaVersion,
            paymentView.OwnerAddress,
            stateJson,
            $"bo-commerce://payment/{payment.PaymentObjectId}");

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(payment.PaymentObjectId, action, result));
        EnsureAccepted(result, "Payment update");
    }

    private BusinessObjectPaymentRecord CompletePayment(
        BusinessObjectPaymentRecord payment,
        BusinessObjectId deliveredObjectId,
        TxId dispatchTxId)
    {
        BusinessObjectId paymentObjectId = new(payment.PaymentObjectId);
        string receiptObjectId = payment.ReceiptObjectId
            ?? BusinessObjectCommercePayloadBuilder.CreateReceiptObjectId(paymentObjectId).Value;
        string purchaseObjectId = payment.PurchaseObjectId
            ?? BusinessObjectCommercePayloadBuilder.CreatePurchaseObjectId(paymentObjectId).Value;
        return payment with
        {
            Status = BusinessObjectCommerceRecordStatus.Dispatched,
            DispatchTxId = dispatchTxId.Value,
            DeliveredObjectId = deliveredObjectId.Value,
            ReceiptObjectId = receiptObjectId,
            PurchaseObjectId = purchaseObjectId,
            UpdatedAtUtc = utcNow()
        };
    }

    private static string? ValidateSale(
        BusinessObjectPaymentRecord payment,
        BusinessObjectSaleTerms? sale,
        Address sellerAddress)
    {
        if (sale is null)
        {
            return "Target BusinessObject is not listed for sale.";
        }

        BusinessObjectCommerceValidationResult validation = BusinessObjectCommerceValidator.ValidateSaleTerms(sale);
        if (!validation.IsValid)
        {
            return string.Join("; ", validation.Errors);
        }

        if (sale.Status != BusinessObjectSaleStatus.Listed)
        {
            return $"Target BusinessObject sale status is {sale.Status}.";
        }

        if (!string.Equals(sale.SellerAddress, sellerAddress.Value, StringComparison.Ordinal))
        {
            return "Current account is not the seller for this BusinessObject.";
        }

        if (!string.Equals(payment.SellerAddress, sellerAddress.Value, StringComparison.Ordinal))
        {
            return "Payment seller does not match current account.";
        }

        if (sale.Mode != payment.SaleMode)
        {
            return "Payment sale mode does not match current sale terms.";
        }

        if (sale.PriceAmount != payment.PriceAmount)
        {
            return "Payment amount does not match current sale terms.";
        }

        if (!string.Equals(sale.TokenId ?? string.Empty, payment.TokenId ?? string.Empty, StringComparison.Ordinal))
        {
            return "Payment token does not match current sale terms.";
        }

        return null;
    }

    private static BusinessObjectTransactionDraft CreateUpdateDraft(
        BusinessObjectId objectId,
        string objectType,
        string schemaVersion,
        Address ownerAddress,
        string stateJson,
        string externalRef)
        => new(
            objectId,
            objectType,
            schemaVersion,
            ownerAddress,
            ownerAddress,
            ownerAddress,
            Amount.Zero,
            Amount.Zero,
            stateJson,
            externalRef,
            BusinessObjectTransactionMode.UpdateObject,
            SourceObjectId: objectId);

    private static BusinessObjectTransactionDraft CreateCreateDraft(
        BusinessObjectId objectId,
        string objectType,
        Address ownerAddress,
        string stateJson,
        string externalRef)
        => new(
            objectId,
            objectType,
            "1.0",
            ownerAddress,
            ownerAddress,
            ownerAddress,
            Amount.Zero,
            Amount.Zero,
            stateJson,
            externalRef,
            BusinessObjectTransactionMode.Create);

    private static IReadOnlyList<ICommerceBusinessObjectView> GetPaymentViews(BusinessObjectWorkspaceModel workspace)
        => workspace.BusinessObjects
            .OfType<ICommerceBusinessObjectView>()
            .Where(view => view.Commerce.Payment is not null)
            .OrderBy(view => view.CreatedAt)
            .ThenBy(view => view.ObjectId.Value, StringComparer.Ordinal)
            .ToArray();

    private static IBusinessObjectView? FindBusinessObject(BusinessObjectWorkspaceModel workspace, BusinessObjectId objectId)
        => workspace.BusinessObjects.FirstOrDefault(item => item.ObjectId == objectId);

    private static bool IsSellerPayment(BusinessObjectPaymentRecord payment, Address sellerAddress)
        => string.Equals(payment.SellerAddress, sellerAddress.Value, StringComparison.Ordinal);

    private static Dictionary<string, int> CreateSoldCounters(BusinessObjectWorkspaceModel workspace)
    {
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (IBusinessObjectView item in workspace.BusinessObjects)
        {
            if (BusinessObjectCommercePayloadBuilder.TryGetSaleTerms(item.StateJson, out BusinessObjectSaleTerms? sale)
                && sale is not null)
            {
                counters[item.ObjectId.Value] = Math.Max(counters.GetValueOrDefault(item.ObjectId.Value), sale.SoldCount);
            }
        }

        foreach (ICommerceBusinessObjectView paymentView in GetPaymentViews(workspace))
        {
            BusinessObjectPaymentRecord payment = paymentView.Commerce.Payment!;
            if (payment.Status == BusinessObjectCommerceRecordStatus.Dispatched)
            {
                counters[payment.TargetObjectId] = Math.Max(
                    counters.GetValueOrDefault(payment.TargetObjectId),
                    CountDispatchedPayments(workspace, payment.TargetObjectId));
            }
        }

        return counters;
    }

    private static int CountDispatchedPayments(BusinessObjectWorkspaceModel workspace, string targetObjectId)
        => GetPaymentViews(workspace).Count(view =>
            view.Commerce.Payment is { Status: BusinessObjectCommerceRecordStatus.Dispatched } payment
            && string.Equals(payment.TargetObjectId, targetObjectId, StringComparison.Ordinal));

    private static LedgerTransactionSummary? FindDeliveryTransaction(
        BusinessObjectWorkspaceModel workspace,
        BusinessObjectId deliveredObjectId,
        Address buyerAddress)
        => workspace.Transactions
            .OrderByDescending(transaction => transaction.Timestamp)
            .FirstOrDefault(transaction => transaction.Outputs.Any(output =>
                output.Address == buyerAddress
                && output.BusinessObject?.ObjectId == deliveredObjectId));

    private static bool HasObjectOrTransactionOutput(BusinessObjectWorkspaceModel workspace, BusinessObjectId objectId)
        => workspace.BusinessObjects.Any(item => item.ObjectId == objectId)
            || workspace.Transactions.Any(transaction => transaction.Outputs.Any(output =>
                output.BusinessObject?.ObjectId == objectId));

    private static BusinessObjectCommerceDispatchActionResult FromExecution(
        string paymentObjectId,
        string action,
        EconomyTransactionExecutionResult result)
        => new(
            paymentObjectId,
            action,
            Submitted: true,
            result.Accepted,
            result.Message,
            result.TxId);

    private static void EnsureAccepted(EconomyTransactionExecutionResult result, string subject)
    {
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"{subject} was rejected: {result.Message}");
        }
    }
}

public sealed record BusinessObjectCommerceDispatchResult(
    IReadOnlyList<BusinessObjectCommerceDispatchActionResult> Actions)
{
    public static BusinessObjectCommerceDispatchResult Empty { get; } = new(Array.Empty<BusinessObjectCommerceDispatchActionResult>());

    public bool HasSubmissions => Actions.Any(action => action.Submitted);

    public int SubmittedCount => Actions.Count(action => action.Submitted);

    public int AcceptedCount => Actions.Count(action => action.Submitted && action.Accepted);

    public string Summary
        => SubmittedCount == 0
            ? "No pending commerce dispatch work."
            : $"{AcceptedCount.ToString(CultureInfo.InvariantCulture)} commerce dispatch action(s) accepted.";
}

public sealed record BusinessObjectCommerceDispatchActionResult(
    string PaymentObjectId,
    string Action,
    bool Submitted,
    bool Accepted,
    string Message,
    TxId? TxId);
