using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Components.Services;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectCreateIdentityTests : BunitContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("test-ledger"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly DateTimeOffset ViewedAtUtc = DateTimeOffset.Parse("2026-07-01T10:00:00Z");

    public BusinessObjectCreateIdentityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddLedgerSsrAccountWorkspaceAdapters();
    }

    [Fact]
    public void ProductionCreateDrafts_DoNotManufacturePublicObjectIds()
    {
        string root = FindRepositoryRoot();
        string wizard = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Ledger.Components",
            "Components",
            "BusinessObjects",
            "BusinessObjectCreationWizard.razor")).ReplaceLineEndings("\n");
        string wasmAdapter = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Ledger.Components",
            "Services",
            "LedgerAccountWorkspaceAdapters.cs")).ReplaceLineEndings("\n");
        string nodeSession = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Ledger.Components",
            "Services",
            "LedgerNodeWorkspaceSession.cs")).ReplaceLineEndings("\n");
        string eventLookup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Ledger.Components",
            "Services",
            "BusinessObjectEventLookup.cs")).ReplaceLineEndings("\n");
        string rawFormatter = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Ledger.Components",
            "Services",
            "TransactionRawJsonFormatter.cs")).ReplaceLineEndings("\n");
        string nodeWorkspace = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Ledger.Node",
            "Components",
            "Shared",
            "NodeWorkspace.razor")).ReplaceLineEndings("\n");
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Ledger.Cli",
            "Program.cs")).ReplaceLineEndings("\n");

        Assert.DoesNotContain("BusinessObjectCreateIdFactory.CreateNewObjectId", wizard, StringComparison.Ordinal);
        Assert.DoesNotContain("BusinessObjectCreateIdFactory.CreateNewObjectId", wasmAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("BusinessObjectCreateIdFactory.CreateOpaqueObjectId", wasmAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("BusinessObjectCreateIdFactory.CreateNewObjectId", nodeSession, StringComparison.Ordinal);
        Assert.DoesNotContain("UniqueObjectId(", wasmAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("UniqueObjectId(", nodeSession, StringComparison.Ordinal);
        Assert.DoesNotContain("new CreateBusinessObjectRequest(\n                    draft.ObjectId", wasmAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("new CreateBusinessObjectRequest(\n                    draft.ObjectId", nodeSession, StringComparison.Ordinal);
        Assert.Contains("draft.OwnerAddress,\n                    draft.MainAmount", wasmAdapter, StringComparison.Ordinal);
        Assert.Contains("draft.OwnerAddress,\n                    draft.MainAmount", nodeSession, StringComparison.Ordinal);
        Assert.Contains("directObjectId.IsCanonicalOutPointId()", wasmAdapter, StringComparison.Ordinal);
        Assert.Contains("requestedObjectId.IsCanonicalOutPointId()", eventLookup, StringComparison.Ordinal);
        Assert.Contains("BusinessObjectId.FromOutPoint(input.PreviousOutPoint)", rawFormatter, StringComparison.Ordinal);
        Assert.Contains("BusinessObjectId.FromOutPoint(transactionId, output.OutputIndex)", rawFormatter, StringComparison.Ordinal);
        Assert.DoesNotContain("businessObject.ObjectId", rawFormatter, StringComparison.Ordinal);
        Assert.DoesNotContain("mintForm.BusinessObjectId", nodeWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Business object id\"", nodeWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("GetOptional(\"business-object-id\")", cli, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wizard_StagesPendingIdentityForRepeatedSameNameWithoutChangingPayload()
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

        IElement stageButton = component.FindAll("button")
            .Single(button => button.TextContent.Contains("Stage BO Transaction", StringComparison.Ordinal));

        await stageButton.ClickAsync(new MouseEventArgs());
        await stageButton.ClickAsync(new MouseEventArgs());

        Assert.Equal(2, stagedDrafts.Count);
        Assert.All(stagedDrafts, draft =>
        {
            Assert.Equal(default(BusinessObjectId), draft.ObjectId);
            Assert.DoesNotContain("bo:", draft.ExternalRef, StringComparison.OrdinalIgnoreCase);

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
        BusinessObjectId existingObjectId = BusinessObjectId.FromOutPoint(new TxId(new string('c', 64)), 0);
        BusinessObjectTransactionDraft draft = new(
            existingObjectId,
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
            existingObjectId,
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
            [new(LedgerTokenOption.MainKey, "Main", null, Amount.FromInt64(1000), IsMain: true)],
            [],
            [new LedgerAddressBookmark("Alice", Alice)],
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
