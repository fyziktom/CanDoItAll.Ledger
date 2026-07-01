using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectCommerceDispatcherTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("commerce-dispatch"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-dispatch-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-dispatch-public"));
    private static readonly BusinessObjectId TargetObjectId = new("bo:post:dispatch-target");
    private static readonly BusinessObjectId ImageTargetObjectId = new("bo:image:dispatch-target");
    private static readonly BusinessObjectId PaymentObjectId = new("bo:payment:dispatch-payment");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-30T12:00:00Z");

    [Fact]
    public async Task CopySale_DispatchesDeterministicCloneAndLinkedRecords()
    {
        ITypedBusinessObjectView target = CreateListedTarget(BusinessObjectSaleMode.Copy, maxClones: 2);
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync(BusinessObjectSaleMode.Copy);
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(target, payment);
        var adapter = new CapturingWorkspaceAdapter(workspace);
        var dispatcher = new BusinessObjectCommerceDispatcher(() => Now.AddMinutes(5));
        BusinessObjectId expectedCloneId = BusinessObjectCommercePayloadBuilder.CreateCloneObjectId(TargetObjectId, PaymentObjectId);

        BusinessObjectCommerceDispatchResult result = await dispatcher.DispatchPendingAsync(adapter, workspace);

        Assert.Equal(5, result.AcceptedCount);
        BusinessObjectTransactionDraft cloneDraft = Assert.Single(adapter.SubmittedDrafts, draft => draft.SelectedMode == BusinessObjectTransactionMode.CloneObject);
        Assert.Equal(TargetObjectId, cloneDraft.SourceObjectId);
        Assert.Equal(expectedCloneId, cloneDraft.TargetObjectId);
        Assert.Equal(Bob, cloneDraft.OwnerAddress);
        Assert.Null(BusinessObjectPayloadBuilder.ParseMediaPayload(cloneDraft.StateJson, "clone").Commerce?.Sale);

        BusinessObjectTransactionDraft saleUpdate = Assert.Single(adapter.SubmittedDrafts, draft => draft.ExternalRef.StartsWith("bo-commerce://sale/", StringComparison.Ordinal));
        BusinessObjectCommercePayloadBuilder.TryGetSaleTerms(saleUpdate.StateJson, out BusinessObjectSaleTerms? updatedSale);
        Assert.NotNull(updatedSale);
        Assert.Equal(1, updatedSale!.SoldCount);
        Assert.Equal(BusinessObjectSaleStatus.Listed, updatedSale.Status);

        BusinessObjectTransactionDraft paymentUpdate = Assert.Single(adapter.SubmittedDrafts, draft => draft.ExternalRef.StartsWith("bo-commerce://payment/", StringComparison.Ordinal));
        BusinessObjectPaymentRecord completedPayment = BusinessObjectPayloadBuilder
            .ParseMediaPayload(paymentUpdate.StateJson, "payment")
            .Commerce!
            .Payment!;
        Assert.Equal(BusinessObjectCommerceRecordStatus.Dispatched, completedPayment.Status);
        Assert.Equal(expectedCloneId.Value, completedPayment.DeliveredObjectId);
        Assert.NotNull(completedPayment.DispatchTxId);
        Assert.NotNull(completedPayment.ReceiptObjectId);
        Assert.NotNull(completedPayment.PurchaseObjectId);

        Assert.Contains(adapter.SubmittedDrafts, draft => draft.ObjectType == "receipt" && draft.OwnerAddress == Alice);
        Assert.Contains(adapter.SubmittedDrafts, draft => draft.ObjectType == "purchase" && draft.OwnerAddress == Bob);
    }

    [Fact]
    public async Task DispatchedPaymentWithExistingArtifacts_DoesNotSubmitDuplicateTransactions()
    {
        BusinessObjectId cloneId = BusinessObjectCommercePayloadBuilder.CreateCloneObjectId(TargetObjectId, PaymentObjectId);
        TxId dispatchTxId = new("tx-commerce-dispatch-existing");
        BusinessObjectPaymentRecord completed = CreatePaymentRecord(BusinessObjectSaleMode.Copy) with
        {
            Status = BusinessObjectCommerceRecordStatus.Dispatched,
            DispatchTxId = dispatchTxId.Value,
            DeliveredObjectId = cloneId.Value,
            ReceiptObjectId = BusinessObjectCommercePayloadBuilder.CreateReceiptObjectId(PaymentObjectId).Value,
            PurchaseObjectId = BusinessObjectCommercePayloadBuilder.CreatePurchaseObjectId(PaymentObjectId).Value
        };
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync(completed);
        ICommerceBusinessObjectView receipt = await CreateReceiptViewAsync(completed);
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(
            [payment, receipt],
            [CreateObjectOutputTransaction(completed.PurchaseObjectId!, Bob)]);
        var adapter = new CapturingWorkspaceAdapter(workspace);
        var dispatcher = new BusinessObjectCommerceDispatcher(() => Now.AddMinutes(5));

        BusinessObjectCommerceDispatchResult result = await dispatcher.DispatchPendingAsync(adapter, workspace);

        Assert.Equal(0, result.SubmittedCount);
        Assert.Empty(adapter.SubmittedDrafts);
    }

    [Fact]
    public async Task CopySaleAtMaxCloneCount_RejectsPaymentWithoutClone()
    {
        ITypedBusinessObjectView target = CreateListedTarget(BusinessObjectSaleMode.Copy, maxClones: 1, soldCount: 1);
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync(BusinessObjectSaleMode.Copy);
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(target, payment);
        var adapter = new CapturingWorkspaceAdapter(workspace);
        var dispatcher = new BusinessObjectCommerceDispatcher(() => Now.AddMinutes(5));

        BusinessObjectCommerceDispatchResult result = await dispatcher.DispatchPendingAsync(adapter, workspace);

        Assert.Equal(1, result.AcceptedCount);
        Assert.DoesNotContain(adapter.SubmittedDrafts, draft => draft.SelectedMode == BusinessObjectTransactionMode.CloneObject);
        BusinessObjectTransactionDraft paymentUpdate = Assert.Single(adapter.SubmittedDrafts);
        BusinessObjectPaymentRecord rejected = BusinessObjectPayloadBuilder
            .ParseMediaPayload(paymentUpdate.StateJson, "payment")
            .Commerce!
            .Payment!;
        Assert.Equal(BusinessObjectCommerceRecordStatus.Rejected, rejected.Status);
        Assert.Contains("max clone", rejected.RejectionReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OriginalSale_DispatchesTransferAndMarksTargetSold()
    {
        ITypedBusinessObjectView target = CreateListedTarget(BusinessObjectSaleMode.Original);
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync(BusinessObjectSaleMode.Original);
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(target, payment);
        var adapter = new CapturingWorkspaceAdapter(workspace);
        var dispatcher = new BusinessObjectCommerceDispatcher(() => Now.AddMinutes(5));

        BusinessObjectCommerceDispatchResult result = await dispatcher.DispatchPendingAsync(adapter, workspace);

        Assert.Equal(4, result.AcceptedCount);
        BusinessObjectTransactionDraft transferDraft = Assert.Single(adapter.SubmittedDrafts, draft => draft.SelectedMode == BusinessObjectTransactionMode.TransferObject);
        Assert.Equal(TargetObjectId, transferDraft.SourceObjectId);
        Assert.Equal(Bob, transferDraft.ReceiverAddress);
        BusinessObjectCommercePayloadBuilder.TryGetSaleTerms(transferDraft.StateJson, out BusinessObjectSaleTerms? soldTerms);
        Assert.NotNull(soldTerms);
        Assert.Equal(BusinessObjectSaleStatus.Sold, soldTerms!.Status);
        Assert.Equal(1, soldTerms.SoldCount);

        BusinessObjectTransactionDraft paymentUpdate = Assert.Single(adapter.SubmittedDrafts, draft => draft.ExternalRef.StartsWith("bo-commerce://payment/", StringComparison.Ordinal));
        BusinessObjectPaymentRecord completedPayment = BusinessObjectPayloadBuilder
            .ParseMediaPayload(paymentUpdate.StateJson, "payment")
            .Commerce!
            .Payment!;
        Assert.Equal(TargetObjectId.Value, completedPayment.DeliveredObjectId);
    }

    [Fact]
    public async Task OriginalImageSale_DispatchesTransferWithImagePayloadForBuyerGallery()
    {
        ITypedBusinessObjectView target = CreateListedTarget(
            BusinessObjectSaleMode.Original,
            objectId: ImageTargetObjectId,
            objectType: "image",
            includeImageDataItem: true);
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync(
            BusinessObjectSaleMode.Original,
            ImageTargetObjectId);
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(target, payment);
        var adapter = new CapturingWorkspaceAdapter(workspace);
        var dispatcher = new BusinessObjectCommerceDispatcher(() => Now.AddMinutes(5));

        BusinessObjectCommerceDispatchResult result = await dispatcher.DispatchPendingAsync(adapter, workspace);

        Assert.Equal(4, result.AcceptedCount);
        BusinessObjectTransactionDraft transferDraft = Assert.Single(adapter.SubmittedDrafts, draft => draft.SelectedMode == BusinessObjectTransactionMode.TransferObject);
        Assert.Equal("image", transferDraft.ObjectType);
        Assert.Equal(ImageTargetObjectId, transferDraft.SourceObjectId);
        Assert.Equal(Bob, transferDraft.ReceiverAddress);

        BusinessObjectMediaPayload transferredPayload = BusinessObjectPayloadBuilder.ParseMediaPayload(
            transferDraft.StateJson,
            ImageTargetObjectId.Value);
        BusinessObjectDataItem imageItem = Assert.Single(transferredPayload.DataItems ?? []);
        Assert.Equal(BusinessObjectDataItemType.Image, imageItem.Type);
        Assert.True(imageItem.IsMain);
        Assert.Equal("Dispatch image", imageItem.Name);

        var imageCaster = new ImageBusinessObjectCaster(new ManualTimeProvider(Now.AddMinutes(6)));
        IBusinessObjectView transferredView = await imageCaster.CastAsync(
            CreateSource(ImageTargetObjectId, "image", transferDraft.StateJson, Bob),
            CancellationToken.None);
        ImageBusinessObjectView imageView = Assert.IsType<ImageBusinessObjectView>(transferredView);
        Assert.Equal(Bob, imageView.OwnerAddress);
        Assert.Equal("Dispatch image target", imageView.Name);
        Assert.Single(imageView.DataItems);
    }

    private static ITypedBusinessObjectView CreateListedTarget(
        BusinessObjectSaleMode saleMode,
        int? maxClones = null,
        int soldCount = 0,
        BusinessObjectId? objectId = null,
        string objectType = "post",
        bool includeImageDataItem = false)
    {
        BusinessObjectId targetObjectId = objectId ?? TargetObjectId;
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(objectType);
        IReadOnlyList<BusinessObjectDataItem> dataItems = includeImageDataItem
            ?
            [
                new BusinessObjectDataItem(
                    "Dispatch image",
                    BusinessObjectDataItemType.Image,
                    BusinessObjectDataItemStorage.Url,
                    "https://example.test/dispatch-image.png",
                    new Uri("https://example.test/dispatch-image.png"),
                    "image/png",
                    "dispatch-image.png",
                    1024,
                    IsMain: true,
                    "Image payload used by the commerce dispatch regression.")
            ]
            : [];
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            includeImageDataItem ? "Dispatch image target" : "Dispatch target",
            "Target BO for commerce dispatch.",
            "Long target body.",
            "#commerce",
            dataItems);
        BusinessObjectSaleTerms sale = BusinessObjectCommercePayloadBuilder.CreateSaleTerms(
            saleMode,
            Amount.FromDecimal(7.25m),
            Alice,
            Now,
            maxClones,
            sharePath: $"/buy-bo/tx/{Uri.EscapeDataString($"tx-{objectType}")}/0") with
        {
            SoldCount = soldCount,
            Status = BusinessObjectSaleStatus.Listed
        };
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.WithSaleTerms(payload, sale));
        return CreateTypedView(targetObjectId, objectType, stateJson, Alice);
    }

    private static Task<ICommerceBusinessObjectView> CreatePaymentViewAsync(BusinessObjectSaleMode saleMode)
        => CreatePaymentViewAsync(CreatePaymentRecord(saleMode));

    private static Task<ICommerceBusinessObjectView> CreatePaymentViewAsync(
        BusinessObjectSaleMode saleMode,
        BusinessObjectId targetObjectId)
        => CreatePaymentViewAsync(CreatePaymentRecord(saleMode, targetObjectId));

    private static BusinessObjectPaymentRecord CreatePaymentRecord(
        BusinessObjectSaleMode saleMode,
        BusinessObjectId? targetObjectId = null)
    {
        BusinessObjectId targetId = targetObjectId ?? TargetObjectId;
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            PaymentObjectId,
            Bob,
            Alice,
            targetId,
            targetVersion: 1,
            targetOutPoint: new OutPoint(new TxId("tx-target"), 0),
            Amount.FromDecimal(7.25m),
            saleMode,
            paymentTxId: null,
            Now,
            saleMaxClones: saleMode == BusinessObjectSaleMode.Copy ? 2 : null);
        return payment with
        {
            PurchaseObjectId = BusinessObjectCommercePayloadBuilder.CreatePurchaseObjectId(PaymentObjectId).Value
        };
    }

    private static async Task<ICommerceBusinessObjectView> CreatePaymentViewAsync(BusinessObjectPaymentRecord payment)
    {
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment));
        return await CastCommerceViewAsync(PaymentObjectId, "payment", stateJson, Alice);
    }

    private static async Task<ICommerceBusinessObjectView> CreateReceiptViewAsync(BusinessObjectPaymentRecord payment)
    {
        BusinessObjectReceiptRecord receipt = BusinessObjectCommercePayloadBuilder.CreateReceiptRecord(
            new BusinessObjectId(payment.ReceiptObjectId!),
            payment,
            new BusinessObjectId(payment.DeliveredObjectId!),
            new TxId(payment.DispatchTxId!),
            Now.AddMinutes(2));
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreateReceiptPayload(receipt));
        return await CastCommerceViewAsync(new BusinessObjectId(receipt.ReceiptObjectId), "receipt", stateJson, Alice);
    }

    private static async Task<ICommerceBusinessObjectView> CastCommerceViewAsync(
        BusinessObjectId objectId,
        string objectType,
        string stateJson,
        Address owner)
    {
        var caster = new CommerceBusinessObjectCaster(new ManualTimeProvider(Now));
        IBusinessObjectView view = await caster.CastAsync(
            CreateSource(objectId, objectType, stateJson, owner),
            CancellationToken.None);
        return Assert.IsAssignableFrom<ICommerceBusinessObjectView>(view);
    }

    private static ITypedBusinessObjectView CreateTypedView(
        BusinessObjectId objectId,
        string objectType,
        string stateJson,
        Address owner)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(objectType);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, objectId.Value);
        return new GenericTypedBusinessObjectView(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            owner,
            BusinessObjectOperation.Create,
            new HashHex(new string('1', 64)),
            new OutPoint(new TxId($"tx-{objectType}"), 0),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject,
            Now,
            Now,
            payload.Name,
            payload.Description,
            payload.Text,
            payload.Author,
            payload.Link,
            payload.ImageLink,
            payload.TagList,
            payload.DataItems ?? Array.Empty<BusinessObjectDataItem>(),
            definition,
            payload.Fields ?? new Dictionary<string, string>(StringComparer.Ordinal),
            BusinessObjectPayloadBuilder.CreateFacts(definition, payload.Fields));
    }

    private static BusinessObjectCastSource CreateSource(
        BusinessObjectId objectId,
        string objectType,
        string stateJson,
        Address owner)
        => new(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            owner,
            BusinessObjectOperation.Create,
            new HashHex(new string('2', 64)),
            new OutPoint(new TxId($"tx-{objectType}"), 0),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject,
            Now);

    private static BusinessObjectWorkspaceModel CreateWorkspace(
        params IBusinessObjectView[] businessObjects)
        => CreateWorkspace(businessObjects, []);

    private static BusinessObjectWorkspaceModel CreateWorkspace(
        IReadOnlyList<IBusinessObjectView> businessObjects,
        IReadOnlyList<LedgerTransactionSummary> transactions)
    {
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(100),
            new Dictionary<TokenId, Amount>(),
            UtxoCount: 3,
            Now);
        BusinessObjectTransactionDraft draft = new(
            new BusinessObjectId("bo:dispatch:draft"),
            "post",
            "1.0",
            Alice,
            Bob,
            Bob,
            Amount.Zero,
            Amount.Zero,
            "{}",
            string.Empty);
        BusinessObjectEditorModel editor = new(
            new BusinessObjectId("bo:dispatch:draft"),
            "post",
            "1.0",
            Alice,
            "{}",
            Now);

        return new BusinessObjectWorkspaceModel(
            "Alice dispatch account",
            Context,
            Alice,
            balance,
            [new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(100), IsMain: true)],
            [],
            [],
            businessObjects,
            [],
            transactions,
            draft,
            editor,
            Now);
    }

    private static LedgerTransactionSummary CreateObjectOutputTransaction(string objectId, Address owner)
        => new(
            Context,
            new TxId($"tx-{objectId.Replace(':', '-')}"),
            TransactionKind.BusinessObject,
            TransactionStatus.Confirmed,
            Now.AddMinutes(1),
            [Alice],
            [owner],
            [],
            [
                new LedgerTransactionOutputSummary(
                    0,
                    owner,
                    Amount.Zero,
                    null,
                    new BusinessObjectRef(new BusinessObjectId(objectId), 1, BusinessObjectOperation.Create, new HashHex(new string('3', 64))))
            ]);

    private sealed class CapturingWorkspaceAdapter(BusinessObjectWorkspaceModel workspace) : IEconomyAccountWorkspaceAdapter
    {
        public List<BusinessObjectTransactionDraft> SubmittedDrafts { get; } = [];

        public string HostMode => "WASM";

        public bool UsesServerSideAccountSigning => false;

        public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(workspace);

        public Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
            int maxTransactions = 10,
            CancellationToken cancellationToken = default)
            => Task.FromResult(AccountWorkspaceRefreshSnapshot.Create(workspace, maxTransactions, workspace.ViewedAtUtc));

        public Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(
            TxId txId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<EconomyTransactionDetailsModel?>(null);

        public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
            BusinessObjectTransactionDraft draft,
            CancellationToken cancellationToken = default)
        {
            SubmittedDrafts.Add(draft);
            return Task.FromResult(new EconomyTransactionExecutionResult(
                draft.SelectedMode,
                Accepted: true,
                TxId: new TxId($"tx-commerce-{SubmittedDrafts.Count}"),
                LedgerSequence: SubmittedDrafts.Count,
                "Accepted."));
        }

        public Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(
            BusinessObjectSendRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Dispatcher must submit deterministic drafts.");

        public Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(
            BusinessObjectPriceRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Dispatcher must submit explicit update drafts.");

        public Task<EconomyTransactionExecutionResult> UpdateProfileAsync(
            ProfileEditRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
            BusinessObjectTicketVerificationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EconomyTransactionExecutionResult> UseTicketAsync(
            BusinessObjectTicketUseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EconomyTransactionExecutionResult> CreateTokenAsync(
            EconomyTokenCreationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(
            EconomyRuleTokenCreationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MessageSecurityActionResult> SignMessageAsync(
            MessageSecuritySignRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));

        public Task<MessageSecurityActionResult> VerifyMessageAsync(
            MessageSecurityVerifyRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));

        public Task<MessageSecurityActionResult> EncryptMessageAsync(
            MessageSecurityEncryptRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));

        public Task<MessageSecurityActionResult> DecryptMessageAsync(
            MessageSecurityDecryptRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));
    }
}
