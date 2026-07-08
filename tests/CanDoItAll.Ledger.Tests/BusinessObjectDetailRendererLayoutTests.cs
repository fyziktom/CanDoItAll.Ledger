using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Rendering;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectDetailRendererLayoutTests : TestContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("bo-detail-layout"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-detail-layout"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-detail-layout"));
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-08T10:00:00Z");

    public BusinessObjectDetailRendererLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddEconomySsrAccountWorkspaceAdapters();
    }

    [Fact]
    public void DetailsDialog_HidesDataItemHeroForTypedObjectWithoutDataItems()
    {
        ITypedBusinessObjectView eventObject = CreateTypedObject(
            "event",
            "Annual Developer Conference",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventDate"] = "2026-08-15T09:00:00Z",
                ["eventClass"] = "Conference",
                ["location"] = "Berlin, Germany",
                ["organizer"] = "CanDoItAll",
                ["status"] = "Confirmed"
            });

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, eventObject));

        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Empty(component.FindAll(".cda-vef-bo-selected-preview"));
        Assert.Empty(component.FindAll(".cda-vef-bo-gallery-rail"));
        Assert.Contains("Annual Developer Conference", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Berlin, Germany", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsDialog_PreservesDataItemHeroForMediaObjectWithDataItems()
    {
        ITypedBusinessObjectView mediaObject = CreateTypedObject(
            "post",
            "Media-backed post",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["channel"] = "proof",
                ["visibility"] = "public"
            },
            [
                new BusinessObjectDataItem(
                    "Main photo",
                    BusinessObjectDataItemType.Image,
                    BusinessObjectDataItemStorage.Url,
                    "main-photo-hash",
                    new Uri("https://example.test/main-photo.png"),
                    "image/png",
                    "main-photo.png",
                    128,
                    IsMain: true)
            ]);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, mediaObject));

        Assert.Empty(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Single(component.FindAll(".cda-vef-bo-selected-preview"));
        Assert.Single(component.FindAll(".cda-vef-bo-gallery-rail"));
        Assert.Contains("Main photo", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Gallery_UsesSharedBusinessObjectGalleryCardComponent()
    {
        ITypedBusinessObjectView eventObject = CreateTypedObject(
            "event",
            "Gallery Shared Card Event",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["location"] = "Main Hall"
            });

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(eventObject)));

        Assert.Single(component.FindAll("[data-testid='cda-bo-gallery-card']"));
        Assert.Contains("Gallery Shared Card Event", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Main Hall", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsDialog_RendersRelatedEventCardForTicketWithoutDataItems()
    {
        ITypedBusinessObjectView eventObject = CreateTypedObject(
            "event",
            "Conference Main Stage",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventDate"] = "2026-08-15T09:00:00Z",
                ["location"] = "Main Hall"
            });
        ITypedBusinessObjectView ticketObject = CreateTypedObject(
            "ticket",
            "Conference Ticket A12",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventId"] = eventObject.ObjectId.Value,
                ["eventAddress"] = Alice.Value,
                ["seat"] = "A12",
                ["ticketClass"] = "VIP",
                ["ticketDuration"] = "1 day"
            });

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, ticketObject)
                .Add(x => x.Workspace, CreateWorkspace(eventObject, ticketObject)));

        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-panel']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-card']"));
        Assert.Empty(component.FindAll(".cda-vef-bo-selected-preview"));
        Assert.Contains("Conference Main Stage", component.Markup, StringComparison.Ordinal);
        Assert.Contains("A12", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsDialog_DoesNotRenderBrokenRelatedCardWhenTargetMissing()
    {
        ITypedBusinessObjectView ticketObject = CreateTypedObject(
            "ticket",
            "Missing Event Ticket",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventId"] = "bo:event:not-in-workspace",
                ["seat"] = "B22"
            });

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, ticketObject)
                .Add(x => x.Workspace, CreateWorkspace(ticketObject)));

        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-related-panel']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-related-card']"));
        Assert.Contains("bo:event:not-in-workspace", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetailsDialog_RendersCommerceRelatedCardForPaymentWithoutDataItems()
    {
        ITypedBusinessObjectView targetObject = CreateTypedObject(
            "post",
            "Listed Compliance Report",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["channel"] = "reports",
                ["visibility"] = "buyer-only"
            });
        ICommerceBusinessObjectView paymentObject = await CreatePaymentObjectAsync(targetObject);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, paymentObject)
                .Add(x => x.Workspace, CreateWorkspace(targetObject, paymentObject)));

        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-panel']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-card']"));
        Assert.Empty(component.FindAll(".cda-vef-bo-selected-preview"));
        Assert.Contains("Listed Compliance Report", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Payment for", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendererRegistry_ResolvesCommerceAndTypedFamilyBeforeMedia()
    {
        ITypedBusinessObjectView eventObject = CreateTypedObject(
            "event",
            "Renderer Event",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["location"] = "Auditorium"
            });
        ICommerceBusinessObjectView paymentObject = await CreatePaymentObjectAsync(eventObject);
        var registry = new BusinessObjectRendererRegistry(
        [
            new MediaBusinessObjectRendererProvider(),
            new GenericBusinessObjectRendererProvider(),
            new TypedFamilyBusinessObjectRendererProvider(),
            new CommerceBusinessObjectRendererProvider()
        ]);

        Assert.Equal("typed-family", registry.Resolve(eventObject).Key);
        Assert.Equal("commerce-record", registry.Resolve(paymentObject).Key);
    }

    private static ITypedBusinessObjectView CreateTypedObject(
        string objectType,
        string name,
        IReadOnlyDictionary<string, string> fields,
        IReadOnlyList<BusinessObjectDataItem>? dataItems = null)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(objectType);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            name,
            $"{name} short description",
            $"{name} long description",
            $"#{objectType} #layout",
            dataItems ?? [],
            fields);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(payload);
        BusinessObjectId objectId = new($"bo:{objectType}:{BusinessObjectCreateIdFactory.NormalizeName(name)}");

        return new GenericTypedBusinessObjectView(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('8', 64)),
            new OutPoint(new TxId($"tx-{objectType}-{BusinessObjectCreateIdFactory.NormalizeName(name)}"), 0),
            stateJson,
            $"ledger://{objectId.Value}",
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

    private static async Task<ICommerceBusinessObjectView> CreatePaymentObjectAsync(IBusinessObjectView targetObject)
    {
        BusinessObjectId paymentId = new("bo:payment:detail-layout");
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            paymentId,
            Bob,
            Alice,
            targetObject.ObjectId,
            targetVersion: targetObject.Version,
            targetOutPoint: targetObject.CurrentOutPoint,
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            new TxId("tx-payment-detail-layout"),
            Now);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment));
        var caster = new CommerceBusinessObjectCaster(new ManualTimeProvider(Now.AddMinutes(1)));
        IBusinessObjectView view = await caster.CastAsync(
            CreateSource(paymentId, "payment", stateJson, Bob),
            CancellationToken.None);

        return Assert.IsAssignableFrom<ICommerceBusinessObjectView>(view);
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
            new HashHex(new string('9', 64)),
            new OutPoint(new TxId($"tx-{objectType}-{BusinessObjectCreateIdFactory.NormalizeName(objectId.Value)}"), 0),
            stateJson,
            $"ledger://{objectId.Value}",
            TransactionDataItemType.BusinessObject,
            Now);

    private static BusinessObjectWorkspaceModel CreateWorkspace(params IBusinessObjectView[] businessObjects)
    {
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(1000),
            new Dictionary<TokenId, Amount>(),
            businessObjects.Length,
            Now);
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
            draft.ObjectId,
            draft.ObjectType,
            draft.SchemaVersion,
            Alice,
            "{}",
            Now);

        return new BusinessObjectWorkspaceModel(
            "Alice detail layout account",
            Context,
            Alice,
            balance,
            [new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(1000), IsMain: true)],
            [],
            [],
            businessObjects,
            [],
            [],
            draft,
            editor,
            Now);
    }
}
