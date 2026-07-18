using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectDataItemMintingComponentTests : BunitContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("data-item-mint-test"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-data-item-mint-public"));
    private static readonly DateTimeOffset ViewedAtUtc = DateTimeOffset.Parse("2026-07-17T10:00:00Z");

    public BusinessObjectDataItemMintingComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddEconomySsrAccountWorkspaceAdapters();
    }

    [Fact]
    public async Task Wizard_AppendsSeparateMixedFileUploads_SelectsMainAndRemovesAnItem()
    {
        var uploadService = new RecordingUploadService();
        var stagedDrafts = new List<BusinessObjectTransactionDraft>();
        Services.AddSingleton<IBusinessObjectDataItemUploadService>(uploadService);
        BusinessObjectCreationWizardModel model = new(
            "image",
            Step: 1,
            "Mixed data item image",
            "#image #mixed",
            "An image business object with mixed data items.",
            string.Empty,
            [],
            null);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, CreateWorkspace())
                .Add(
                    x => x.StageBusinessObjectRequested,
                    EventCallback.Factory.Create<BusinessObjectTransactionDraft>(
                        this,
                        draft =>
                        {
                            stagedDrafts.Add(draft);
                            return Task.CompletedTask;
                        })));

        IElement uploadInput = component.Find("[data-testid='bo-upload-input']");
        Assert.Contains(".svg", uploadInput.GetAttribute("accept"), StringComparison.Ordinal);
        Assert.Contains(".txt", uploadInput.GetAttribute("accept"), StringComparison.Ordinal);

        Upload(component, InputFileContent.CreateFromText("plain text", "notes.txt", contentType: "text/plain"));
        Upload(component, InputFileContent.CreateFromText("<svg xmlns='http://www.w3.org/2000/svg'></svg>", "hero.svg", contentType: "image/svg+xml"));
        Upload(component, InputFileContent.CreateFromText("%PDF-1.4", "report.pdf", contentType: "application/pdf"));
        Upload(component, InputFileContent.CreateFromText("{\"status\":\"ok\"}", "metadata.json", contentType: "application/json"));

        Assert.Equal(4, uploadService.FileNames.Count);
        Assert.Equal(4, component.FindAll("[data-testid='bo-upload-item']").Count);
        IElement uploadStatuses = component.Find("[data-testid='bo-upload-statuses']");
        Assert.Contains("4 files", uploadStatuses.TextContent, StringComparison.Ordinal);
        Assert.Contains("Image", uploadStatuses.TextContent, StringComparison.Ordinal);
        Assert.Contains("IPFS CID", uploadStatuses.TextContent, StringComparison.Ordinal);
        Assert.Empty(component.FindComponents<SummaryTiles>());
        Assert.Empty(component.FindComponents<SummaryTile>());
        Assert.Empty(component.FindComponents<SectionCard>());
        Assert.True(
            component.Markup.IndexOf("data-testid=\"bo-upload-item-list\"", StringComparison.Ordinal)
            < component.Markup.IndexOf("data-testid=\"bo-upload-main-preview\"", StringComparison.Ordinal));
        Assert.Equal(
            ["notes.txt", "hero.svg", "report.pdf", "metadata.json"],
            component.FindAll("[data-testid='bo-upload-item']")
                .Select(item => item.GetAttribute("data-item-name") ?? string.Empty)
                .ToArray());

        IElement heroSelector = FindItemAction(component, "bo-upload-item-select", "hero.svg");
        await heroSelector.ClickAsync(new MouseEventArgs());
        Assert.Equal("true", FindItemAction(component, "bo-upload-item-select", "hero.svg").GetAttribute("aria-pressed"));
        Assert.Equal("false", FindItemAction(component, "bo-upload-item-select", "notes.txt").GetAttribute("aria-pressed"));
        IRenderedComponent<BusinessObjectDataItemInteractivePreview> uploadViewer =
            Assert.Single(component.FindComponents<BusinessObjectDataItemInteractivePreview>());
        Assert.Equal("hero.svg", uploadViewer.Instance.Item.Name);
        Assert.Single(component.FindAll("[aria-label='Zoom in']"));
        Assert.Single(component.FindAll("[aria-label='Zoom out']"));
        Assert.Single(component.FindAll("[aria-label='Reset view']"));

        await FindItemAction(component, "bo-upload-item-remove", "metadata.json").ClickAsync(new MouseEventArgs());
        Assert.Equal(3, component.FindAll("[data-testid='bo-upload-item']").Count);
        Assert.Empty(component.FindAll("[data-testid='bo-upload-item'][data-item-name='metadata.json']"));

        await ClickButtonAsync(component, "Next");
        Assert.Empty(component.FindComponents<SectionCard>());
        Assert.DoesNotContain("Describe the business object and fill any fields", component.Markup, StringComparison.Ordinal);
        await ClickButtonAsync(component, "Next");

        IElement finishPreview = component.Find("[data-testid='bo-finish-preview']");
        Assert.Equal("DIV", finishPreview.TagName);
        Assert.Equal("hero.svg", finishPreview.QuerySelector("img")?.GetAttribute("alt"));
        Assert.Single(finishPreview.QuerySelectorAll("[data-testid='bo-finish-preview-details']"));
        Assert.Contains("3 files", component.Find("[data-testid='bo-finish-statuses']").TextContent, StringComparison.Ordinal);
        Assert.Empty(component.FindComponents<SummaryTiles>());
        Assert.Empty(component.FindComponents<SummaryTile>());
        Assert.Empty(component.FindComponents<SectionCard>());
        Assert.DoesNotContain("Review the preview, metadata, and creation source", component.Markup, StringComparison.Ordinal);
        Assert.Equal(
            "hero.svg",
            Assert.Single(component.FindComponents<BusinessObjectDataItemInteractivePreview>()).Instance.Item.Name);
        Assert.Contains(
            "available-token-selector--compact",
            component.Find("[data-testid='bo-create-token-selector']").ClassList);

        await ClickButtonAsync(component, "Stage BO Transaction");

        BusinessObjectTransactionDraft draft = Assert.Single(stagedDrafts);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(draft.StateJson, "image");
        IReadOnlyList<BusinessObjectDataItem> items = payload.DataItems ?? [];
        Assert.Equal(3, items.Count);
        Assert.Equal(["notes.txt", "hero.svg", "report.pdf"], items.Select(item => item.Name).ToArray());
        Assert.Equal(BusinessObjectDataItemType.Text, items[0].Type);
        Assert.Equal(BusinessObjectDataItemType.Image, items[1].Type);
        Assert.Equal(BusinessObjectDataItemType.Pdf, items[2].Type);
        Assert.Single(items, item => item.IsMain);
        Assert.True(items[1].IsMain);
    }

    [Fact]
    public async Task Wizard_RemovingMainItem_SelectsTheNearestRemainingItem()
    {
        BusinessObjectDataItem first = CreateStoredItem("first.txt", "QmFirst", "text/plain", isMain: true);
        BusinessObjectDataItem second = CreateStoredItem("second.json", "QmSecond", "application/json");
        BusinessObjectCreationWizardModel model = new(
            "post",
            Step: 1,
            "Removal fallback",
            "#post",
            "Removal fallback test.",
            string.Empty,
            [first, second],
            first);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, CreateWorkspace()));

        await FindItemAction(component, "bo-upload-item-remove", "first.txt").ClickAsync(new MouseEventArgs());

        IElement remainingSelector = FindItemAction(component, "bo-upload-item-select", "second.json");
        Assert.Equal("true", remainingSelector.GetAttribute("aria-pressed"));
        Assert.Single(component.FindAll("[data-testid='bo-upload-item']"));
    }

    private static void Upload(
        IRenderedComponent<BusinessObjectCreationWizard> component,
        InputFileContent file)
        => component.FindComponent<InputFile>().UploadFiles(file);

    private static IElement FindItemAction(
        IRenderedComponent<BusinessObjectCreationWizard> component,
        string testId,
        string fileName)
        => component.FindAll($"[data-testid='{testId}']")
            .Single(item => string.Equals(item.GetAttribute("data-item-name"), fileName, StringComparison.Ordinal));

    private static async Task ClickButtonAsync(
        IRenderedComponent<BusinessObjectCreationWizard> component,
        string text)
    {
        IElement button = component.FindAll("button")
            .Single(candidate => candidate.TextContent.Contains(text, StringComparison.Ordinal));
        await button.ClickAsync(new MouseEventArgs());
    }

    private static BusinessObjectDataItem CreateStoredItem(
        string fileName,
        string hash,
        string contentType,
        bool isMain = false)
        => new(
            fileName,
            BusinessObjectDataItem.DetectType($"{contentType} {fileName}"),
            BusinessObjectDataItemStorage.Ipfs,
            hash,
            new Uri($"https://gateway.test/ipfs/{hash}"),
            contentType,
            fileName,
            16,
            isMain);

    private static BusinessObjectWorkspaceModel CreateWorkspace()
    {
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(1000),
            new Dictionary<TokenId, Amount>(),
            UtxoCount: 0,
            ViewedAtUtc);
        BusinessObjectId objectId = BusinessObjectId.FromOutPoint(new TxId(new string('c', 64)), 0);
        BusinessObjectTransactionDraft draft = new(
            objectId,
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
            objectId,
            "post",
            "1.0",
            Alice,
            "{}",
            ViewedAtUtc);

        return new BusinessObjectWorkspaceModel(
            "Alice test account",
            Context,
            Alice,
            balance,
            [new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(1000), IsMain: true)],
            [],
            [new EconomyAddressBookmark("Alice", Alice)],
            [],
            [],
            [],
            draft,
            editor,
            ViewedAtUtc);
    }

    private sealed class RecordingUploadService : IBusinessObjectDataItemUploadService
    {
        public List<string> FileNames { get; } = [];

        public Task<BusinessObjectDataItem> SaveToIpfsAsync(
            BusinessObjectDataItemUploadRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileNames.Add(request.FileName);
            string hash = $"QmMintTest{FileNames.Count}";
            return Task.FromResult(new BusinessObjectDataItem(
                request.FileName,
                BusinessObjectDataItem.DetectType($"{request.ContentType} {request.FileName}"),
                BusinessObjectDataItemStorage.Ipfs,
                hash,
                new Uri($"https://gateway.test/ipfs/{hash}"),
                request.ContentType,
                request.FileName,
                request.Size,
                request.IsMain,
                request.Description));
        }
    }
}
