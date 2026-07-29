using Bunit;
using AngleSharp.Dom;
using System.Globalization;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Components.Samples;
using CanDoItAll.Ledger.Components.Services;
using CanDoItAll.Ledger.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Ledger.Tests;

public sealed class TransactionUiComponentTests : BunitContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("ui-ledger"));
    private static readonly Address Alice = new("cdia1alice");
    private static readonly Address Bob = new("cdia1bob");

    public TransactionUiComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void HistoryDialog_orders_transactions_by_ledger_sequence_newest_first()
    {
        DateTimeOffset sameTimestamp = DateTimeOffset.Parse("2026-07-03T12:00:00Z");
        IRenderedComponent<TransactionHistoryDialogContent> component = Render<TransactionHistoryDialogContent>(parameters => parameters
            .Add(x => x.AccountAddress, Bob)
            .Add(x => x.Transactions,
            [
                CreateSummary("tx-oldest", sameTimestamp, 1),
                CreateSummary("tx-newest-message", sameTimestamp, 7, CreateMessageData()),
                CreateSummary("tx-middle", sameTimestamp, 3)
            ]));

        IReadOnlyList<AngleSharp.Dom.IElement> rows = component.FindAll("[data-testid='tx-history-row']");
        string[] headers = component.FindAll("[data-testid='tx-history-grid'] th")
            .Select(header => header.TextContent.Trim())
            .ToArray();
        IElement grid = component.Find("[data-testid='tx-history-grid']");

        Assert.Equal(3, rows.Count);
        Assert.Equal(["Tx id", "Route", "Kind", "Status", "Inputs", "Outputs", "Data", "Created", "Actions"], headers);
        Assert.Contains("overflow-x-auto", grid.ClassList);
        Assert.All(grid.QuerySelectorAll("tbody tr"), row => Assert.Equal("0", row.GetAttribute("tabindex")));
        Assert.Contains("tx-newest-message", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("tx-middle", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("tx-oldest", rows[2].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HistoryDialog_grid_preserves_row_and_details_actions_without_copy_bubbling()
    {
        LedgerTransactionSummary transaction = CreateSummary(
            "tx-action",
            DateTimeOffset.Parse("2026-07-03T12:00:00Z"),
            4);
        var requested = new List<LedgerTransactionSummary>();
        IRenderedComponent<TransactionHistoryDialogContent> component = Render<TransactionHistoryDialogContent>(parameters => parameters
            .Add(x => x.Transactions, [transaction])
            .Add(x => x.TransactionDetailsRequested, item => requested.Add(item)));

        await component.Find("[data-testid='tx-history-row'] button").ClickAsync(new MouseEventArgs());
        Assert.Empty(requested);

        await component.Find("[data-testid='tx-history-grid'] tbody tr").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal([transaction], requested);

        requested.Clear();
        IElement detailsButton = component.FindAll("button")
            .Single(button => button.TextContent.Contains("Details", StringComparison.Ordinal));
        await detailsButton.ClickAsync(new MouseEventArgs());
        Assert.Equal([transaction], requested);
    }

    [Fact]
    public void HistoryDialog_prefers_saved_directory_names_and_keeps_short_addresses_visible()
    {
        LedgerTransactionSummary transaction = CreateSummary(
            "tx-address-labels",
            DateTimeOffset.Parse("2026-07-03T12:00:00Z"),
            8);
        IRenderedComponent<TransactionHistoryDialogContent> component = Render<TransactionHistoryDialogContent>(parameters => parameters
            .Add(x => x.Transactions, [transaction])
            .Add(x => x.AccountAddress, Alice)
            .Add(x => x.AccountName, "Alice account")
            .Add(x => x.Bookmarks, [new LedgerAddressBookmark("Bob seed", Bob)])
            .Add(x => x.DirectoryEntries,
            [
                new AddressDirectoryEntry(
                    Bob.Value,
                    "Bob saved contact",
                    AddressDirectoryEntryKind.External,
                    Favorite: true)
            ]));

        string route = component.FindAll("[data-testid='tx-history-grid'] tbody td")[1].TextContent;

        Assert.Contains("Alice account (cdia1alice)", route, StringComparison.Ordinal);
        Assert.Contains("Bob saved contact (cdia1bob)", route, StringComparison.Ordinal);
        Assert.DoesNotContain("Bob seed", route, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoryDialog_humanizes_labels_and_formats_offsets_in_local_time()
    {
        DateTimeOffset timestamp = DateTimeOffset.Parse("2026-07-16T00:30:45+05:45");
        LedgerTransactionSummary transaction = CreateSummary(
            "tx-local-time",
            timestamp,
            9,
            CreateMessageData());
        IRenderedComponent<TransactionHistoryDialogContent> component = Render<TransactionHistoryDialogContent>(parameters => parameters
            .Add(x => x.Transactions, [transaction]));

        IReadOnlyList<IElement> cells = component.FindAll("[data-testid='tx-history-grid'] tbody td");
        IElement? time = cells[7].QuerySelector("time");

        Assert.Equal("Business object", cells[2].TextContent.Trim());
        Assert.Equal("Business object", cells[6].TextContent.Trim());
        Assert.NotNull(time);
        Assert.Equal(timestamp.ToString("O"), time.GetAttribute("datetime"));
        Assert.Equal(
            timestamp.ToLocalTime().ToString("dd MMM yyyy · HH:mm:ss", CultureInfo.InvariantCulture),
            time.TextContent.Trim());
    }

    [Fact]
    public async Task TransactionDetails_raw_tab_renders_structured_json()
    {
        var signature = new SignatureEnvelope(
            SignatureAlgorithmId.EcdsaP256Sha256,
            "alice-key",
            new HashHex("1111111111111111111111111111111111111111111111111111111111111111"),
            new HashHex("2222222222222222222222222222222222222222222222222222222222222222"),
            [0x01, 0x02, 0x03],
            [0x04, 0x05, 0x06]);
        var details = new LedgerTransactionDetailsModel(
            new TxId("tx-raw-message"),
            new HashHex("3333333333333333333333333333333333333333333333333333333333333333"),
            TransactionKind.BusinessObject,
            TransactionStatus.Confirmed,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-07-03T13:15:00Z"),
            CreateMessageData(),
            [
                new LedgerTransactionInputDetails(
                    0,
                    new OutPoint(new TxId("tx-prev"), 1),
                    Alice,
                    Amount.FromDecimal(2.5m),
                    null,
                    null,
                    new SignatureBundle([signature]),
                    signature.PayloadHash,
                    new OutPointKey(new HashHex("4444444444444444444444444444444444444444444444444444444444444444")))
            ],
            [
                new LedgerTransactionOutputDetails(
                    0,
                    Bob,
                    Amount.FromDecimal(1.5m),
                    null,
                    new LedgerTransactionBusinessObjectDetails(
                        1,
                        BusinessObjectOperation.Create,
                        new HashHex("5555555555555555555555555555555555555555555555555555555555555555")),
                    new HashHex("6666666666666666666666666666666666666666666666666666666666666666"))
            ],
            LedgerSequence: 12,
            LedgerEntryHash: new HashHex("7777777777777777777777777777777777777777777777777777777777777777"));
        IRenderedComponent<TransactionDetailsDialogContent> component = Render<TransactionDetailsDialogContent>(parameters => parameters
            .Add(x => x.Details, details));

        AngleSharp.Dom.IElement rawTab = component.FindAll("button")
            .First(button => button.TextContent.Contains("Raw tx", StringComparison.OrdinalIgnoreCase));
        await rawTab.ClickAsync(new MouseEventArgs());

        string rawJson = component.Find("[data-testid='tx-raw-json']").TextContent;

        Assert.Contains("\"txid\": \"tx-raw-message\"", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"ledgerSequence\": 12", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"vin\":", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"vout\":", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"signature\": \"010203\"", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"publicKey\": \"040506\"", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"objectId\": \"tx-raw-message:0\"", rawJson, StringComparison.Ordinal);
        Assert.DoesNotContain("bo:secure-message:1", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"objectType\": \"secure-message\"", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"stateJson\": {", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"format\": \"candoitall-secure-message-v1\"", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"headersJson\": {", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"priority\": \"high\"", rawJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0022", rawJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadViewer_formats_inline_json_without_escaped_nested_strings()
    {
        IRenderedComponent<TxDataPayloadViewer> component = Render<TxDataPayloadViewer>(parameters => parameters
            .Add(x => x.Data, CreateMessageData()));

        string payloadJson = component.Find("[data-testid='tx-payload-inline-json']").TextContent;

        Assert.Contains("\"objectType\": \"secure-message\"", payloadJson, StringComparison.Ordinal);
        Assert.Contains("\"stateJson\": {", payloadJson, StringComparison.Ordinal);
        Assert.Contains("\"headersJson\": {", payloadJson, StringComparison.Ordinal);
        Assert.Contains("\"priority\": \"high\"", payloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0022", payloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TransactionComposerShell_RendersUtxoMergeWithOnlyPlainSpendableInputs()
    {
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        LedgerSpendableOutput extraPlain = CreateSpendableOutput("tx-extra-plain", 2, Amount.FromDecimal(3.5m), workspace.Address);
        BusinessObjectTransactionDraft draft = workspace.TransactionDraft with
        {
            SelectedMode = BusinessObjectTransactionMode.UtxoMerge,
            PaymentReceiverAddress = workspace.Address,
            SelectedUtxos =
            [
                workspace.SpendableOutputsOrEmpty[0].OutPoint,
                extraPlain.OutPoint
            ]
        };

        IRenderedComponent<TransactionComposerShell> component = Render<TransactionComposerShell>(parameters => parameters
            .Add(x => x.Draft, draft)
            .Add(x => x.TokenOptions, workspace.TokenOptions)
            .Add(x => x.Bookmarks, workspace.AddressBookmarks)
            .Add(x => x.SpendableOutputs, workspace.SpendableOutputsOrEmpty.Append(extraPlain).ToArray()));

        component.Find("[data-testid='transaction-mode-UtxoMerge']").Click();

        Assert.Empty(component.FindAll("[data-testid='cda-merge-utxo-selector-row']"));
        Assert.Contains("2 selected", component.Find("[data-testid='cda-merge-utxo-selector']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Change selection", component.Find("[data-testid='cda-merge-utxo-selector']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Combine two or more plain main-currency outputs.", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tx-token-source", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tx-permission-source", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tx-rule-permission-source", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransactionUtxoSplitForm_AppliesEqualOutputPlanToDraft()
    {
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        BusinessObjectTransactionDraft? captured = null;

        IRenderedComponent<TransactionUtxoSplitForm> component = Render<TransactionUtxoSplitForm>(parameters => parameters
            .Add(x => x.Draft, workspace.TransactionDraft with { SelectedMode = BusinessObjectTransactionMode.UtxoSplit })
            .Add(x => x.Bookmarks, workspace.AddressBookmarks)
            .Add(x => x.SpendableOutputs, workspace.SpendableOutputsOrEmpty)
            .Add(x => x.DraftChanged, draft => captured = draft));

        component.Find("[data-testid='cda-split-equal-count']").Input("3");
        component.Find("[data-testid='cda-split-equal-amount']").Input("0.5");
        IElement apply = component.FindAll("button")
            .Single(button => button.TextContent.Contains("Apply equal outputs", StringComparison.Ordinal));
        await apply.ClickAsync(new MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Equal(3, captured!.UtxoSplitOutputsOrEmpty.Count);
        Assert.All(captured.UtxoSplitOutputsOrEmpty, output =>
        {
            Assert.Equal(workspace.Address, output.ReceiverAddress);
            Assert.Equal("0.5", output.Amount.ToString());
        });
    }

    [Fact]
    public void TransactionSendForm_UpdatesReusablePaymentDraftFields()
    {
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        BusinessObjectTransactionDraft current = workspace.TransactionDraft with
        {
            SelectedMode = BusinessObjectTransactionMode.MainPayment
        };

        IRenderedComponent<TransactionSendForm> component = Render<TransactionSendForm>(parameters => parameters
            .Add(x => x.Draft, current)
            .Add(x => x.Bookmarks, workspace.AddressBookmarks)
            .Add(x => x.DraftChanged, draft => current = draft));

        component.Find("[data-testid='cda-send-payment-amount']").Input("12.25");
        Assert.Equal("12.25", current.PaymentAmount.ToString());

        component.Render(parameters => parameters
            .Add(x => x.Draft, current)
            .Add(x => x.Bookmarks, workspace.AddressBookmarks)
            .Add(x => x.DraftChanged, draft => current = draft));
        component.Find("[data-testid='cda-send-payment-memo']").Input("ops payout");

        Assert.Equal("ops payout", current.ExternalRef);
        Assert.Equal("12.25", current.PaymentAmount.ToString());
    }

    private static LedgerTransactionSummary CreateSummary(
        string txId,
        DateTimeOffset timestamp,
        long ledgerSequence,
        TransactionData? data = null)
        => new(
            Context,
            new TxId(txId),
            TransactionKind.BusinessObject,
            TransactionStatus.Confirmed,
            timestamp,
            [Alice],
            [Bob],
            [],
            [],
            data,
            ledgerSequence);

    private static LedgerSpendableOutput CreateSpendableOutput(
        string txId,
        int outputIndex,
        Amount amount,
        Address address)
        => new(
            Context,
            new OutPointKey(new HashHex("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")),
            new OutPoint(new TxId(txId), outputIndex),
            address,
            amount,
            null,
            null,
            DateTimeOffset.Parse("2026-07-03T12:00:00Z"));

    private static TransactionData CreateMessageData()
        => new(
            "application/json",
            new HashHex("9999999999999999999999999999999999999999999999999999999999999999"),
            "{\"objectType\":\"secure-message\",\"schemaVersion\":\"1.0\",\"stateJson\":\"{\\\"format\\\":\\\"candoitall-secure-message-v1\\\",\\\"headersJson\\\":\\\"{\\\\\\\"priority\\\\\\\":\\\\\\\"high\\\\\\\"}\\\",\\\"cipherText\\\":\\\"abc123\\\"}\"}",
            null,
            TransactionDataItemType.BusinessObject);
}
