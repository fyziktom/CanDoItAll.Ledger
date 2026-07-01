using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
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
        Assert.Equal($"/buy-bo/{Uri.EscapeDataString(ListedObjectId.Value)}", request.SharePath);
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

    private static ITypedBusinessObjectView CreateListedPost()
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
            BusinessObjectSaleMode.Copy,
            Amount.FromDecimal(12.5m),
            Alice,
            ListedAtUtc,
            maxClones: 3,
            sharePath: $"/buy-bo/{Uri.EscapeDataString(ListedObjectId.Value)}");
        BusinessObjectMediaPayload listedPayload = BusinessObjectCommercePayloadBuilder.WithSaleTerms(payload, terms);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(listedPayload);

        return CreateTypedView(ListedObjectId, "post", stateJson);
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
        string stateJson)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(objectType);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, objectId.Value);

        return new GenericTypedBusinessObjectView(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('1', 64)),
            new OutPoint(new TxId($"tx-{objectType}-listed"), 0),
            stateJson,
            $"ledger://{objectId.Value}",
            TransactionDataItemType.BusinessObject,
            ListedAtUtc,
            ListedAtUtc,
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
    {
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(1000),
            new Dictionary<TokenId, Amount>(),
            businessObjects.Length,
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
            [],
            draft,
            editor,
            ListedAtUtc);
    }

    private static IReadOnlyList<EconomyTokenOption> CreateTokenOptions()
        =>
        [
            new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(1000), IsMain: true)
        ];
}
