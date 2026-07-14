using Bunit;
using AngleSharp.Dom;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Core;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Economy.Tests;

public sealed class TransactionUiComponentTests : TestContext
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
        IRenderedComponent<TransactionHistoryDialogContent> component = RenderComponent<TransactionHistoryDialogContent>(parameters => parameters
            .Add(x => x.AccountAddress, Bob)
            .Add(x => x.Transactions,
            [
                CreateSummary("tx-oldest", sameTimestamp, 1),
                CreateSummary("tx-newest-message", sameTimestamp, 7, CreateMessageData()),
                CreateSummary("tx-middle", sameTimestamp, 3)
            ]));

        IReadOnlyList<AngleSharp.Dom.IElement> rows = component.FindAll("[data-testid='tx-history-row']");

        Assert.Equal(3, rows.Count);
        Assert.Contains("tx-newest-message", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("tx-middle", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("tx-oldest", rows[2].TextContent, StringComparison.Ordinal);
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
        var details = new EconomyTransactionDetailsModel(
            new TxId("tx-raw-message"),
            new HashHex("3333333333333333333333333333333333333333333333333333333333333333"),
            TransactionKind.BusinessObject,
            TransactionStatus.Confirmed,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-07-03T13:15:00Z"),
            CreateMessageData(),
            [
                new EconomyTransactionInputDetails(
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
                new EconomyTransactionOutputDetails(
                    0,
                    Bob,
                    Amount.FromDecimal(1.5m),
                    null,
                    new EconomyTransactionBusinessObjectDetails(
                        1,
                        BusinessObjectOperation.Create,
                        new HashHex("5555555555555555555555555555555555555555555555555555555555555555")),
                    new HashHex("6666666666666666666666666666666666666666666666666666666666666666"))
            ],
            LedgerSequence: 12,
            LedgerEntryHash: new HashHex("7777777777777777777777777777777777777777777777777777777777777777"));
        IRenderedComponent<TransactionDetailsDialogContent> component = RenderComponent<TransactionDetailsDialogContent>(parameters => parameters
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
        IRenderedComponent<TxDataPayloadViewer> component = RenderComponent<TxDataPayloadViewer>(parameters => parameters
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
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
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

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
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
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        BusinessObjectTransactionDraft? captured = null;

        IRenderedComponent<TransactionUtxoSplitForm> component = RenderComponent<TransactionUtxoSplitForm>(parameters => parameters
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
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        BusinessObjectTransactionDraft current = workspace.TransactionDraft with
        {
            SelectedMode = BusinessObjectTransactionMode.MainPayment
        };

        IRenderedComponent<TransactionSendForm> component = RenderComponent<TransactionSendForm>(parameters => parameters
            .Add(x => x.Draft, current)
            .Add(x => x.Bookmarks, workspace.AddressBookmarks)
            .Add(x => x.DraftChanged, draft => current = draft));

        component.Find("[data-testid='cda-send-payment-amount']").Input("12.25");
        Assert.Equal("12.25", current.PaymentAmount.ToString());

        component.SetParametersAndRender(parameters => parameters
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
