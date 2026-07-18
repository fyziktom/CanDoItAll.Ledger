using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Tests;

public sealed class TransactionDetailDensityTests : BunitContext
{
    private static readonly Address Alice = new("cdia1alice");
    private static readonly Address Bob = new("cdia1bob");
    private static readonly DateTimeOffset RecordedAt = DateTimeOffset.Parse("2026-07-16T12:04:00-04:00");

    public TransactionDetailDensityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Input_output_flow_uses_compact_nodes_and_directory_names()
    {
        EconomyTransactionDetailsModel details = CreateDetails();
        IReadOnlyList<AddressDirectoryEntry> directory = CreateDirectory();

        IRenderedComponent<TransactionInputOutputFlow> component = Render<TransactionInputOutputFlow>(parameters => parameters
            .Add(item => item.Inputs, details.Inputs)
            .Add(item => item.Outputs, details.Outputs)
            .Add(item => item.DirectoryEntries, directory));

        Assert.Single(component.FindAll("[data-testid='cda-transaction-input-node']"));
        Assert.Equal(2, component.FindAll("[data-testid='cda-transaction-output-node']").Count);
        Assert.Contains("Alice account", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Treasury", component.Markup, StringComparison.Ordinal);
        Assert.Contains("1 bo.posts", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindComponents<SummaryTiles>());
        Assert.Empty(component.FindComponents<SummaryTile>());
        Assert.Empty(component.FindComponents<MetricCard>());
        Assert.Empty(component.FindAll("[data-testid='cda-transaction-input-technical-details']"));
        Assert.Empty(component.FindAll("[data-testid='cda-transaction-output-technical-details']"));
        Assert.Equal("compact", component.Find("[data-testid='cda-transaction-io-flow']").GetAttribute("data-detail-level"));
    }

    [Fact]
    public void Input_output_flow_detailed_mode_exposes_protocol_fields_without_stat_cards()
    {
        EconomyTransactionDetailsModel baseline = CreateDetails(signatures: CreateSignatureBundle());
        HashHex inputDataHash = new(new string('3', 64));
        HashHex payloadHash = new(new string('4', 64));
        OutPointKey outPointKey = new(new HashHex(new string('5', 64)));
        HashHex metadataHash = new(new string('6', 64));
        EconomyTransactionInputDetails input = baseline.Inputs[0] with
        {
            BusinessObject = new EconomyTransactionBusinessObjectDetails(2, BusinessObjectOperation.Update, inputDataHash),
            SignaturePayloadHash = payloadHash,
            OutPointKey = outPointKey
        };
        EconomyTransactionOutputDetails output = baseline.Outputs[1] with { MetadataHash = metadataHash };

        IRenderedComponent<TransactionInputOutputFlow> component = Render<TransactionInputOutputFlow>(parameters => parameters
            .Add(item => item.Inputs, [input])
            .Add(item => item.Outputs, [output])
            .Add(item => item.TransactionId, baseline.TxId)
            .Add(item => item.DetailLevel, TransactionFlowDetailLevel.Detailed)
            .Add(item => item.DirectoryEntries, CreateDirectory())
            .Add(
                item => item.InputPresentationSelector,
                _ => new TransactionFlowItemPresentation("Authority", "warning", "Reusable permission.")));

        Assert.Single(component.FindAll("[data-testid='cda-transaction-input-technical-details']"));
        Assert.Single(component.FindAll("[data-testid='cda-transaction-output-technical-details']"));
        Assert.Equal("detailed", component.Find("[data-testid='cda-transaction-io-flow']").GetAttribute("data-detail-level"));
        Assert.Contains("Authority", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Reusable permission.", component.Markup, StringComparison.Ordinal);
        Assert.Contains(input.PreviousOutPoint.ToString(), component.FindComponents<CopyableMonoValue>().Select(value => value.Instance.Value));
        Assert.Contains(
            BusinessObjectId.FromOutPoint(baseline.TxId, output.OutputIndex).Value,
            component.FindComponents<CopyableMonoValue>().Select(value => value.Instance.Value));
        string[] hashes = component.FindComponents<HashDisplay>()
            .Select(hash => hash.Instance.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        Assert.Contains(inputDataHash.Value, hashes);
        Assert.Contains(payloadHash.Value, hashes);
        Assert.Contains(outPointKey.Value.Value, hashes);
        Assert.Contains(metadataHash.Value, hashes);
        Assert.Contains(output.BusinessObject!.DataHash.Value, hashes);
        Assert.Empty(component.FindComponents<MetricCard>());
    }

    [Fact]
    public void Timeline_uses_one_authoritative_time_and_omits_unsigned_event()
    {
        IRenderedComponent<TransactionTimeline> component = Render<TransactionTimeline>(parameters => parameters
            .Add(item => item.Details, CreateDetails()));

        IReadOnlyList<IElement> events = component.FindAll("[data-testid='cda-transaction-timeline-event']");
        IElement timestamp = Assert.Single(component.FindAll("time"));

        Assert.Equal(2, events.Count);
        Assert.Equal(RecordedAt.ToString("O"), timestamp.GetAttribute("datetime"));
        Assert.Contains("Recorded", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Confirmed", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Signed", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Derived", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Current", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Timeline_shows_signed_event_with_actual_signature_count()
    {
        IRenderedComponent<TransactionTimeline> component = Render<TransactionTimeline>(parameters => parameters
            .Add(item => item.Details, CreateDetails(signatures: CreateSignatureBundle())));

        Assert.Equal(3, component.FindAll("[data-testid='cda-transaction-timeline-event']").Count);
        Assert.Contains("Signed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("1 signature(s)", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Confirmed", component.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TransactionStatus.Pending, "warning", "pending")]
    [InlineData(TransactionStatus.Confirmed, "success", "verified")]
    [InlineData(TransactionStatus.Rejected, "danger", "cancel")]
    [InlineData(TransactionStatus.Reverted, "danger", "history")]
    [InlineData(TransactionStatus.Archived, "neutral", "archive")]
    public void Detail_status_uses_semantically_correct_tone_and_icon(
        TransactionStatus status,
        string expectedTone,
        string expectedIcon)
    {
        IRenderedComponent<TransactionDetailsDialogContent> component = Render<TransactionDetailsDialogContent>(parameters => parameters
            .Add(item => item.Details, CreateDetails(status)));

        IReadOnlyList<IRenderedComponent<StatusBadge>> statusBadges = component
            .FindComponents<StatusBadge>()
            .Where(badge => badge.Instance.Text == status.ToString())
            .ToArray();

        Assert.NotEmpty(statusBadges);
        Assert.All(statusBadges, badge => Assert.Equal(expectedTone, badge.Instance.Tone));
        Assert.Equal(expectedIcon, component.FindComponents<Icon>().First().Instance.Name);
    }

    [Fact]
    public void Detail_identity_strip_states_kind_and_status_once_and_keeps_full_date()
    {
        IRenderedComponent<TransactionDetailsDialogContent> component = Render<TransactionDetailsDialogContent>(parameters => parameters
            .Add(item => item.Details, CreateDetails())
            .Add(item => item.DirectoryEntries, CreateDirectory()));

        IElement identity = component.Find("[data-testid='cda-transaction-identity-strip']");

        Assert.Equal(1, CountOccurrences(identity.TextContent, "Business object"));
        Assert.Equal(1, CountOccurrences(identity.TextContent, "Confirmed"));
        Assert.Contains("16 Jul 2026", identity.TextContent, StringComparison.Ordinal);
        Assert.Contains("1 in · 2 out", identity.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirmed transaction", identity.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hash", identity.TextContent, StringComparison.Ordinal);
        Assert.Contains("Alice account", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Treasury", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_contract_keeps_custom_css_ledger_specific_and_removes_duplicate_dialog_subtitles()
    {
        string details = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionDetailsDialogContent.razor");
        string flow = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionInputOutputFlow.razor");
        string flowCss = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionInputOutputFlow.razor.css");
        string payload = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TxDataPayloadViewer.razor");
        string signatures = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionSignatureList.razor");
        string timeline = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionTimeline.razor");
        string timelineCss = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionTimeline.razor.css");
        string adapter = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Account", "EconomyAccountWorkspaceAdapterHost.razor");
        string demo = ReadRepoFile("examples", "CanDoItAll.Economy.Components.Demo", "Components", "Pages", "Home.razor");
        string accountApp = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "Home.razor");
        string node = ReadRepoFile("src", "CanDoItAll.Economy.Node", "Components", "Shared", "NodeWorkspace.razor");
        string nodeAdmin = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Admin", "NodeAdminDashboard.razor");

        Assert.Contains("<Toolbar Class=\"gap-2\"", details, StringComparison.Ordinal);
        Assert.Contains("Style=\"padding:0.5rem;box-shadow:none\"", details, StringComparison.Ordinal);
        Assert.Contains("<TransactionInputOutputFlow", details, StringComparison.Ordinal);
        Assert.Contains("DetailLevel=\"TransactionFlowDetailLevel.Detailed\"", details, StringComparison.Ordinal);
        Assert.Contains("<TxDataPayloadViewer", details, StringComparison.Ordinal);
        Assert.Contains("<TransactionSignatureList", details, StringComparison.Ordinal);
        Assert.DoesNotContain("<TxInputCard", details, StringComparison.Ordinal);
        Assert.DoesNotContain("<TxOutputCard", details, StringComparison.Ordinal);
        Assert.DoesNotContain("<TxSignatureCard", details, StringComparison.Ordinal);
        Assert.DoesNotContain("<SectionCard", details, StringComparison.Ordinal);
        Assert.DoesNotContain("<CompactStat", details, StringComparison.Ordinal);
        Assert.DoesNotContain("@Details.Status transaction", details, StringComparison.Ordinal);
        Assert.DoesNotContain("ContextLabel", details, StringComparison.Ordinal);
        Assert.Contains("<SurfaceCard", flow, StringComparison.Ordinal);
        Assert.Contains("<StatusBadge", flow, StringComparison.Ordinal);
        Assert.DoesNotContain("<SummaryTiles", flow, StringComparison.Ordinal);
        Assert.DoesNotContain("<MetricCard", flow, StringComparison.Ordinal);
        Assert.Contains("<DataGrid", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("<SectionCard", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("<SelectionListItem", payload, StringComparison.Ordinal);
        Assert.Contains("<DataGrid", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("<SectionCard", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("<SelectionListItem", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("padding:", flowCss, StringComparison.Ordinal);
        Assert.DoesNotContain("border-radius:", flowCss, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSeconds", timeline, StringComparison.Ordinal);
        Assert.DoesNotContain("padding:", timelineCss, StringComparison.Ordinal);
        Assert.DoesNotContain("border-radius:", timelineCss, StringComparison.Ordinal);
        Assert.Contains("GetEntriesAsync(AddressDirectorySeed.FromWorkspace(workspace))", adapter, StringComparison.Ordinal);
        Assert.Contains("nameof(TransactionDetailsDialogContent.DirectoryEntries)", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("Subtitle = $\"{details.Kind}", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("Subtitle = $\"{details.Kind}", demo, StringComparison.Ordinal);
        Assert.DoesNotContain("Eyebrow=\"Confirmed ledger transaction\"", accountApp, StringComparison.Ordinal);
        Assert.DoesNotContain("Subtitle=\"Review the confirmed ledger outputs.\"", accountApp, StringComparison.Ordinal);
        Assert.DoesNotContain("Subtitle = txId", node, StringComparison.Ordinal);
        Assert.DoesNotContain("Subtitle = transaction.Route", nodeAdmin, StringComparison.Ordinal);
        Assert.DoesNotContain("nameof(TransactionDetailsDialogContent.ContextLabel)", nodeAdmin, StringComparison.Ordinal);
    }

    private static EconomyTransactionDetailsModel CreateDetails(
        TransactionStatus status = TransactionStatus.Confirmed,
        SignatureBundle? signatures = null)
    {
        const string transactionId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        return new EconomyTransactionDetailsModel(
            new TxId(transactionId),
            new HashHex(transactionId),
            TransactionKind.BusinessObject,
            status,
            SigningMode.Independent,
            RecordedAt,
            null,
            [
                new EconomyTransactionInputDetails(
                    0,
                    new OutPoint(new TxId("previous"), 0),
                    Alice,
                    Amount.FromInt64(3),
                    null,
                    null,
                    signatures ?? SignatureBundle.Empty)
            ],
            [
                new EconomyTransactionOutputDetails(0, Alice, Amount.FromInt64(2), null, null, null),
                new EconomyTransactionOutputDetails(
                    1,
                    Bob,
                    Amount.Zero,
                    new TokenTransfer(new TokenId("bo.posts"), Amount.FromInt64(1)),
                    new EconomyTransactionBusinessObjectDetails(
                        1,
                        BusinessObjectOperation.Create,
                        new HashHex("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")),
                    null)
            ]);
    }

    private static SignatureBundle CreateSignatureBundle()
        => new(
        [
            new SignatureEnvelope(
                SignatureAlgorithmId.EcdsaP256Sha256,
                "alice-key",
                new HashHex("1111111111111111111111111111111111111111111111111111111111111111"),
                new HashHex("2222222222222222222222222222222222222222222222222222222222222222"),
                [0x01, 0x02, 0x03])
        ]);

    private static IReadOnlyList<AddressDirectoryEntry> CreateDirectory()
        =>
        [
            new(Alice.Value, "Alice account", AddressDirectoryEntryKind.Own),
            new(Bob.Value, "Treasury", AddressDirectoryEntryKind.External)
        ];

    private static int CountOccurrences(string value, string fragment)
        => value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string ReadRepoFile(params string[] relativeParts)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativeParts]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
