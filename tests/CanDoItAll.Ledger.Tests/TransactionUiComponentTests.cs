using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
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
                    new BusinessObjectRef(
                        new BusinessObjectId("bo:secure-message:1"),
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
        Assert.Contains("\"objectType\": \"secure-message\"", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"stateJson\": {", rawJson, StringComparison.Ordinal);
        Assert.Contains("\"format\": \"candoitall-secure-message-v1\"", rawJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0022", rawJson, StringComparison.Ordinal);
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

    private static TransactionData CreateMessageData()
        => new(
            "application/json",
            new HashHex("9999999999999999999999999999999999999999999999999999999999999999"),
            "{\"objectType\":\"secure-message\",\"schemaVersion\":\"1.0\",\"stateJson\":\"{\\\"format\\\":\\\"candoitall-secure-message-v1\\\",\\\"cipherText\\\":\\\"abc123\\\"}\"}",
            null,
            TransactionDataItemType.BusinessObject);
}
