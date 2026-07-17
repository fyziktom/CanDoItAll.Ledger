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
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectCommerceUiComponentTests : TestContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("test-ledger"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly BusinessObjectId ListedObjectId = ConfirmedObjectId("post:listed-commerce-post");
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
        Assert.Contains(
            component.FindComponents<CopyableMonoValue>(),
            value => value.Instance.Value.Contains("/buy-bo/tx/", StringComparison.Ordinal));
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
    public void GalleryCard_UsesBoundedFallbackAndExplicitCompactTags()
    {
        ITypedBusinessObjectView post = CreatePost(
            new BusinessObjectId("bo:post:compact-card"),
            "Compact gallery card",
            ListedAtUtc);

        IRenderedComponent<BusinessObjectLedgerGallery> component =
            RenderComponent<BusinessObjectLedgerGallery>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(post)));

        IElement card = component.Find("[data-testid='cda-bo-gallery-card']");
        IElement operation = component.Find("[data-testid='cda-bo-card-operation']");
        IElement tag = Assert.Single(component.FindAll("[data-testid='cda-bo-card-tag']"));

        Assert.Contains("cda-bo-gallery-card", card.ClassList);
        Assert.Single(card.QuerySelectorAll("[data-testid='cda-bo-card-fallback']"));
        Assert.Equal("Created BO", operation.GetAttribute("aria-label"));
        Assert.Equal("commerce", tag.TextContent.Trim(), ignoreCase: true);
        Assert.DoesNotContain("BO", card.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("#Create", card.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#v1", card.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GalleryCard_AnchorsTransactionFooterAndUsesTinyCopyActionWithoutTags()
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            "No-tag card",
            "A short description.",
            "A short description.",
            string.Empty,
            []);
        ITypedBusinessObjectView post = CreateTypedView(
            new BusinessObjectId("bo:post:no-tag-card"),
            "post",
            BusinessObjectPayloadBuilder.Serialize(payload));

        IRenderedComponent<BusinessObjectGalleryCard> component =
            RenderComponent<BusinessObjectGalleryCard>(parameters => parameters
                .Add(x => x.Item, post)
                .Add(x => x.Workspace, CreateWorkspace(post)));

        IElement footer = component.Find("[data-testid='cda-bo-card-footer']");
        IElement body = component.Find(".cda-bo-gallery-body");
        CopyableMonoValue transactionId = Assert.Single(component.FindComponents<CopyableMonoValue>()).Instance;
        CopyButton copy = Assert.Single(
            component.FindComponents<CopyButton>(),
            candidate => candidate.Instance.AriaLabel == "Copy transaction ID").Instance;
        string fullTransactionId = post.CurrentOutPoint.TxId.Value;
        string expectedTransactionId = fullTransactionId.Length > 17
            ? $"{fullTransactionId[..8]}...{fullTransactionId[^6..]}"
            : fullTransactionId;

        Assert.Empty(component.FindAll("[data-testid='cda-bo-card-tag']"));
        Assert.Equal("cda-bo-card-footer", body.LastElementChild?.GetAttribute("data-testid"));
        Assert.Contains("cda-bo-gallery-footer", footer.ClassList);
        Assert.Equal(fullTransactionId, transactionId.Value);
        Assert.Equal(expectedTransactionId, component.Find(".cda-bo-gallery-txid .cda-text-block--mono").TextContent.Trim());
        Assert.Equal(fullTransactionId, copy.Value);
        Assert.Equal(CopyButtonDisplayMode.IconOnly, copy.DisplayMode);
        Assert.Equal(CopyButtonSize.ExtraSmall, copy.Size);
        Assert.Equal(ListedAtUtc.ToString("O"), footer.QuerySelector("time")?.GetAttribute("datetime"));
        Assert.DoesNotContain(
            component.FindComponents<TooltipTarget>(),
            candidate => candidate.Instance.TestId == "cda-bo-card-description-tooltip");
    }

    [Fact]
    public void GalleryCard_LimitsTagsAndShowsTheRemainderThroughTooltipService()
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            "Tagged card",
            "A short description.",
            "A short description.",
            "#alpha #beta #gamma #delta #epsilon #zeta #eta #theta #iota",
            []);
        ITypedBusinessObjectView post = CreateTypedView(
            new BusinessObjectId("bo:post:tagged-card"),
            "post",
            BusinessObjectPayloadBuilder.Serialize(payload));

        IRenderedComponent<BusinessObjectGalleryCard> component =
            RenderComponent<BusinessObjectGalleryCard>(parameters => parameters
                .Add(x => x.Item, post));

        IReadOnlyList<IElement> tags = component.FindAll("[data-testid='cda-bo-card-tag']");
        IElement overflow = component.Find("[data-testid='cda-bo-card-tags-overflow']");
        IRenderedComponent<TooltipTarget> tooltip = Assert.Single(
            component.FindComponents<TooltipTarget>(),
            candidate => candidate.Instance.TestId == "cda-bo-card-tags-overflow-tooltip");

        Assert.Equal(5, tags.Count);
        Assert.Equal(["alpha", "beta", "gamma", "delta", "epsilon"], tags.Select(tag => tag.TextContent.Trim()).ToArray());
        Assert.All(tags, tag => Assert.DoesNotContain('#', tag.TextContent));
        Assert.Equal("...", overflow.TextContent.Trim());
        Assert.Equal("More tags: zeta, eta, theta, iota", tooltip.Instance.Text);

        tooltip.Find("span").TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 1, ClientY = 1 });
        Assert.Equal("More tags: zeta, eta, theta, iota", Services.GetRequiredService<TooltipService>().Current?.Text);
    }

    [Fact]
    public void GalleryCard_TruncatesLongDescriptionsAndUsesPastTenseOperationTooltip()
    {
        const string description = "This intentionally long business object description contains enough detail to exceed the compact gallery card limit while preserving the complete content for the tooltip service.";
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            "Updated card",
            description,
            description,
            "#alpha",
            []);
        ITypedBusinessObjectView post = CreateTypedView(
            new BusinessObjectId("bo:post:updated-card"),
            "post",
            BusinessObjectPayloadBuilder.Serialize(payload),
            operation: BusinessObjectOperation.Update);

        IRenderedComponent<BusinessObjectGalleryCard> component =
            RenderComponent<BusinessObjectGalleryCard>(parameters => parameters
                .Add(x => x.Item, post));

        IElement renderedDescription = component.Find("[data-testid='cda-bo-card-description']");
        IElement operation = component.Find("[data-testid='cda-bo-card-operation']");
        IReadOnlyList<IRenderedComponent<TooltipTarget>> tooltips = component.FindComponents<TooltipTarget>();
        IRenderedComponent<TooltipTarget> descriptionTooltip = Assert.Single(
            tooltips,
            candidate => candidate.Instance.TestId == "cda-bo-card-description-tooltip");
        TooltipTarget operationTooltip = Assert.Single(
            tooltips,
            candidate => candidate.Instance.TestId == "cda-bo-card-operation-tooltip").Instance;

        Assert.Equal($"{description[..96].TrimEnd()}...", renderedDescription.TextContent);
        Assert.Equal(description, descriptionTooltip.Instance.Text);
        Assert.Equal("Updated BO", operationTooltip.Text);
        Assert.Equal("Updated BO", operation.GetAttribute("aria-label"));
        Assert.Null(operation.GetAttribute("title"));

        descriptionTooltip.Find("span").TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 1, ClientY = 1 });
        Assert.Equal(description, Services.GetRequiredService<TooltipService>().Current?.Text);
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

        string[] headings = component.FindAll("[data-testid='cda-bo-gallery-card'] h3")
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

        string[] headings = component.FindAll("[data-testid='cda-bo-gallery-card'] h3")
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

        string[] headings = component.FindAll("[data-testid='cda-bo-gallery-card'] h3")
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

        IElement card = Assert.Single(component.FindAll("[data-testid='cda-bo-gallery-card']"));

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
            "business-object",
            $$"""{"format":"{{SecureLedgerMessages.Format}}"}""");

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

        IElement card = Assert.Single(component.FindAll("[data-testid='cda-bo-gallery-card']"));

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

        SelectOptionByText(component, "[data-testid='bo-sale-mode']", "Copies");

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

        SelectOptionByText(component, "[data-testid='bo-sale-mode']", "Copies");
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
    public async Task CommerceDashboard_RendersStructuredPaymentRowWithTargetContext()
    {
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync();
        DateTimeOffset activityAt = ListedAtUtc.AddMinutes(7);

        IRenderedComponent<BusinessObjectCommerceDashboard> component =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(
                    x => x.Workspace,
                    CreateWorkspace(
                        [payment],
                        [CreateObjectTransaction(payment, activityAt)])));

        IElement row = FindCommerceRow(component, "payment");
        IElement kind = row.QuerySelector("[data-testid='cda-commerce-record']")!;
        IElement[] cells = row.QuerySelectorAll("td").ToArray();
        IElement timestamp = Assert.IsAssignableFrom<IElement>(cells[0].QuerySelector("time"));

        Assert.Equal(activityAt.ToString("O"), timestamp.GetAttribute("datetime"));
        Assert.Equal("payment", kind.GetAttribute("data-commerce-kind"));
        Assert.Equal(payment.ObjectId.Value, kind.GetAttribute("data-record-id"));
        Assert.Contains("Payment", cells[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Pending", cells[2].TextContent, StringComparison.Ordinal);
        Assert.NotNull(cells[2].QuerySelector(".cda-status-badge--tone-warning"));
        Assert.Contains("25 MAIN", cells[3].TextContent, StringComparison.Ordinal);
        Assert.NotNull(cells[3].QuerySelector(".cda-status-badge--tone-info"));
        Assert.Contains(
            cells[5].QuerySelectorAll("[title]"),
            element => string.Equals(element.GetAttribute("title"), ListedObjectId.Value, StringComparison.Ordinal));
        Assert.NotNull(cells[5].QuerySelector("[aria-label='Copy target business object id']"));
    }

    [Fact]
    public async Task CommerceDashboard_UnifiesPaymentAndReceiptRecordsWithKindMarkers()
    {
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync();
        ICommerceBusinessObjectView receipt = await CreateReceiptViewAsync();

        IRenderedComponent<BusinessObjectCommerceDashboard> component =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(payment, receipt)));

        IElement grid = component.Find("[data-testid='cda-commerce-record-grid']");

        Assert.Equal(2, grid.QuerySelectorAll("tbody > tr").Count());
        Assert.Single(grid.QuerySelectorAll("[data-commerce-kind='payment']"));
        Assert.Single(grid.QuerySelectorAll("[data-commerce-kind='receipt']"));
        Assert.Empty(component.FindAll("[data-testid='cda-commerce-payments']"));
        Assert.Empty(component.FindAll("[data-testid='cda-commerce-receipts']"));
    }

    [Fact]
    public async Task CommerceDashboard_OrdersPaymentAndReceiptRecordsByCurrentTransactionTimestamp()
    {
        BusinessObjectId olderPaymentId = ConfirmedObjectId("payment:older");
        BusinessObjectId newerPaymentId = ConfirmedObjectId("payment:newer");
        BusinessObjectId olderPaymentTargetId = ConfirmedObjectId("post:older-payment-target");
        BusinessObjectId newerPaymentTargetId = ConfirmedObjectId("post:newer-payment-target");
        BusinessObjectId olderReceiptPaymentId = ConfirmedObjectId("payment:older-receipt");
        BusinessObjectId newerReceiptPaymentId = ConfirmedObjectId("payment:newer-receipt");
        BusinessObjectId olderReceiptTargetId = ConfirmedObjectId("post:older-receipt-target");
        BusinessObjectId newerReceiptTargetId = ConfirmedObjectId("post:newer-receipt-target");
        ICommerceBusinessObjectView olderPayment = await CreatePaymentViewAsync(
            olderPaymentId,
            olderPaymentTargetId);
        ICommerceBusinessObjectView newerPayment = await CreatePaymentViewAsync(
            newerPaymentId,
            newerPaymentTargetId);
        ICommerceBusinessObjectView olderReceipt = await CreateReceiptViewAsync(
            olderReceiptPaymentId,
            olderReceiptTargetId);
        ICommerceBusinessObjectView newerReceipt = await CreateReceiptViewAsync(
            newerReceiptPaymentId,
            newerReceiptTargetId);

        IRenderedComponent<BusinessObjectCommerceDashboard> component =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(
                    x => x.Workspace,
                    CreateWorkspace(
                        [olderPayment, newerPayment, olderReceipt, newerReceipt],
                        [
                            CreateObjectTransaction(olderPayment, ListedAtUtc.AddMinutes(1)),
                            CreateObjectTransaction(newerPayment, ListedAtUtc.AddMinutes(3)),
                            CreateObjectTransaction(olderReceipt, ListedAtUtc.AddMinutes(2)),
                            CreateObjectTransaction(newerReceipt, ListedAtUtc.AddMinutes(4))
                        ])));

        string[] recordIds = component.Find("[data-testid='cda-commerce-record-grid']")
            .QuerySelectorAll("[data-testid='cda-commerce-record']")
            .Select(marker => marker.GetAttribute("data-record-id") ?? string.Empty)
            .ToArray();

        Assert.Equal(
            [
                newerReceipt.ObjectId.Value,
                newerPayment.ObjectId.Value,
                olderReceipt.ObjectId.Value,
                olderPayment.ObjectId.Value
            ],
            recordIds);
    }

    [Fact]
    public async Task CommerceDashboard_ShowsRelatedBusinessObjectActionForPaymentAndReceiptRecords()
    {
        ITypedBusinessObjectView listedPost = CreateListedPost();
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync();
        ICommerceBusinessObjectView receipt = await CreateReceiptViewAsync();

        IRenderedComponent<BusinessObjectCommerceDashboard> component =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(listedPost, payment, receipt)));

        IRefreshableElementCollection<IElement> relatedButtons = component.FindAll("[data-testid='cda-commerce-related-details']");

        Assert.Equal(2, relatedButtons.Count);
        Assert.All(relatedButtons, button =>
        {
            Assert.False(button.HasAttribute("disabled"));
            Assert.Equal("Open related business object", button.GetAttribute("aria-label"));
            Assert.Contains("Open Listed Commerce Post", button.GetAttribute("title") ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CommerceDashboard_UsesSavedDirectoryNameForCounterparty()
    {
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync();
        var directoryEntry = new AddressDirectoryEntry(
            Bob.Value,
            "Bob commerce partner",
            AddressDirectoryEntryKind.External,
            Favorite: true);

        IRenderedComponent<BusinessObjectCommerceDashboard> component =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(x => x.Workspace, CreateWorkspace(payment))
                .Add(x => x.DirectoryEntries, [directoryEntry]));

        IElement row = FindCommerceRow(component, "payment");
        IElement counterpartyCell = row.QuerySelectorAll("td")[4];
        IElement counterparty = Assert.IsAssignableFrom<IElement>(counterpartyCell.QuerySelector("[title]"));

        Assert.Contains("Bob commerce partner", counterpartyCell.TextContent, StringComparison.Ordinal);
        Assert.Contains(Bob.Value, counterparty.GetAttribute("title") ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommerceDashboard_CompactAndDetailedFieldsExposeDifferentInformationDensity()
    {
        ICommerceBusinessObjectView receipt = await CreateReceiptViewAsync();
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(receipt);

        IRenderedComponent<BusinessObjectCommerceDashboard> compact =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(x => x.Workspace, workspace));
        IRenderedComponent<BusinessObjectCommerceDashboard> detailed =
            RenderComponent<BusinessObjectCommerceDashboard>(parameters => parameters
                .Add(x => x.Workspace, workspace)
                .Add(x => x.Fields, BusinessObjectCommerceFields.Detailed));

        string[] compactHeaders = CommerceHeaders(compact);
        string[] detailedHeaders = CommerceHeaders(detailed);

        Assert.DoesNotContain("Sale mode", compactHeaders);
        Assert.DoesNotContain("Delivered object", compactHeaders);
        Assert.DoesNotContain("Transactions", compactHeaders);
        Assert.DoesNotContain("Rejection", compactHeaders);
        Assert.Contains("Sale mode", detailedHeaders);
        Assert.Contains("Delivered object", detailedHeaders);
        Assert.Contains("Transactions", detailedHeaders);
        Assert.Contains("Rejection", detailedHeaders);
        Assert.Empty(compact.FindAll("[aria-label='Copy current record transaction id']"));
        Assert.Empty(compact.FindAll("[aria-label='Copy payment transaction id']"));
        Assert.Empty(compact.FindAll("[aria-label='Copy dispatch transaction id']"));
        Assert.Single(detailed.FindAll("[aria-label='Copy current record transaction id']"));
        Assert.Single(detailed.FindAll("[aria-label='Copy payment transaction id']"));
        Assert.Single(detailed.FindAll("[aria-label='Copy dispatch transaction id']"));

        IElement transactionCell = Assert.Single(
            detailed.Find("[data-testid='cda-commerce-record-grid']")
                .QuerySelectorAll("tbody > tr td"),
            cell => cell.TextContent.Contains("Record", StringComparison.Ordinal)
                && cell.TextContent.Contains("Payment", StringComparison.Ordinal)
                && cell.TextContent.Contains("Dispatch", StringComparison.Ordinal));

        Assert.Contains("payment-tx", transactionCell.TextContent, StringComparison.Ordinal);
        Assert.Contains("dispatch-tx", transactionCell.TextContent, StringComparison.Ordinal);
        Assert.Contains(receipt.CurrentOutPoint.TxId.Value[..8], transactionCell.TextContent, StringComparison.Ordinal);
        Assert.NotEqual("payment-tx", receipt.CurrentOutPoint.TxId.Value);
        Assert.NotEqual("dispatch-tx", receipt.CurrentOutPoint.TxId.Value);
    }

    [Fact]
    public async Task CommerceSummary_RendersPaymentAsOneCompactPartyFlowAndReferenceTable()
    {
        ICommerceBusinessObjectView payment = await CreatePaymentViewAsync();

        IRenderedComponent<BusinessObjectCommerceSummary> component =
            RenderComponent<BusinessObjectCommerceSummary>(parameters => parameters
                .Add(x => x.Object, payment));

        Assert.Single(component.FindAll("[data-testid='cda-commerce-record-summary']"));
        Assert.Single(component.FindAll("[data-testid='cda-commerce-party-flow']"));
        Assert.Single(component.FindAll("[data-testid='cda-commerce-reference-grid']"));
        Assert.Contains("Target object", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Created", component.Markup, StringComparison.Ordinal);
        Assert.Single(component.FindComponents<StatusBadge>(), badge => badge.Instance.Text == "Pending");
        string[] copyValues = component.FindComponents<CopyableMonoValue>()
            .Select(value => value.Instance.Value)
            .ToArray();
        Assert.Contains(Alice.Value, copyValues);
        Assert.Contains(Bob.Value, copyValues);
        Assert.Contains(ListedObjectId.Value, copyValues);
        Assert.Empty(component.FindComponents<CompactStat>());
        Assert.Empty(component.FindComponents<MetricCard>());
    }

    [Fact]
    public async Task CommerceSummary_RendersReceiptDeliveryAndDispatchWithoutFactCardMosaic()
    {
        ICommerceBusinessObjectView receipt = await CreateReceiptViewAsync();

        IRenderedComponent<BusinessObjectCommerceSummary> component =
            RenderComponent<BusinessObjectCommerceSummary>(parameters => parameters
                .Add(x => x.Object, receipt));

        Assert.Single(component.FindComponents<StatusBadge>(), badge => badge.Instance.Text == "Receipt");
        Assert.Single(component.FindComponents<StatusBadge>(), badge => badge.Instance.Text == "Dispatched");
        Assert.Contains("Delivered object", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Dispatch transaction", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Issued", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindComponents<CompactStat>());
        Assert.Empty(component.FindComponents<MetricCard>());
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
        string objectType = "post",
        string? stateJsonOverride = null)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            name,
            $"{name} short description",
            $"{name} long description",
            "#commerce",
            []);
        string stateJson = stateJsonOverride ?? BusinessObjectPayloadBuilder.Serialize(payload);
        return CreateTypedView(objectId, objectType, stateJson, createdAt);
    }

    private static async Task<ICommerceBusinessObjectView> CreatePaymentViewAsync(
        BusinessObjectId? paymentId = null,
        BusinessObjectId? targetObjectId = null,
        DateTimeOffset? createdAt = null)
    {
        BusinessObjectId effectivePaymentId = paymentId ?? ConfirmedObjectId("payment:listed-commerce-post");
        DateTimeOffset effectiveCreatedAt = createdAt ?? ListedAtUtc;
        BusinessObjectPaymentRecord payment = CreatePaymentRecord(effectivePaymentId, targetObjectId, effectiveCreatedAt);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment));
        var caster = new CommerceBusinessObjectCaster(new ManualTimeProvider(effectiveCreatedAt.AddMinutes(3)));
        IBusinessObjectView view = await caster.CastAsync(
            CreateSource(effectivePaymentId, "payment", stateJson, effectiveCreatedAt),
            CancellationToken.None);

        return Assert.IsAssignableFrom<ICommerceBusinessObjectView>(view);
    }

    private static async Task<ICommerceBusinessObjectView> CreateReceiptViewAsync(
        BusinessObjectId? paymentId = null,
        BusinessObjectId? targetObjectId = null,
        DateTimeOffset? createdAt = null)
    {
        BusinessObjectId effectivePaymentId = paymentId ?? ConfirmedObjectId("payment:listed-commerce-post");
        BusinessObjectId effectiveTargetId = targetObjectId ?? ListedObjectId;
        DateTimeOffset effectiveCreatedAt = createdAt ?? ListedAtUtc.AddMinutes(4);
        BusinessObjectId receiptId = ConfirmedObjectId($"receipt:{effectivePaymentId.Value}:{effectiveTargetId.Value}");
        BusinessObjectPaymentRecord payment = CreatePaymentRecord(effectivePaymentId, effectiveTargetId, effectiveCreatedAt) with
        {
            Status = BusinessObjectCommerceRecordStatus.Dispatched,
            DeliveredObjectId = effectiveTargetId.Value,
            DispatchTxId = "dispatch-tx",
            ReceiptObjectId = receiptId.Value
        };
        BusinessObjectReceiptRecord receipt = BusinessObjectCommercePayloadBuilder.CreateReceiptRecord(
            payment,
            effectiveTargetId,
            new TxId("dispatch-tx"),
            effectiveCreatedAt);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreateReceiptPayload(receipt));
        var caster = new CommerceBusinessObjectCaster(new ManualTimeProvider(effectiveCreatedAt));
        IBusinessObjectView view = await caster.CastAsync(
            CreateSource(receiptId, "receipt", stateJson, effectiveCreatedAt),
            CancellationToken.None);

        return Assert.IsAssignableFrom<ICommerceBusinessObjectView>(view);
    }

    private static BusinessObjectPaymentRecord CreatePaymentRecord(
        BusinessObjectId paymentId,
        BusinessObjectId? targetObjectId = null,
        DateTimeOffset? createdAt = null)
    {
        BusinessObjectId effectiveTargetId = targetObjectId ?? ListedObjectId;
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            Bob,
            Alice,
            effectiveTargetId,
            targetVersion: 1,
            targetOutPoint: effectiveTargetId.RequireOutPoint(),
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            new TxId("payment-tx"),
            createdAt ?? ListedAtUtc);
        return payment with { PaymentObjectId = paymentId.Value };
    }

    private static ITypedBusinessObjectView CreateTypedView(
        BusinessObjectId objectId,
        string objectType,
        string stateJson,
        DateTimeOffset? createdAt = null,
        BusinessObjectOperation operation = BusinessObjectOperation.Create)
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
            operation,
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
        string stateJson,
        DateTimeOffset? createdAt = null)
        => new(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('2', 64)),
            OutPointFor(objectType, objectId),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject,
            createdAt ?? ListedAtUtc);

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

    private static BusinessObjectId ConfirmedObjectId(string seed)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return BusinessObjectId.FromOutPoint(new TxId(Convert.ToHexString(hash).ToLowerInvariant()), 0);
    }

    private static string BuyPathFor(OutPoint outPoint)
        => $"/buy-bo/tx/{Uri.EscapeDataString(outPoint.TxId.Value)}/{outPoint.OutputIndex}";

    private static IElement FindCommerceRow(IRenderedFragment component, string kind)
    {
        IElement marker = Assert.Single(
            component.FindAll("[data-testid='cda-commerce-record']"),
            element => string.Equals(element.GetAttribute("data-commerce-kind"), kind, StringComparison.Ordinal));
        IElement cell = Assert.IsAssignableFrom<IElement>(marker.ParentElement);
        return Assert.IsAssignableFrom<IElement>(cell.ParentElement);
    }

    private static string[] CommerceHeaders(IRenderedFragment component)
        => component.Find("[data-testid='cda-commerce-record-grid']")
            .QuerySelectorAll("thead th")
            .Select(header => header.TextContent.Trim())
            .ToArray();

    private static void SelectOptionByText(IRenderedFragment component, string selector, string text)
    {
        IElement select = component.Find(selector);
        IElement option = Assert.Single(
            select.QuerySelectorAll("option"),
            candidate => candidate.TextContent.Equals(text, StringComparison.Ordinal));

        select.Change(option.GetAttribute("value"));
    }

    private static IReadOnlyList<EconomyTokenOption> CreateTokenOptions()
        =>
        [
            new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(1000), IsMain: true)
        ];
}
