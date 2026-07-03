using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectCommerceUiComponentTests : TestContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("test-ledger"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly BusinessObjectId ListedObjectId = new("bo:post:listed-commerce-post");
    private static readonly DateTimeOffset ListedAtUtc = DateTimeOffset.Parse("2026-06-30T10:00:00Z");

    public BusinessObjectCommerceUiComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddEconomySsrAccountWorkspaceAdapters();
    }

    [Fact]
    public void DetailsDialog_RendersSalePanelForListedBusinessObject()
    {
        ITypedBusinessObjectView listedPost = CreateListedPost();

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, listedPost));

        IElement panel = component.Find("[data-testid='cda-bo-sale-panel']");

        Assert.Contains("For sale", panel.TextContent, StringComparison.Ordinal);
        Assert.Contains("Copy / 12.5 MAIN", panel.TextContent, StringComparison.Ordinal);
        Assert.Contains("0 / 3", panel.TextContent, StringComparison.Ordinal);
        Assert.Contains("/buy-bo/tx/", panel.InnerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Gallery_RendersSaleBadgeForListedBusinessObject()
    {
        ITypedBusinessObjectView listedPost = CreateListedPost();

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(listedPost)));

        IElement badge = component.Find("[data-testid='cda-bo-sale-badge']");

        Assert.Equal("Copy / 12.5 MAIN", badge.TextContent.Trim());
    }

    [Fact]
    public void Gallery_OrdersBusinessObjectsFromNewestToOldest()
    {
        ITypedBusinessObjectView older = CreatePost(
            new BusinessObjectId("bo:post:older"),
            "Older listing",
            ListedAtUtc.AddMinutes(-5));
        ITypedBusinessObjectView newest = CreatePost(
            new BusinessObjectId("bo:post:newest"),
            "Newest listing",
            ListedAtUtc.AddMinutes(5));
        ITypedBusinessObjectView middle = CreatePost(
            new BusinessObjectId("bo:post:middle"),
            "Middle listing",
            ListedAtUtc);

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(older, newest, middle)));

        string[] headings = component.FindAll(".cda-vef-gallery-card h3")
            .Select(element => element.TextContent.Trim())
            .ToArray();

        Assert.Equal(["Newest listing", "Middle listing", "Older listing"], headings);
    }

    [Fact]
    public void Gallery_OrdersBusinessObjectsByCurrentTransactionTimestamp()
    {
        ITypedBusinessObjectView older = CreatePost(
            new BusinessObjectId("bo:post:tx-older"),
            "Older transaction",
            ListedAtUtc);
        ITypedBusinessObjectView newest = CreatePost(
            new BusinessObjectId("bo:post:tx-newest"),
            "Newest transaction",
            ListedAtUtc);
        ITypedBusinessObjectView middle = CreatePost(
            new BusinessObjectId("bo:post:tx-middle"),
            "Middle transaction",
            ListedAtUtc);

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(
                    x => x.Workspace,
                    CreateWorkspace(
                        [older, newest, middle],
                        [
                            CreateObjectTransaction(older, ListedAtUtc.AddMinutes(1)),
                            CreateObjectTransaction(newest, ListedAtUtc.AddMinutes(3)),
                            CreateObjectTransaction(middle, ListedAtUtc.AddMinutes(2))
                        ])));

        string[] headings = component.FindAll(".cda-vef-gallery-card h3")
            .Select(element => element.TextContent.Trim())
            .ToArray();

        Assert.Equal(["Newest transaction", "Middle transaction", "Older transaction"], headings);
        Assert.Contains("06.30.2026 10:03", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Gallery_UsesTransactionListOrderWhenTimestampsTie()
    {
        ITypedBusinessObjectView older = CreatePost(
            new BusinessObjectId("bo:post:tie-older"),
            "Older tie transaction",
            ListedAtUtc);
        ITypedBusinessObjectView newest = CreatePost(
            new BusinessObjectId("bo:post:tie-newest"),
            "Newest tie transaction",
            ListedAtUtc);
        ITypedBusinessObjectView middle = CreatePost(
            new BusinessObjectId("bo:post:tie-middle"),
            "Middle tie transaction",
            ListedAtUtc);

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(
                    x => x.Workspace,
                    CreateWorkspace(
                        [older, newest, middle],
                        [
                            CreateObjectTransaction(newest, ListedAtUtc),
                            CreateObjectTransaction(middle, ListedAtUtc),
                            CreateObjectTransaction(older, ListedAtUtc)
                        ])));

        string[] headings = component.FindAll(".cda-vef-gallery-card h3")
            .Select(element => element.TextContent.Trim())
            .ToArray();

        Assert.Equal(["Newest tie transaction", "Middle tie transaction", "Older tie transaction"], headings);
    }

    [Fact]
    public async Task Gallery_HidesCommerceArtifactsFromCards()
    {
        ITypedBusinessObjectView post = CreatePost(
            new BusinessObjectId("bo:post:visible-gallery-post"),
            "Visible gallery post",
            ListedAtUtc);
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync();

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(
                    x => x.Workspace,
                    CreateWorkspace(
                        [post, payment],
                        [
                            CreateObjectTransaction(post, ListedAtUtc.AddMinutes(1)),
                            CreateObjectTransaction(payment, ListedAtUtc.AddMinutes(2))
                        ])));

        IElement card = Assert.Single(component.FindAll(".cda-vef-gallery-card"));

        Assert.Contains("Visible gallery post", card.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Payment for", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Gallery_HidesSecureMessageBusinessObjectsFromCards()
    {
        ITypedBusinessObjectView post = CreatePost(
            new BusinessObjectId("bo:post:visible-message-boundary-post"),
            "Visible non-message post",
            ListedAtUtc);
        ITypedBusinessObjectView message = CreatePost(
            new BusinessObjectId("bo:secure-message:hidden-gallery-message"),
            "Hidden secure message",
            ListedAtUtc.AddMinutes(5),
            SecureLedgerMessages.ObjectType);
        ITypedBusinessObjectView wrappedMessage = CreatePost(
            new BusinessObjectId("bo:secure-message:hidden-wrapped-gallery-message"),
            "Hidden wrapped secure message",
            ListedAtUtc.AddMinutes(6),
            "business-object");

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(
                    x => x.Workspace,
                    CreateWorkspace(
                        [post, message, wrappedMessage],
                        [
                            CreateObjectTransaction(post, ListedAtUtc.AddMinutes(1)),
                            CreateObjectTransaction(message, ListedAtUtc.AddMinutes(2)),
                            CreateObjectTransaction(wrappedMessage, ListedAtUtc.AddMinutes(3))
                        ])));

        IElement card = Assert.Single(component.FindAll(".cda-vef-gallery-card"));

        Assert.Contains("Visible non-message post", card.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden secure message", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden wrapped secure message", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(SecureLedgerMessages.ObjectType, component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SetSaleDialog_HidesMaxCopiesUntilCopyModeIsSelected()
    {
        ITypedBusinessObjectView originalSalePost = CreateListedPost(BusinessObjectSaleMode.Original);

        IRenderedComponent<BusinessObjectSetPriceDialogContent> component =
            RenderComponent<BusinessObjectSetPriceDialogContent>(parameters => parameters
                .Add(x => x.Object, originalSalePost)
                .Add(x => x.TokenOptions, CreateTokenOptions()));

        Assert.Empty(component.FindAll("[data-testid='bo-sale-max-clones']"));

        component.Find("[data-testid='bo-sale-mode']").Change(nameof(BusinessObjectSaleMode.Copy));

        Assert.Single(component.FindAll("[data-testid='bo-sale-max-clones']"));
    }

    [Fact]
    public void SetSaleDialog_EmitsCopySaleRequestWithSharePath()
    {
        BusinessObjectPriceRequest? captured = null;
        ITypedBusinessObjectView listedPost = CreateListedPost();

        IRenderedComponent<BusinessObjectSetPriceDialogContent> component =
            RenderComponent<BusinessObjectSetPriceDialogContent>(parameters => parameters
                .Add(x => x.Object, listedPost)
                .Add(x => x.TokenOptions, CreateTokenOptions())
                .Add(
                    x => x.SubmitRequested,
                    EventCallback.Factory.Create<BusinessObjectPriceRequest>(
                        this,
                        request => captured = request)));

        component.Find("[data-testid='bo-sale-mode']").Change(nameof(BusinessObjectSaleMode.Copy));
        component.Find("[data-testid='bo-sale-price']").Input("14.75");
        component.Find("[data-testid='bo-sale-max-clones']").Input("4");
        component.FindAll("button")
            .First(button => button.TextContent.Contains("Set Sale", StringComparison.Ordinal))
            .Click();

        BusinessObjectPriceRequest request = Assert.IsType<BusinessObjectPriceRequest>(captured);
        Assert.Equal(ListedObjectId, request.ObjectId);
        Assert.Equal(BusinessObjectSaleMode.Copy, request.SaleMode);
        Assert.Equal(14.75m, request.PriceAmount.Value);
        Assert.Equal(4, request.MaxClones);
        Assert.False(request.ClearSale);
        Assert.Equal(BuyPathFor(listedPost.CurrentOutPoint), request.SharePath);
    }

    [Fact]
    public async Task CommerceDashboard_RendersPaymentRecordsWithTargetContext()
    {
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync();

        IRenderedComponent<BusinessObjectCommerceDashboard> component =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(payment)));

        IElement row = component.Find("[data-testid='cda-commerce-record']");

        Assert.Contains("Payment", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("Pending / 25 MAIN", row.TextContent, StringComparison.Ordinal);
        Assert.Contains(ListedObjectId.Value, row.TextContent, StringComparison.Ordinal);
    }

    private static ITypedBusinessObjectView CreateListedPost(
        BusinessObjectSaleMode saleMode = BusinessObjectSaleMode.Copy)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            "Listed Commerce Post",
            "Short sale description",
            "Long sale description that remains available in the details dialog.",
            "#commerce",
            [],
            new Dictionary<string, string>
            {
                ["channel"] = "proof",
                ["visibility"] = "public"
            });
        BusinessObjectSaleTerms terms = BusinessObjectCommercePayloadBuilder.CreateSaleTerms(
            saleMode,
            Amount.FromDecimal(12.5m),
            Alice,
            ListedAtUtc,
            maxClones: saleMode == BusinessObjectSaleMode.Copy ? 3 : null,
            sharePath: BuyPathFor(OutPointFor("post", ListedObjectId)));
        BusinessObjectMediaPayload listedPayload = BusinessObjectCommercePayloadBuilder.WithSaleTerms(payload, terms);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(listedPayload);

        return CreateTypedView(ListedObjectId, "post", stateJson);
    }

    private static ITypedBusinessObjectView CreatePost(
        BusinessObjectId objectId,
        string name,
        DateTimeOffset createdAt,
        string objectType = "post")
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            name,
            $"{name} short description",
            $"{name} long description",
            "#commerce",
            []);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(payload);
        return CreateTypedView(objectId, objectType, stateJson, createdAt);
    }

    private static async Task<ICommerceBusinessObjectView> CreatePaymentViewAsync()
    {
        var paymentId = new BusinessObjectId("bo:payment:listed-commerce-post");
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            paymentId,
            Bob,
            Alice,
            ListedObjectId,
            targetVersion: 1,
            targetOutPoint: new OutPoint(new TxId("target-tx"), 0),
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            new TxId("payment-tx"),
            ListedAtUtc);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment));
        var caster = new CommerceBusinessObjectCaster(new ManualTimeProvider(ListedAtUtc.AddMinutes(3)));
        IBusinessObjectView view = await caster.CastAsync(
            CreateSource(paymentId, "payment", stateJson),
            CancellationToken.None);

        return Assert.IsAssignableFrom<ICommerceBusinessObjectView>(view);
    }

    private static ITypedBusinessObjectView CreateTypedView(
        BusinessObjectId objectId,
        string objectType,
        string stateJson,
        DateTimeOffset? createdAt = null)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(objectType);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, objectId.Value);
        DateTimeOffset timestamp = createdAt ?? ListedAtUtc;

        return new GenericTypedBusinessObjectView(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('1', 64)),
            OutPointFor(objectType, objectId),
            stateJson,
            $"ledger://{objectId.Value}",
            TransactionDataItemType.BusinessObject,
            timestamp,
            timestamp,
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
        string stateJson)
        => new(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('2', 64)),
            new OutPoint(new TxId($"tx-{objectType}"), 0),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject,
            ListedAtUtc);

    private static BusinessObjectWorkspaceModel CreateWorkspace(params IBusinessObjectView[] businessObjects)
        => CreateWorkspace(businessObjects, []);

    private static BusinessObjectWorkspaceModel CreateWorkspace(
        IReadOnlyList<IBusinessObjectView> businessObjects,
        IReadOnlyList<LedgerTransactionSummary> transactions)
    {
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(1000),
            new Dictionary<TokenId, Amount>(),
            businessObjects.Count,
            ListedAtUtc);
        BusinessObjectTransactionDraft draft = new(
            new BusinessObjectId("bo:post:draft"),
            "post",
            "1.0",
            Alice,
            Alice,
            Alice,
            Amount.Zero,
            Amount.Zero,
            "{}",
            string.Empty);
        BusinessObjectEditorModel editor = new(
            new BusinessObjectId("bo:post:draft"),
            "post",
            "1.0",
            Alice,
            "{}",
            ListedAtUtc);

        return new BusinessObjectWorkspaceModel(
            "Alice test account",
            Context,
            Alice,
            balance,
            CreateTokenOptions(),
            [],
            [],
            businessObjects,
            [],
            transactions,
            draft,
            editor,
            ListedAtUtc);
    }

    private static LedgerTransactionSummary CreateObjectTransaction(
        IBusinessObjectView businessObject,
        DateTimeOffset timestamp)
        => new(
            Context,
            businessObject.CurrentOutPoint.TxId,
            TransactionKind.BusinessObject,
            TransactionStatus.Confirmed,
            timestamp,
            [Alice],
            [businessObject.OwnerAddress],
            [],
            [
                new LedgerTransactionOutputSummary(
                    businessObject.CurrentOutPoint.OutputIndex,
                    businessObject.OwnerAddress,
                    Amount.Zero,
                    null,
                    new BusinessObjectRef(
                        businessObject.ObjectId,
                        businessObject.Version,
                        businessObject.Operation,
                        businessObject.DataHash))
            ]);

    private static OutPoint OutPointFor(string objectType, BusinessObjectId objectId)
        => new(new TxId($"tx-{objectType}-{objectId.Value.Replace(':', '-')}"), 0);

    private static string BuyPathFor(OutPoint outPoint)
        => $"/buy-bo/tx/{Uri.EscapeDataString(outPoint.TxId.Value)}/{outPoint.OutputIndex}";

    private static IReadOnlyList<EconomyTokenOption> CreateTokenOptions()
        =>
        [
            new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(1000), IsMain: true)
        ];
}
