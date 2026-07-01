using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectCreateIdentityTests : TestContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("test-ledger"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly DateTimeOffset ViewedAtUtc = DateTimeOffset.Parse("2026-07-01T10:00:00Z");

    public BusinessObjectCreateIdentityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Factory_CreatesValidDistinctIdsForSameTypeAndName()
    {
        BusinessObjectId first = BusinessObjectCreateIdFactory.CreateNewObjectId("image", "New Image BO");
        BusinessObjectId second = BusinessObjectCreateIdFactory.CreateNewObjectId("image", "New Image BO");

        Assert.NotEqual(first, second);
        Assert.StartsWith("bo:image:new-image-bo:", first.Value, StringComparison.Ordinal);
        Assert.StartsWith("bo:image:new-image-bo:", second.Value, StringComparison.Ordinal);
        Assert.True(LedgerValueValidation.IsValidIdentifier(first.Value, LedgerValidationLimits.MaxBusinessObjectIdLength));
        Assert.True(LedgerValueValidation.IsValidIdentifier(second.Value, LedgerValidationLimits.MaxBusinessObjectIdLength));
    }

    [Fact]
    public void Factory_StaysInsideLedgerIdentifierLimitsForLongNames()
    {
        string longName = string.Join('-', Enumerable.Repeat("Long Image Name", 20));

        BusinessObjectId objectId = BusinessObjectCreateIdFactory.CreateNewObjectId("image", longName);

        Assert.True(objectId.Value.Length <= LedgerValidationLimits.MaxBusinessObjectIdLength);
        Assert.True(LedgerValueValidation.IsValidIdentifier(objectId.Value, LedgerValidationLimits.MaxBusinessObjectIdLength));
        Assert.StartsWith("bo:image:long-image-name", objectId.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCreateDrafts_DoNotUseNameOnlyOrTimestampOnlyObjectIds()
    {
        string root = FindRepositoryRoot();
        string wizard = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Economy.Components",
            "Components",
            "BusinessObjects",
            "BusinessObjectCreationWizard.razor")).ReplaceLineEndings("\n");
        string wasmAdapter = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Economy.Components",
            "Services",
            "EconomyAccountWorkspaceAdapters.cs")).ReplaceLineEndings("\n");
        string nodeSession = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Economy.Components",
            "Services",
            "EconomyNodeWorkspaceSession.cs")).ReplaceLineEndings("\n");

        Assert.DoesNotContain("new BusinessObjectId($\"bo:{selectedObjectType}:{", wizard, StringComparison.Ordinal);
        Assert.DoesNotContain("new BusinessObjectId($\"bo:account-app:{now:yyyyMMddHHmmss}\")", wasmAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("new BusinessObjectId($\"bo:ui-message:{normalizedAccount}:{viewedAt:yyyyMMddHHmmss}\")", nodeSession, StringComparison.Ordinal);
        Assert.DoesNotContain("new CreateBusinessObjectRequest(\n                    UniqueObjectId(draft.ObjectId)", nodeSession, StringComparison.Ordinal);

        Assert.Contains("BusinessObjectCreateIdFactory.CreateNewObjectId(selectedObjectType, payload.Name)", wizard, StringComparison.Ordinal);
        Assert.Contains("BusinessObjectCreateIdFactory.CreateNewObjectId(\"account-app\", \"account app message\")", wasmAdapter, StringComparison.Ordinal);
        Assert.Contains("BusinessObjectCreateIdFactory.CreateNewObjectId(\"ui-message\", $\"Message from {normalizedAccount}\")", nodeSession, StringComparison.Ordinal);
        Assert.Contains("new CreateBusinessObjectRequest(\n                    draft.ObjectId", wasmAdapter, StringComparison.Ordinal);
        Assert.Contains("new CreateBusinessObjectRequest(\n                    draft.ObjectId", nodeSession, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wizard_StagesDistinctCreateIdsForRepeatedSameNameWithoutChangingPayload()
    {
        var stagedDrafts = new List<BusinessObjectTransactionDraft>();
        BusinessObjectCreationWizardModel model = new(
            "image",
            Step: 3,
            "New Image BO",
            "#image #proof",
            "Short image description",
            "Long image text can repeat and must not define identity.",
            [],
            null);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            RenderComponent<BusinessObjectCreationWizard>(parameters => parameters
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

        IElement stageButton = component.FindAll("button")
            .Single(button => button.TextContent.Contains("Stage BO Transaction", StringComparison.Ordinal));

        await stageButton.ClickAsync(new MouseEventArgs());
        await stageButton.ClickAsync(new MouseEventArgs());

        Assert.Equal(2, stagedDrafts.Count);
        Assert.NotEqual(stagedDrafts[0].ObjectId, stagedDrafts[1].ObjectId);
        Assert.All(stagedDrafts, draft =>
        {
            Assert.StartsWith("bo:image:new-image-bo:", draft.ObjectId.Value, StringComparison.Ordinal);
            Assert.True(LedgerValueValidation.IsValidIdentifier(draft.ObjectId.Value, LedgerValidationLimits.MaxBusinessObjectIdLength));

            BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(draft.StateJson, "fallback");
            Assert.Equal("New Image BO", payload.Name);
            Assert.Equal("Short image description", payload.Description);
            Assert.Equal("Long image text can repeat and must not define identity.", payload.Text);
        });
    }

    private static BusinessObjectWorkspaceModel CreateWorkspace()
    {
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(1000),
            new Dictionary<TokenId, Amount>(),
            UtxoCount: 0,
            ViewedAtUtc);
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.Ledger.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Cannot locate repository root.");
    }
}
