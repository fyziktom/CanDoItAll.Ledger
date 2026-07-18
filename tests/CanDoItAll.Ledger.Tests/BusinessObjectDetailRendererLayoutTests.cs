using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Rendering;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectDetailRendererLayoutTests : BunitContext
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
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, eventObject));

        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Empty(component.FindAll(".cda-bo-selected-preview"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-overview-data-items']"));
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
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, mediaObject));

        Assert.Empty(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Single(component.FindAll(".cda-bo-selected-preview"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-data-item-selector']"));
        component.Find("[data-testid='cda-bo-data-items-tab-button']").Click();
        Assert.Single(component.FindAll("[data-testid='cda-bo-data-items-tab']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-data-items-tab-list']"));
        Assert.Contains("Main photo", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsDialog_OverviewSelectorAndFullListShareSelectedDataItem()
    {
        var mainPhoto = new BusinessObjectDataItem(
            "Main photo",
            BusinessObjectDataItemType.Image,
            BusinessObjectDataItemStorage.Url,
            "main-photo-hash",
            new Uri("https://example.test/main-photo.png"),
            "image/png",
            "main-photo.png",
            128,
            IsMain: true);
        var report = new BusinessObjectDataItem(
            "Audit report",
            BusinessObjectDataItemType.Pdf,
            BusinessObjectDataItemStorage.Url,
            "audit-report-hash",
            new Uri("https://example.test/audit-report.pdf"),
            "application/pdf",
            "audit-report.pdf",
            4096);
        ITypedBusinessObjectView mediaObject = CreateTypedObject(
            "post",
            "Multi-item post",
            new Dictionary<string, string>(StringComparer.Ordinal),
            [mainPhoto, report]);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, mediaObject));

        IReadOnlyList<AngleSharp.Dom.IElement> options = component.FindAll("[data-testid='cda-bo-overview-data-item-option']");
        Assert.Equal(2, options.Count);
        Assert.Equal("true", options[0].GetAttribute("aria-selected"));
        Assert.Equal("false", options[1].GetAttribute("aria-selected"));

        options[1].Click();

        AngleSharp.Dom.IElement preview = component.Find("[data-testid='cda-bo-selected-preview']");
        Assert.Equal(report.Hash, preview.GetAttribute("data-item-hash"));
        Assert.Equal("false", component.FindAll("[data-testid='cda-bo-overview-data-item-option']")[0].GetAttribute("aria-selected"));
        Assert.Equal("true", component.FindAll("[data-testid='cda-bo-overview-data-item-option']")[1].GetAttribute("aria-selected"));

        component.Find("[data-testid='cda-bo-data-items-tab-button']").Click();
        SelectionListItem selected = Assert.Single(
            component.FindComponents<SelectionListItem>(),
            item => item.Instance.Title == report.Name).Instance;
        Assert.True(selected.IsSelected);
    }

    [Fact]
    public void DataItemPreviewSelector_UsesIconsInsteadOfNestedMediaControls()
    {
        var video = new BusinessObjectDataItem(
            "Demo video",
            BusinessObjectDataItemType.AVMedia,
            BusinessObjectDataItemStorage.Url,
            "demo-video-hash",
            new Uri("https://example.test/demo.mp4"),
            "video/mp4",
            "demo.mp4",
            2048,
            IsMain: true);

        IRenderedComponent<BusinessObjectDataItemPreviewSelector> component =
            Render<BusinessObjectDataItemPreviewSelector>(parameters => parameters
                .Add(x => x.Items, [video])
                .Add(x => x.SelectedItem, video));

        Assert.Empty(component.FindAll("video"));
        Assert.Empty(component.FindAll("audio"));
        Assert.Single(component.FindAll("[role='option'][aria-selected='true']"));
    }

    [Fact]
    public void DetailsDialog_DataItemCopyActionsUseCidAndConfiguredGatewayLink()
    {
        const string cid = "QmCopyActions";
        ITypedBusinessObjectView mediaObject = CreateTypedObject(
            "post",
            "IPFS-backed post",
            new Dictionary<string, string>(StringComparer.Ordinal),
            [
                new BusinessObjectDataItem(
                    "IPFS photo",
                    BusinessObjectDataItemType.Image,
                    BusinessObjectDataItemStorage.Ipfs,
                    cid,
                    new Uri($"http://localhost:5081/ipfs-api/cat?arg={cid}"),
                    "image/png",
                    "ipfs-photo.png",
                    128,
                    IsMain: true)
            ]);
        var resolver = new IpfsGatewayDataItemUriResolver(
            new Uri("https://gateway.example/", UriKind.Absolute));

        IRenderedComponent<CascadingValue<IBusinessObjectDataItemUriResolver>> component =
            Render<CascadingValue<IBusinessObjectDataItemUriResolver>>(parameters => parameters
                .Add(x => x.Value, resolver)
                .AddChildContent<BusinessObjectLedgerDetailsDialogContent>(child => child
                    .Add(x => x.Object, mediaObject)));

        AngleSharp.Dom.IElement previewDownload = component.Find("[data-testid='cda-bo-dataitem-download']");
        Assert.Equal("ipfs-photo.png", previewDownload.GetAttribute("download"));
        Assert.False(previewDownload.HasAttribute("target"));

        component.Find("[data-testid='cda-bo-data-items-tab-button']").Click();
        Assert.Single(component.Find("[data-testid='cda-bo-data-items-tab']").QuerySelectorAll("[data-testid='cda-bo-data-item-copy-cid']"));
        Assert.Single(component.Find("[data-testid='cda-bo-data-items-tab']").QuerySelectorAll("[data-testid='cda-bo-data-item-copy-link']"));
        Assert.Empty(component.FindAll(".cda-bo-gallery-item__select button"));

        IReadOnlyList<IRenderedComponent<CopyButton>> copyButtons = component.FindComponents<CopyButton>();
        CopyButton cidButton = Assert.Single(copyButtons
            .Where(button => button.Instance.AriaLabel == "Copy CID for IPFS photo"))
            .Instance;
        CopyButton linkButton = Assert.Single(copyButtons
            .Where(button => button.Instance.AriaLabel == "Copy IPFS link for IPFS photo"))
            .Instance;

        Assert.Equal(cid, cidButton.Value);
        Assert.Equal("https://gateway.example/ipfs/QmCopyActions", linkButton.Value);
        Assert.Equal(CopyButtonDisplayMode.IconOnly, cidButton.DisplayMode);
        Assert.Equal(CopyButtonSize.ExtraSmall, linkButton.Size);

        AngleSharp.Dom.IElement download = component.Find("[data-testid='cda-bo-data-item-download']");
        Assert.Equal("https://gateway.example/ipfs/QmCopyActions", download.GetAttribute("href"));
        Assert.Equal("ipfs-photo.png", download.GetAttribute("download"));
        Assert.False(download.HasAttribute("target"));
    }

    [Fact]
    public void DataItemList_UsesUrlAndHashWordingForUrlStorage()
    {
        var item = new BusinessObjectDataItem(
            "Remote report",
            BusinessObjectDataItemType.Pdf,
            BusinessObjectDataItemStorage.Url,
            "report-sha256",
            new Uri("https://assets.example.test/reports/annual.pdf"),
            "application/pdf",
            "annual-report.pdf",
            4096);

        IRenderedComponent<BusinessObjectDataItemList> component =
            Render<BusinessObjectDataItemList>(parameters => parameters
                .Add(x => x.Items, [item]));

        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-copy-cid']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-copy-link']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-data-item-copy-hash']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-data-item-copy-url']"));
        Assert.Contains("URL /", component.Markup, StringComparison.Ordinal);

        IReadOnlyList<IRenderedComponent<CopyButton>> copyButtons = component.FindComponents<CopyButton>();
        CopyButton hashButton = Assert.Single(copyButtons
            .Where(button => button.Instance.AriaLabel == "Copy hash for Remote report"))
            .Instance;
        CopyButton urlButton = Assert.Single(copyButtons
            .Where(button => button.Instance.AriaLabel == "Copy URL for Remote report"))
            .Instance;

        Assert.Equal("report-sha256", hashButton.Value);
        Assert.Equal("https://assets.example.test/reports/annual.pdf", urlButton.Value);
        Assert.Equal("_blank", component.Find("[data-testid='cda-bo-data-item-open']").GetAttribute("target"));

        AngleSharp.Dom.IElement download = component.Find("[data-testid='cda-bo-data-item-download']");
        Assert.Equal("annual-report.pdf", download.GetAttribute("download"));
        Assert.False(download.HasAttribute("target"));
    }

    [Fact]
    public void DataItemList_ExposesOnlyHashActionForInlineStorage()
    {
        var item = new BusinessObjectDataItem(
            "Inline metadata",
            BusinessObjectDataItemType.Json,
            BusinessObjectDataItemStorage.Inline,
            "inline-sha256",
            new Uri("https://assets.example.test/should-not-be-used.json"),
            "application/json",
            "metadata.json",
            64);

        IRenderedComponent<BusinessObjectDataItemList> component =
            Render<BusinessObjectDataItemList>(parameters => parameters
                .Add(x => x.Items, [item]));

        Assert.Single(component.FindAll("[data-testid='cda-bo-data-item-copy-hash']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-copy-cid']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-copy-link']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-copy-url']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-open']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-download']"));
        Assert.Contains("Inline / 64 bytes", component.Markup, StringComparison.Ordinal);

        CopyButton hashButton = Assert.Single(component.FindComponents<CopyButton>()).Instance;
        Assert.Equal("Copy hash for Inline metadata", hashButton.AriaLabel);
        Assert.Equal("inline-sha256", hashButton.Value);
    }

    [Fact]
    public void MediaRenderer_ReusesStorageAwareDataItemActions()
    {
        ITypedBusinessObjectView mediaObject = CreateTypedObject(
            "post",
            "Shared media actions",
            new Dictionary<string, string>(StringComparer.Ordinal),
            [
                new BusinessObjectDataItem(
                    "Inline body",
                    BusinessObjectDataItemType.Text,
                    BusinessObjectDataItemStorage.Inline,
                    "inline-body-hash")
            ]);
        BusinessObjectRendererDescriptor descriptor =
            new MediaBusinessObjectRendererProvider().CreateDescriptor(mediaObject);

        IRenderedComponent<MediaBusinessObjectRenderer> component =
            Render<MediaBusinessObjectRenderer>(parameters => parameters
                .Add(x => x.Object, mediaObject)
                .Add(x => x.Descriptor, descriptor));

        Assert.Single(component.FindComponents<BusinessObjectDataItemActions>());
        Assert.Single(component.FindAll("[data-testid='cda-bo-data-item-copy-hash']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-open']"));
        Assert.Empty(component.FindAll("[data-testid='cda-bo-data-item-download']"));
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
            Render<BusinessObjectLedgerGallery>(parameters => parameters
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
                ["ticketDuration"] = "1 day",
                ["mintAuthorAddress"] = Alice.Value,
                ["used"] = "false",
                ["status"] = "FreeToUse"
            });

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, ticketObject)
                .Add(x => x.Workspace, CreateWorkspace(eventObject, ticketObject)));

        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-panel']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-card']"));
        Assert.Empty(component.FindAll(".cda-bo-selected-preview"));
        Assert.Contains("Conference Main Stage", component.Markup, StringComparison.Ordinal);
        Assert.Contains("A12", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Mint author address", component.Markup, StringComparison.Ordinal);
        Assert.Contains(Alice.Value, component.Markup, StringComparison.Ordinal);
        Assert.Contains("FreeToUse", component.Markup, StringComparison.Ordinal);
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
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
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
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, paymentObject)
                .Add(x => x.Workspace, CreateWorkspace(targetObject, paymentObject)));

        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-panel']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-related-card']"));
        Assert.Empty(component.FindAll(".cda-bo-selected-preview"));
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

    [Theory]
    [InlineData("post")]
    [InlineData("image")]
    [InlineData("message")]
    [InlineData("event")]
    [InlineData("ticket")]
    [InlineData("payment")]
    [InlineData("purchase")]
    [InlineData("receipt")]
    [InlineData("invoice")]
    [InlineData("product")]
    [InlineData("profile")]
    [InlineData("music")]
    public void DetailsDialog_UsesCompactSharedLayoutForEveryBuiltInFamily(string objectType)
    {
        ITypedBusinessObjectView businessObject = CreateTypedObject(
            objectType,
            $"{objectType} compact-layout proof",
            new Dictionary<string, string>(StringComparer.Ordinal));

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, businessObject));

        Assert.Single(component.FindAll("[data-testid='cda-bo-details-layout']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-overview-no-data']"));
        Assert.NotEmpty(component.FindComponents<CompactStat>());
        Assert.Empty(component.FindComponents<MetricCard>());
        Assert.Contains($"{objectType} compact-layout proof", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInFamilyCoverage_EnumeratesTheWholeCatalog()
    {
        string[] coveredFamilies =
        [
            "post",
            "image",
            "message",
            "event",
            "ticket",
            "payment",
            "purchase",
            "receipt",
            "invoice",
            "product",
            "profile",
            "music"
        ];

        Assert.Equal(
            BusinessObjectFamilyCatalog.Families
                .Select(definition => definition.ObjectType)
                .Order(StringComparer.Ordinal),
            coveredFamilies.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task RendererRegistry_ResolvesRuleSettingsMediaTypedCommerceAndGenericCategories()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        IBusinessObjectView rule = workspace.BusinessObjects.Single(item => item.ObjectType == "account-rule");
        IBusinessObjectView settings = workspace.BusinessObjects.Single(item => item.ObjectType == "account-settings");
        IBusinessObjectView media = workspace.BusinessObjects.OfType<IMediaBusinessObjectView>().First();
        IBusinessObjectView generic = workspace.BusinessObjects.Single(item => item.ObjectType == "inventory-lot");
        ITypedBusinessObjectView typed = CreateTypedObject(
            "invoice",
            "Typed renderer proof",
            new Dictionary<string, string>(StringComparer.Ordinal));
        ICommerceBusinessObjectView commerce = await CreatePaymentObjectAsync(typed);
        var registry = new BusinessObjectRendererRegistry(
        [
            new GenericBusinessObjectRendererProvider(),
            new MediaBusinessObjectRendererProvider(),
            new SettingsBusinessObjectRendererProvider(),
            new TypedFamilyBusinessObjectRendererProvider(),
            new CommerceBusinessObjectRendererProvider(),
            new RuleBusinessObjectRendererProvider()
        ]);

        Assert.Equal("account-rule", registry.Resolve(rule).Key);
        Assert.Equal("account-settings", registry.Resolve(settings).Key);
        Assert.Equal("media-dataitems", registry.Resolve(media).Key);
        Assert.Equal("typed-family", registry.Resolve(typed).Key);
        Assert.Equal("commerce-record", registry.Resolve(commerce).Key);
        Assert.Equal("generic", registry.Resolve(generic).Key);
    }

    [Fact]
    public async Task DetailsDialog_ExposesEverySpecializedRendererThroughTypeDetailsTab()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        IBusinessObjectView rule = workspace.BusinessObjects.Single(item => item.ObjectType == "account-rule");
        IBusinessObjectView settings = workspace.BusinessObjects.Single(item => item.ObjectType == "account-settings");
        IBusinessObjectView media = workspace.BusinessObjects.OfType<IMediaBusinessObjectView>().First();
        IBusinessObjectView generic = workspace.BusinessObjects.Single(item => item.ObjectType == "inventory-lot");
        ITypedBusinessObjectView typed = CreateTypedObject(
            "invoice",
            "Typed dialog renderer proof",
            new Dictionary<string, string>(StringComparer.Ordinal));
        ICommerceBusinessObjectView commerce = await CreatePaymentObjectAsync(typed);

        (IBusinessObjectView Object, string RendererKey)[] cases =
        [
            (rule, "account-rule"),
            (settings, "account-settings"),
            (media, "media-dataitems"),
            (typed, "typed-family"),
            (commerce, "commerce-record"),
            (generic, "generic")
        ];

        foreach ((IBusinessObjectView businessObject, string rendererKey) in cases)
        {
            using IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
                Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                    .Add(x => x.Object, businessObject));

            component.Find("[data-testid='cda-bo-type-details-tab']").Click();
            Assert.Single(component.FindAll("[data-testid='cda-bo-type-details-tab']"));
            Assert.Single(component.FindAll("[data-testid='cda-bo-type-renderer']"));
            Assert.Single(component.FindAll($"[data-testid='cda-bo-type-renderer'] [data-renderer='{rendererKey}']"));
        }
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    public void DetailsDialog_AlwaysExposesDataItemsTabWithCount(int itemCount, string expectedBadge)
    {
        IReadOnlyList<BusinessObjectDataItem> dataItems = itemCount == 0
            ? []
            :
            [
                new BusinessObjectDataItem(
                    "Discoverable attachment",
                    BusinessObjectDataItemType.Image,
                    BusinessObjectDataItemStorage.Ipfs,
                    "QmDiscoverable",
                    new Uri("https://gateway.example/ipfs/QmDiscoverable"),
                    "image/png",
                    "discoverable.png",
                    256,
                    IsMain: true)
            ];
        ITypedBusinessObjectView businessObject = CreateTypedObject(
            "post",
            $"Data item tab {itemCount}",
            new Dictionary<string, string>(StringComparer.Ordinal),
            dataItems);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, businessObject));

        IRenderedComponent<TabsItem> tab = Assert.Single(component.FindComponents<TabsItem>()
            .Where(candidate => candidate.Instance.Text == "Data items"));
        Assert.Equal(expectedBadge, tab.Instance.BadgeText);
        component.Find("[data-testid='cda-bo-data-items-tab-button']").Click();
        Assert.Single(component.FindAll("[data-testid='cda-bo-data-items-tab-list']"));
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
        BusinessObjectId objectId = ConfirmedObjectId($"{objectType}:{BusinessObjectDisplaySlug.Normalize(name)}");

        return new GenericTypedBusinessObjectView(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('8', 64)),
            objectId.RequireOutPoint(),
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
        BusinessObjectId paymentId = ConfirmedObjectId("payment:detail-layout");
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            Bob,
            Alice,
            targetObject.ObjectId,
            targetVersion: targetObject.Version,
            targetOutPoint: targetObject.CurrentOutPoint,
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            paymentId.RequireOutPoint().TxId,
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
            objectId.RequireOutPoint(),
            stateJson,
            $"ledger://{objectId.Value}",
            TransactionDataItemType.BusinessObject,
            Now);

    private static BusinessObjectId ConfirmedObjectId(string seed)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return BusinessObjectId.FromOutPoint(new TxId(Convert.ToHexString(hash).ToLowerInvariant()), 0);
    }

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
