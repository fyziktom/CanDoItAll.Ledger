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
        DeliveryTransactionMatch? existingDelivery = FindDeliveryTransactionByPayment(workspace, payment, buyerAddress);
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

        TxId dispatchTxId = existingDelivery?.Transaction.TxId
            ?? await SubmitDeliveryAsync(
                    adapter,
                    workspace,
                    target ?? throw new InvalidOperationException("Target BusinessObject is required for new delivery."),
                    payment,
                    buyerAddress,
                    sale,
                    actions,
                    cancellationToken)
                .ConfigureAwait(false);
        BusinessObjectId deliveredObjectId = BusinessObjectId.FromOutPoint(
            dispatchTxId,
            existingDelivery?.OutputIndex ?? 0);

        if (payment.SaleMode == BusinessObjectSaleMode.Copy && target is not null && sale is not null)
        {
            int nextSoldCount = Math.Max(currentSoldCount + (existingDelivery is null ? 1 : 0), sale.SoldCount);
            soldCounters[payment.TargetObjectId] = nextSoldCount;
            await UpdateCopySaleCounterAsync(adapter, target, sale, nextSoldCount, actions, cancellationToken)
                .ConfigureAwait(false);
        }

        BusinessObjectPaymentRecord completedPayment = CompletePayment(payment, deliveredObjectId, dispatchTxId);
        BusinessObjectId? receiptObjectId = await EnsureReceiptAsync(
            adapter,
            workspace,
            completedPayment,
            trustRecordedLink: false,
            actions,
            cancellationToken).ConfigureAwait(false);
        BusinessObjectId? purchaseObjectId = await EnsurePurchaseAsync(
            adapter,
            workspace,
            completedPayment,
            trustRecordedLink: false,
            actions,
            cancellationToken).ConfigureAwait(false);
        completedPayment = completedPayment with
        {
            ReceiptObjectId = receiptObjectId?.Value,
            PurchaseObjectId = purchaseObjectId?.Value
        };
        await UpdatePaymentAsync(adapter, paymentView, completedPayment, "payment-dispatched", actions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TxId> SubmitDeliveryAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        IBusinessObjectView target,
        BusinessObjectPaymentRecord payment,
        Address buyerAddress,
        BusinessObjectSaleTerms? sale,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        BusinessObjectTransactionDraft draft = payment.SaleMode == BusinessObjectSaleMode.Copy
            ? CreateCloneDraft(target, buyerAddress, payment.PaymentObjectId, workspace.TokenOptions)
            : CreateOriginalTransferDraft(target, buyerAddress, sale, payment.PaymentObjectId);

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(payment.PaymentObjectId, payment.SaleMode == BusinessObjectSaleMode.Copy ? "clone-dispatch" : "original-dispatch", result));
        EnsureAccepted(result, "BusinessObject delivery");
        return result.TxId ?? throw new InvalidOperationException("Accepted dispatch did not return a transaction id.");
    }

    private static BusinessObjectTransactionDraft CreateCloneDraft(
        IBusinessObjectView target,
        Address buyerAddress,
        string paymentObjectId,
        IReadOnlyList<EconomyTokenOption> tokenOptions)
    {
        string cloneStateJson = BusinessObjectCommercePayloadBuilder.ClearSaleTermsStateJson(
            target.StateJson,
            DisplayFallbackName(target));
        var draft = new BusinessObjectTransactionDraft(
            BusinessObjectId.FromOutPoint(target.CurrentOutPoint),
            target.ObjectType,
            target.SchemaVersion,
            buyerAddress,
            buyerAddress,
            buyerAddress,
            Amount.Zero,
            Amount.Zero,
            cloneStateJson,
            $"bo-commerce://clone/payment/{paymentObjectId}",
            BusinessObjectTransactionMode.CloneObject,
            SourceObjectId: BusinessObjectId.FromOutPoint(target.CurrentOutPoint));
        return BusinessObjectCarrierTokenPolicy.WithDefaultCarrierToken(draft, tokenOptions, allowBalanceFallback: true);
    }

    private static BusinessObjectTransactionDraft CreateOriginalTransferDraft(
        IBusinessObjectView target,
        Address buyerAddress,
        BusinessObjectSaleTerms? sale,
        string paymentObjectId)
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
                DisplayFallbackName(target),
                soldTerms);
        }

        BusinessObjectId sourceObjectId = BusinessObjectId.FromOutPoint(target.CurrentOutPoint);
        return new BusinessObjectTransactionDraft(
            sourceObjectId,
            target.ObjectType,
            target.SchemaVersion,
            buyerAddress,
            buyerAddress,
            buyerAddress,
            Amount.Zero,
            Amount.Zero,
            stateJson,
            $"bo-commerce://transfer/payment/{paymentObjectId}",
            BusinessObjectTransactionMode.TransferObject,
            SourceObjectId: sourceObjectId);
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
            DisplayFallbackName(target),
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

        BusinessObjectId? receiptObjectId = await EnsureReceiptAsync(
            adapter,
            workspace,
            payment,
            trustRecordedLink: true,
            actions,
            cancellationToken).ConfigureAwait(false);
        BusinessObjectId? purchaseObjectId = await EnsurePurchaseAsync(
            adapter,
            workspace,
            payment,
            trustRecordedLink: true,
            actions,
            cancellationToken).ConfigureAwait(false);

        if (!string.Equals(payment.ReceiptObjectId, receiptObjectId?.Value, StringComparison.Ordinal)
            || !string.Equals(payment.PurchaseObjectId, purchaseObjectId?.Value, StringComparison.Ordinal))
        {
            BusinessObjectPaymentRecord normalized = payment with
            {
                ReceiptObjectId = receiptObjectId?.Value,
                PurchaseObjectId = purchaseObjectId?.Value,
                UpdatedAtUtc = utcNow()
            };
            await UpdatePaymentAsync(adapter, paymentView, normalized, "payment-link-normalize", actions, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<BusinessObjectId?> EnsureReceiptAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        BusinessObjectPaymentRecord payment,
        bool trustRecordedLink,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payment.DeliveredObjectId) || string.IsNullOrWhiteSpace(payment.DispatchTxId))
        {
            return null;
        }

        BusinessObjectId? linkedReceiptId = trustRecordedLink
            ? FindVerifiedLinkedArtifact(workspace, payment.ReceiptObjectId)
            : null;
        if (linkedReceiptId is not null)
        {
            return linkedReceiptId;
        }

        ICommerceBusinessObjectView? existing = workspace.BusinessObjects
            .OfType<ICommerceBusinessObjectView>()
            .FirstOrDefault(view => string.Equals(
                view.Commerce.Receipt?.PaymentObjectId,
                payment.PaymentObjectId,
                StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing.ObjectId;
        }

        BusinessObjectId? confirmedReceiptId = FindConfirmedArtifactByPayment(
            workspace,
            "receipt",
            payment.PaymentObjectId,
            new Address(payment.SellerAddress));
        if (confirmedReceiptId is not null)
        {
            return confirmedReceiptId;
        }

        BusinessObjectReceiptRecord receipt = BusinessObjectCommercePayloadBuilder.CreateReceiptRecord(
            payment with { ReceiptObjectId = null },
            new BusinessObjectId(payment.DeliveredObjectId),
            new TxId(payment.DispatchTxId),
            utcNow());
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreateReceiptPayload(receipt));
        BusinessObjectTransactionDraft draft = CreateCreateDraft(
            "receipt",
            new Address(payment.SellerAddress),
            stateJson,
            $"bo-commerce://receipt/payment/{payment.PaymentObjectId}",
            workspace.TokenOptions);

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(payment.PaymentObjectId, "receipt-create", result));
        EnsureAccepted(result, "Receipt create");
        return result.ResultBusinessObjectId
            ?? throw new InvalidOperationException("Accepted receipt create did not return its output-derived BusinessObject id.");
    }

    private async Task<BusinessObjectId?> EnsurePurchaseAsync(
        IEconomyAccountWorkspaceAdapter adapter,
        BusinessObjectWorkspaceModel workspace,
        BusinessObjectPaymentRecord payment,
        bool trustRecordedLink,
        List<BusinessObjectCommerceDispatchActionResult> actions,
        CancellationToken cancellationToken)
    {
        BusinessObjectId? linkedPurchaseId = trustRecordedLink
            ? FindVerifiedLinkedArtifact(workspace, payment.PurchaseObjectId)
            : null;
        if (linkedPurchaseId is not null)
        {
            return linkedPurchaseId;
        }

        ICommerceBusinessObjectView? existing = workspace.BusinessObjects
            .OfType<ICommerceBusinessObjectView>()
            .FirstOrDefault(view => string.Equals(
                view.Commerce.Purchase?.PaymentObjectId,
                payment.PaymentObjectId,
                StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing.ObjectId;
        }

        BusinessObjectId? confirmedPurchaseId = FindConfirmedArtifactByPayment(
            workspace,
            "purchase",
            payment.PaymentObjectId,
            new Address(payment.BuyerAddress));
        if (confirmedPurchaseId is not null)
        {
            return confirmedPurchaseId;
        }

        BusinessObjectPurchaseRecord purchase = BusinessObjectCommercePayloadBuilder.CreatePurchaseRecord(
            payment with { PurchaseObjectId = null },
            utcNow());
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePurchasePayload(purchase));
        BusinessObjectTransactionDraft draft = CreateCreateDraft(
            "purchase",
            new Address(payment.BuyerAddress),
            stateJson,
            $"bo-commerce://purchase/payment/{payment.PaymentObjectId}",
            workspace.TokenOptions);

        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(draft, cancellationToken)
            .ConfigureAwait(false);
        actions.Add(FromExecution(payment.PaymentObjectId, "purchase-create", result));
        EnsureAccepted(result, "Purchase create");
        return result.ResultBusinessObjectId
            ?? throw new InvalidOperationException("Accepted purchase create did not return its output-derived BusinessObject id.");
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
        return payment with
        {
            Status = BusinessObjectCommerceRecordStatus.Dispatched,
            PaymentTxId = payment.PaymentTxId ?? new BusinessObjectId(payment.PaymentObjectId).RequireOutPoint().TxId.Value,
            DispatchTxId = dispatchTxId.Value,
            DeliveredObjectId = deliveredObjectId.Value,
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
        string objectType,
        Address ownerAddress,
        string stateJson,
        string externalRef,
        IReadOnlyList<EconomyTokenOption> tokenOptions)
    {
        var draft = new BusinessObjectTransactionDraft(
            default,
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
        return BusinessObjectCarrierTokenPolicy.WithDefaultCarrierToken(draft, tokenOptions, allowBalanceFallback: true);
    }

    private static IReadOnlyList<ICommerceBusinessObjectView> GetPaymentViews(BusinessObjectWorkspaceModel workspace)
        => workspace.BusinessObjects
            .OfType<ICommerceBusinessObjectView>()
            .Where(view => view.Commerce.Payment is not null)
            .OrderBy(view => view.CreatedAt)
            .ThenBy(view => view.ObjectId.Value, StringComparer.Ordinal)
            .ToArray();

    private static IBusinessObjectView? FindBusinessObject(BusinessObjectWorkspaceModel workspace, BusinessObjectId objectId)
        => workspace.BusinessObjects.FirstOrDefault(item => item.ObjectId == objectId);

    private static string DisplayFallbackName(IBusinessObjectView businessObject)
        => businessObject is IMediaBusinessObjectView media && !string.IsNullOrWhiteSpace(media.Name)
            ? media.Name
            : "Untitled BusinessObject";

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

    private static DeliveryTransactionMatch? FindDeliveryTransactionByPayment(
        BusinessObjectWorkspaceModel workspace,
        BusinessObjectPaymentRecord payment,
        Address buyerAddress)
    {
        string expectedExternalRef = payment.SaleMode == BusinessObjectSaleMode.Copy
            ? $"bo-commerce://clone/payment/{payment.PaymentObjectId}"
            : $"bo-commerce://transfer/payment/{payment.PaymentObjectId}";

        foreach (LedgerTransactionSummary transaction in LedgerTransactionOrdering.NewestFirst(workspace.Transactions))
        {
            if (!string.Equals(transaction.Data?.ExternalRef, expectedExternalRef, StringComparison.Ordinal))
            {
                continue;
            }

            LedgerTransactionOutputSummary? output = transaction.Outputs
                .OrderBy(candidate => candidate.OutputIndex)
                .FirstOrDefault(candidate => candidate.Address == buyerAddress && candidate.HasBusinessObject);
            if (output is not null)
            {
                return new DeliveryTransactionMatch(transaction, output.OutputIndex);
            }
        }

        return null;
    }

    private static BusinessObjectId? FindVerifiedLinkedArtifact(
        BusinessObjectWorkspaceModel workspace,
        string? linkedObjectId)
    {
        if (string.IsNullOrWhiteSpace(linkedObjectId))
        {
            return null;
        }

        var objectId = new BusinessObjectId(linkedObjectId);
        if (!objectId.IsCanonicalOutPointId())
        {
            return null;
        }

        bool exists = workspace.BusinessObjects.Any(item => item.ObjectId == objectId)
            || workspace.Transactions.Any(transaction => transaction.Outputs.Any(output =>
                output.HasBusinessObject
                && BusinessObjectId.FromOutPoint(transaction.TxId, output.OutputIndex) == objectId));
        return exists ? objectId : null;
    }

    private static BusinessObjectId? FindConfirmedArtifactByPayment(
        BusinessObjectWorkspaceModel workspace,
        string objectType,
        string paymentObjectId,
        Address ownerAddress)
    {
        string expectedExternalRef = $"bo-commerce://{objectType}/payment/{paymentObjectId}";
        foreach (LedgerTransactionSummary transaction in LedgerTransactionOrdering.NewestFirst(workspace.Transactions))
        {
            if (!string.Equals(transaction.Data?.ExternalRef, expectedExternalRef, StringComparison.Ordinal)
                || !BusinessObjectPayloadEnvelopeReader.TryRead(
                    transaction.Data?.InlineJson,
                    out BusinessObjectPayloadEnvelope envelope)
                || !string.Equals(envelope.ObjectType, objectType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LedgerTransactionOutputSummary? output = transaction.Outputs
                .OrderBy(candidate => candidate.OutputIndex)
                .FirstOrDefault(candidate => candidate.Address == ownerAddress && candidate.HasBusinessObject);
            if (output is not null)
            {
                return BusinessObjectId.FromOutPoint(transaction.TxId, output.OutputIndex);
            }
        }

        return null;
    }

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

    private sealed record DeliveryTransactionMatch(
        LedgerTransactionSummary Transaction,
        int OutputIndex);
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
