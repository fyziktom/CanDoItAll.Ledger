using System.Text.Json;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectAdvancedTransactionTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly LedgerAccountContext Context = new(LedgerId);
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly Address Carol = Address.FromPublicKey(Hex.Utf8("carol-public"));
    private static readonly Address Dave = Address.FromPublicKey(Hex.Utf8("dave-public"));
    private static readonly Address Bootstrap = Address.FromPublicKey(Hex.Utf8("bootstrap-public"));
    private static readonly TokenId OperatingToken = new("operating-token");

    [Fact]
    public async Task Account_SupportsSubaccountsAndAdvancedBusinessObjectTransactions()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectSubAccount subAccount = alice.AddSubAccount("field-office", rig.CreateKey("dave"));
        LedgerSubmitResult funding = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(100)),
            new TransactionOutput(1, Dave, Amount.FromInt64(25)));
        Assert.True(funding.Accepted);

        Assert.Equal(Dave, subAccount.Address);
        Assert.Single(alice.SubAccounts);

        LedgerSubmitResult subCreate = await subAccount.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("sub-settings"),
                "settings",
                "{\"theme\":\"field\"}",
                MainAmount: Amount.FromInt64(25),
                ItemType: TransactionDataItemType.AccountSettings),
            CancellationToken.None);
        Assert.True(subCreate.Accepted);
        Assert.Equal("25", (await subAccount.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("work-order"),
                "work-order",
                "{\"status\":\"draft\"}",
                MainAmount: Amount.FromInt64(100)),
            CancellationToken.None);
        Assert.True(create.Accepted);

        rig.Time.Advance(TimeSpan.FromHours(1));
        LedgerSubmitResult update = await alice.UpdateBusinessObjectAsync(
            new UpdateBusinessObjectRequest(new BusinessObjectId("work-order"), "{\"status\":\"approved\"}"),
            CancellationToken.None);
        Assert.True(update.Accepted);
        Assert.Equal(2, Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None)).Version);

        rig.Time.Advance(TimeSpan.FromHours(1));
        LedgerSubmitResult clone = await alice.CloneBusinessObjectAsync(
            new CloneBusinessObjectRequest(
                new BusinessObjectId("work-order"),
                new BusinessObjectId("work-order-copy"),
                "work-order",
                "{\"status\":\"copy\"}"),
            CancellationToken.None);
        Assert.True(clone.Accepted);
        Assert.Equal(2, (await alice.GetBusinessObjectsAsync(CancellationToken.None)).Count);

        rig.Time.Advance(TimeSpan.FromHours(1));
        LedgerSubmitResult merge = await alice.MergeBusinessObjectsAsync(
            new MergeBusinessObjectsRequest(
                new BusinessObjectId("work-order"),
                new BusinessObjectId("work-order-copy"),
                new BusinessObjectId("merged-work-order"),
                "{\"status\":\"merged\"}"),
            CancellationToken.None);
        Assert.True(merge.Accepted);
        LedgerBusinessObjectHolding merged = Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal(new BusinessObjectId("merged-work-order"), merged.ObjectId);

        rig.Time.Advance(TimeSpan.FromHours(1));
        LedgerSubmitResult split = await alice.SplitBusinessObjectPaymentAsync(
            new SplitBusinessObjectPaymentRequest(
                new BusinessObjectId("merged-work-order"),
                new[]
                {
                    new BusinessObjectPaymentOutput(Bob, Amount.FromInt64(40)),
                    new BusinessObjectPaymentOutput(Carol, Amount.FromInt64(10))
                }),
            CancellationToken.None);
        Assert.True(split.Accepted);
        Assert.Empty(await alice.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal("50", (await alice.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());

        BusinessObjectAccount bob = rig.CreateAccount("bob");
        LedgerBusinessObjectHolding bobHolding = Assert.Single(await bob.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal(new BusinessObjectId("merged-work-order"), bobHolding.ObjectId);
        Assert.Equal(2, bobHolding.Version);

        rig.Time.Advance(TimeSpan.FromHours(1));
        LedgerSubmitResult close = await bob.CloseBusinessObjectAsync(
            new CloseBusinessObjectRequest(new BusinessObjectId("merged-work-order")),
            CancellationToken.None);
        Assert.True(close.Accepted);
        Assert.Empty(await bob.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal("40", (await bob.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());
    }

    [Fact]
    public async Task Account_BuildsBusinessObjectEditsAsSelfUpdateTransactions()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-08T06:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectId objectId = new("self-updated-post");
        LedgerSubmitResult funding = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(15)));
        Assert.True(funding.Accepted);

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                objectId,
                "post",
                "{\"title\":\"draft\"}",
                MainAmount: Amount.FromInt64(15)),
            CancellationToken.None);
        Assert.True(create.Accepted, string.Join("; ", create.Errors));
        LedgerBusinessObjectHolding current = Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None));

        rig.Time.Advance(TimeSpan.FromMinutes(10));
        const string nextStateJson = "{\"title\":\"edited\",\"longDescription\":\"BO edit persisted by ledger update.\"}";
        LedgerTransaction update = await alice.BuildUpdateBusinessObjectTransactionAsync(
            new UpdateBusinessObjectRequest(objectId, nextStateJson, "bo-update://self-updated-post"),
            CancellationToken.None);

        AssertSingleSelfUpdateTransaction(
            update,
            current,
            Alice,
            objectId,
            Amount.FromInt64(15),
            expectedVersion: 2,
            nextStateJson,
            "bo-update://self-updated-post");

        LedgerSubmitResult result = await alice.SubmitTransactionAsync(update, CancellationToken.None);

        Assert.True(result.Accepted, string.Join("; ", result.Errors));
        LedgerBusinessObjectHolding updated = Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal(2, updated.Version);
        Assert.Equal(nextStateJson, updated.StateJson);
    }

    [Fact]
    public async Task Account_BuildsPriceChangesAsSelfUpdateTransactionsWithSaleTerms()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-08T07:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectId objectId = new("self-priced-post");
        LedgerSubmitResult funding = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(20)));
        Assert.True(funding.Accepted);

        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            BusinessObjectFamilyCatalog.FindOrDefault("post"),
            "Self priced post",
            "Sale setup test",
            "This object is listed by writing a BO update transaction back to the seller.",
            "#sale #ledger",
            Array.Empty<BusinessObjectDataItem>());
        string initialStateJson = BusinessObjectPayloadBuilder.Serialize(payload);
        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                objectId,
                "post",
                initialStateJson,
                MainAmount: Amount.FromInt64(20)),
            CancellationToken.None);
        Assert.True(create.Accepted, string.Join("; ", create.Errors));
        LedgerBusinessObjectHolding current = Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None));

        rig.Time.Advance(TimeSpan.FromMinutes(5));
        Amount price = Amount.FromDecimal(12.5m);
        BusinessObjectSaleTerms saleTerms = BusinessObjectCommercePayloadBuilder.CreateSaleTerms(
            BusinessObjectSaleMode.Original,
            price,
            Alice,
            rig.Time.UtcNow,
            sharePath: "/buy-bo/self-priced-post");
        string saleStateJson = BusinessObjectCommercePayloadBuilder.WithSaleTermsStateJson(
            initialStateJson,
            objectId.Value,
            saleTerms);
        LedgerTransaction priceUpdate = await alice.BuildSetBusinessObjectPriceTransactionAsync(
            new SetBusinessObjectPriceRequest(
                objectId,
                price,
                null,
                saleStateJson,
                "bo-commerce://sale/self-priced-post"),
            CancellationToken.None);

        AssertSingleSelfUpdateTransaction(
            priceUpdate,
            current,
            Alice,
            objectId,
            Amount.FromInt64(20),
            expectedVersion: 2,
            saleStateJson,
            "bo-commerce://sale/self-priced-post");
        Assert.True(BusinessObjectCommercePayloadBuilder.TryGetSaleTerms(
            priceUpdate.Data!.InlineJson,
            out BusinessObjectSaleTerms? transactionSaleTerms));
        Assert.Equal(BusinessObjectSaleStatus.Listed, transactionSaleTerms!.Status);
        Assert.Equal(BusinessObjectSaleMode.Original, transactionSaleTerms.Mode);
        Assert.Equal(price.Value, transactionSaleTerms.PriceAmount);
        Assert.Equal(Alice.Value, transactionSaleTerms.SellerAddress);

        LedgerSubmitResult result = await alice.SubmitTransactionAsync(priceUpdate, CancellationToken.None);

        Assert.True(result.Accepted, string.Join("; ", result.Errors));
        LedgerBusinessObjectHolding updated = Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal(2, updated.Version);
        Assert.Equal(saleStateJson, updated.StateJson);
        Assert.True(BusinessObjectCommercePayloadBuilder.TryGetSaleTerms(updated.StateJson, out BusinessObjectSaleTerms? persistedSaleTerms));
        Assert.Equal(price.Value, persistedSaleTerms!.PriceAmount);
        Assert.Equal(Alice.Value, persistedSaleTerms.SellerAddress);
    }

    [Fact]
    public async Task TicketVerification_ValidatesLedgerHistoryAndRejectsUsedTickets()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-08T08:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectId eventId = new("bo:event:ledger-conf-2026");
        BusinessObjectId ticketId = new("bo:ticket:ledger-conf-2026-001");
        LedgerSubmitResult funding = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(30)));
        Assert.True(funding.Accepted);

        LedgerSubmitResult createEvent = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                eventId,
                "event",
                CreateEventStateJson(eventId),
                MainAmount: Amount.FromInt64(10)),
            CancellationToken.None);
        Assert.True(createEvent.Accepted, string.Join("; ", createEvent.Errors));

        LedgerSubmitResult createTicket = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                ticketId,
                "ticket",
                CreateTicketStateJson(ticketId, eventId, used: false),
                MainAmount: Amount.FromInt64(20)),
            CancellationToken.None);
        Assert.True(createTicket.Accepted, string.Join("; ", createTicket.Errors));

        IReadOnlyList<LedgerTransactionSummary> historyBeforeUse = await rig.Gateway.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                Context,
                BusinessObjectId: ticketId,
                IncludeData: true),
            CancellationToken.None);
        BusinessObjectTicketHistoryValidation valid = BusinessObjectTicketVerification.ValidateHistory(
            "ticket",
            ticketId,
            historyBeforeUse);

        Assert.True(valid.IsValid, valid.Summary);
        Assert.Equal(createTicket.TxId, valid.OriginTxId);
        Assert.Contains(valid.Entries, entry => entry.Version == 1 && !entry.Used);

        LedgerBusinessObjectHolding ticket = (await alice.GetBusinessObjectsAsync(CancellationToken.None))
            .Single(holding => holding.ObjectId == ticketId);
        rig.Time.Advance(TimeSpan.FromMinutes(15));
        string usedStateJson = BusinessObjectTicketVerification.BuildUsedStateJson(
            ticket.StateJson,
            ticketId,
            rig.Time.UtcNow,
            new HashHex(new string('1', 64)));
        LedgerSubmitResult useTicket = await alice.UpdateBusinessObjectAsync(
            new UpdateBusinessObjectRequest(ticketId, usedStateJson, "ticket-used://ledger-conf-2026-001"),
            CancellationToken.None);
        Assert.True(useTicket.Accepted, string.Join("; ", useTicket.Errors));

        IReadOnlyList<LedgerTransactionSummary> historyAfterUse = await rig.Gateway.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                Context,
                BusinessObjectId: ticketId,
                IncludeData: true),
            CancellationToken.None);
        BusinessObjectTicketHistoryValidation used = BusinessObjectTicketVerification.ValidateHistory(
            "ticket",
            ticketId,
            historyAfterUse);

        Assert.False(used.IsValid);
        Assert.True(used.IsUsed);
        Assert.Equal(createTicket.TxId, used.OriginTxId);
        Assert.Equal(useTicket.TxId, used.UsedTxId);
        Assert.Contains(used.Entries, entry => entry.Version == 2 && entry.Used);
        Assert.Contains("\"used\":\"true\"", usedStateJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lastVerificationHash", usedStateJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Account_RejectsInvalidAdvancedRequestsBeforeAssembly()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult funding = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(100)));
        Assert.True(funding.Accepted);

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("work-order"),
                "work-order",
                "{\"status\":\"draft\"}",
                MainAmount: Amount.FromInt64(100)),
            CancellationToken.None);
        Assert.True(create.Accepted);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => alice.BuildCreateBusinessObjectTransactionAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("negative-create"),
                "work-order",
                "{}",
                MainAmount: Amount.FromInt64(-1)),
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => alice.BuildTransferBusinessObjectTransactionAsync(
            new TransferBusinessObjectRequest(
                new BusinessObjectId("work-order"),
                Bob,
                Amount.FromInt64(-1)),
            CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => alice.BuildCloneBusinessObjectTransactionAsync(
            new CloneBusinessObjectRequest(
                new BusinessObjectId("work-order"),
                new BusinessObjectId("work-order"),
                "work-order",
                "{}"),
            CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => alice.BuildMergeBusinessObjectsTransactionAsync(
            new MergeBusinessObjectsRequest(
                new BusinessObjectId("work-order"),
                new BusinessObjectId("work-order"),
                new BusinessObjectId("merged-work-order"),
                "{}"),
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => alice.BuildSplitBusinessObjectPaymentTransactionAsync(
            new SplitBusinessObjectPaymentRequest(
                new BusinessObjectId("work-order"),
                new[]
                {
                    new BusinessObjectPaymentOutput(Bob, Amount.FromInt64(-1))
                }),
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => alice.BuildPaymentTransactionAsync(
            new PaymentRequest(Bob, Amount.Zero),
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => alice.BuildTokenTransferTransactionAsync(
            new TokenTransferRequest(OperatingToken, Amount.Zero, Bob),
            CancellationToken.None));
    }

    [Fact]
    public async Task Account_SendsPlainPaymentsFromNonBusinessObjectOutputs()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T00:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount bob = rig.CreateAccount("bob");

        LedgerSubmitResult mint = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromDecimal(40.25m)),
            new TransactionOutput(1, Alice, Amount.FromDecimal(60.75m)));
        Assert.True(mint.Accepted);

        rig.Time.Advance(TimeSpan.FromMinutes(5));
        LedgerTransaction payment = await alice.BuildPaymentTransactionAsync(
            new PaymentRequest(Bob, Amount.FromDecimal(70.50m)),
            CancellationToken.None);

        Assert.Equal(TransactionKind.Transfer, payment.Kind);
        Assert.Equal(DateTimeOffset.Parse("2026-05-09T00:05:00Z"), payment.Timestamp);
        Assert.Equal(2, payment.Inputs.Count);
        Assert.All(payment.Inputs, input => Assert.NotEmpty(input.SignatureBundle.Signatures));
        Assert.Equal(Bob, payment.Outputs[0].Address);
        Assert.Equal("70.5", payment.Outputs[0].MainAmount.ToString());
        Assert.Equal(Alice, payment.Outputs[1].Address);
        Assert.Equal("30.5", payment.Outputs[1].MainAmount.ToString());

        LedgerSubmitResult paymentResult = await alice.SubmitTransactionAsync(payment, CancellationToken.None);

        Assert.True(paymentResult.Accepted);
        Assert.Equal("30.5", (await alice.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());
        Assert.Equal("70.5", (await bob.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());
    }

    [Fact]
    public async Task Account_SendsTokenTransfersWithMainAndTokenChange()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T01:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount bob = rig.CreateAccount("bob");

        LedgerSubmitResult mint = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(10), new TokenTransfer(OperatingToken, Amount.FromInt64(100))));
        Assert.True(mint.Accepted);

        rig.Time.Advance(TimeSpan.FromMinutes(10));
        LedgerSubmitResult transfer = await alice.SendTokenTransferAsync(
            new TokenTransferRequest(OperatingToken, Amount.FromInt64(25), Bob, Amount.FromInt64(2)),
            CancellationToken.None);

        Assert.True(transfer.Accepted);
        LedgerAccountBalance aliceBalance = await alice.GetBalanceAsync(CancellationToken.None);
        LedgerAccountBalance bobBalance = await bob.GetBalanceAsync(CancellationToken.None);
        Assert.Equal("8", aliceBalance.MainBalance.ToString());
        Assert.Equal("75", aliceBalance.TokenBalances[OperatingToken].ToString());
        Assert.Equal("2", bobBalance.MainBalance.ToString());
        Assert.Equal("25", bobBalance.TokenBalances[OperatingToken].ToString());
    }

    [Fact]
    public async Task Account_CreatesTokenBackedBusinessObjectFromNonUniqueTokenSource()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T01:30:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult mint = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(OperatingToken, Amount.FromInt64(1))),
            new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(OperatingToken, Amount.FromInt64(5))));
        Assert.True(mint.Accepted);

        LedgerTransaction create = await alice.BuildCreateBusinessObjectTransactionAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("token-card"),
                "token-card",
                "{\"name\":\"source-created\"}",
                Token: new TokenTransfer(OperatingToken, Amount.FromInt64(1))),
            CancellationToken.None);

        TransactionInput input = Assert.Single(create.Inputs);
        Assert.Equal(1, input.PreviousOutPoint.OutputIndex);
        Assert.Equal(2, create.Outputs.Count);
        Assert.Equal("1", create.Outputs[0].Token!.Amount.ToString());
        Assert.Equal("4", create.Outputs[1].Token!.Amount.ToString());
        Assert.NotNull(create.Outputs[0].BusinessObject);
        Assert.Null(create.Outputs[1].BusinessObject);

        LedgerSubmitResult result = await alice.SubmitTransactionAsync(create, CancellationToken.None);

        Assert.True(result.Accepted);
        LedgerTokenType tokenType = Assert.Single(await alice.GetTokenTypesAsync(CancellationToken.None));
        Assert.Equal(OperatingToken, tokenType.TokenId);
        Assert.Equal("6", tokenType.TotalAmount.ToString());
        Assert.Equal("4", tokenType.SourceAmount.ToString());
        Assert.Equal("2", tokenType.UniqueAmount.ToString());
        Assert.Equal(1, tokenType.SourceUtxoCount);
        Assert.Equal(2, tokenType.UniqueUtxoCount);
        Assert.Equal(3, tokenType.TotalUtxoCount);
    }

    [Fact]
    public async Task Account_CreateBusinessObjectWritesStructuredStateJsonForEveryObjectType()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T01:32:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult mint = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(10)));
        Assert.True(mint.Accepted);

        LedgerTransaction create = await alice.BuildCreateBusinessObjectTransactionAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("gallery-post-structured"),
                "gallery-post",
                "{\"title\":\"Structured post\",\"meta\":{\"tags\":[\"ui\",\"raw\"]}}",
                MainAmount: Amount.FromInt64(1)),
            CancellationToken.None);

        string inlineJson = Assert.IsType<string>(create.Data?.InlineJson);
        using JsonDocument document = JsonDocument.Parse(inlineJson);

        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("stateJson").ValueKind);
        Assert.DoesNotContain("\\u0022", inlineJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\\\"", inlineJson, StringComparison.Ordinal);
        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(inlineJson, out BusinessObjectPayloadEnvelope envelope));
        Assert.Equal("gallery-post", envelope.ObjectType);
        Assert.Equal("{\"title\":\"Structured post\",\"meta\":{\"tags\":[\"ui\",\"raw\"]}}", envelope.StateJson);
    }

    [Fact]
    public async Task Account_AggregatesTransferredTokenBackedBusinessObjectsAsUniqueUtxos()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T01:35:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount bob = rig.CreateAccount("bob");
        LedgerSubmitResult mint = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(OperatingToken, Amount.FromInt64(5))));
        Assert.True(mint.Accepted);

        for (var i = 1; i <= 3; i++)
        {
            BusinessObjectId objectId = new($"token-card-{i}");
            LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    objectId,
                    "token-card",
                    $$"""{"index":{{i}}}""",
                    Token: new TokenTransfer(OperatingToken, Amount.FromInt64(1))),
                CancellationToken.None);
            Assert.True(create.Accepted, string.Join("; ", create.Errors));

            LedgerSubmitResult transfer = await alice.TransferBusinessObjectAsync(
                new TransferBusinessObjectRequest(objectId, Bob),
                CancellationToken.None);
            Assert.True(transfer.Accepted, string.Join("; ", transfer.Errors));
        }

        LedgerTokenType aliceType = Assert.Single(await alice.GetTokenTypesAsync(CancellationToken.None));
        Assert.Equal(OperatingToken, aliceType.TokenId);
        Assert.Equal("2", aliceType.TotalAmount.ToString());
        Assert.Equal("2", aliceType.SourceAmount.ToString());
        Assert.Equal("0", aliceType.UniqueAmount.ToString());
        Assert.Equal(1, aliceType.SourceUtxoCount);
        Assert.Equal(0, aliceType.UniqueUtxoCount);
        Assert.Equal(1, aliceType.TotalUtxoCount);

        LedgerTokenType bobType = Assert.Single(await bob.GetTokenTypesAsync(CancellationToken.None));
        Assert.Equal(OperatingToken, bobType.TokenId);
        Assert.Equal("3", bobType.TotalAmount.ToString());
        Assert.Equal("0", bobType.SourceAmount.ToString());
        Assert.Equal("3", bobType.UniqueAmount.ToString());
        Assert.Equal(0, bobType.SourceUtxoCount);
        Assert.Equal(3, bobType.UniqueUtxoCount);
        Assert.Equal(3, bobType.TotalUtxoCount);
    }

    [Fact]
    public async Task Account_DoesNotUseUniqueTokenUtxoAsBusinessObjectSource()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T01:45:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult mint = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(OperatingToken, Amount.FromInt64(1))));
        Assert.True(mint.Accepted);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => alice.BuildCreateBusinessObjectTransactionAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("token-card"),
                "token-card",
                "{}",
                Token: new TokenTransfer(OperatingToken, Amount.FromInt64(1))),
            CancellationToken.None));

        Assert.Contains("non-unique token source UTXO", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Account_CanCreateBusinessObjectFromExplicitTokenSourceUtxo()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T01:50:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult mint = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(OperatingToken, Amount.FromInt64(5))),
            new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(OperatingToken, Amount.FromInt64(4))));
        Assert.True(mint.Accepted);
        OutPoint explicitSource = new(mint.TxId!.Value, 1);

        LedgerTransaction create = await alice.BuildCreateBusinessObjectTransactionAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("explicit-token-card"),
                "token-card",
                "{}",
                Token: new TokenTransfer(OperatingToken, Amount.FromInt64(1)),
                SourceOutPoint: explicitSource),
            CancellationToken.None);

        TransactionInput input = Assert.Single(create.Inputs);
        Assert.Equal(explicitSource, input.PreviousOutPoint);
        Assert.Equal("3", create.Outputs[1].Token!.Amount.ToString());
    }

    [Fact]
    public async Task Account_IssuesPermissionAndMintsTokenTypeFromPermissionSource()
    {
        TestRig rig = CreateRig(
            DateTimeOffset.Parse("2026-05-09T01:55:00Z"),
            enforceTokenPolicy: true,
            includeRootPermission: true);
        BusinessObjectAccount admin = rig.CreateAccount("bootstrap");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount bob = rig.CreateAccount("bob");

        LedgerSubmitResult issue = await admin.IssuePermissionAsync(
            new PermissionIssueRequest(Alice, "messages", Amount.FromInt64(2), Repeatable: true),
            CancellationToken.None);
        Assert.True(issue.Accepted, string.Join("; ", issue.Errors));

        LedgerPermissionSource source = Assert.Single(await alice.GetMintPermissionSourcesAsync(
            new TokenId("messages.production"),
            CancellationToken.None));
        Assert.True(source.Grant.Repeatable);
        Assert.Equal("messages", source.Grant.Namespace);

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult mint = await alice.MintTokenAsync(
            new TokenMintRequest(
                new TokenId("messages.production"),
                Amount.FromInt64(5),
                PermissionSourceOutPoint: source.OutPoint),
            CancellationToken.None);
        Assert.True(mint.Accepted, string.Join("; ", mint.Errors));

        LedgerAccountBalance aliceBalance = await alice.GetBalanceAsync(CancellationToken.None);
        Assert.Equal("5", aliceBalance.TokenBalances[new TokenId("messages.production")].ToString());

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult delegated = await alice.IssuePermissionAsync(
            new PermissionIssueRequest(Bob, "messages.production", Amount.FromInt64(1), Repeatable: true),
            CancellationToken.None);
        Assert.True(delegated.Accepted, string.Join("; ", delegated.Errors));

        Assert.Single(await bob.GetMintPermissionSourcesAsync(new TokenId("messages.production.alert"), CancellationToken.None));
        Assert.Empty(await bob.GetMintPermissionSourcesAsync(new TokenId("materials.iron"), CancellationToken.None));
    }

    [Fact]
    public async Task Account_DoesNotSpendBusinessObjectsForPlainPayments()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T02:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult funding = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(100)));
        Assert.True(funding.Accepted);

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("funded-object"),
                "settings",
                "{\"theme\":\"dense\"}",
                MainAmount: Amount.FromInt64(100)),
            CancellationToken.None);
        Assert.True(create.Accepted);
        Assert.Equal("100", (await alice.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => alice.BuildPaymentTransactionAsync(
            new PaymentRequest(Bob, Amount.FromInt64(1)),
            CancellationToken.None));

        Assert.Contains("non-BusinessObject", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Account_ReadsCastableBusinessObjectViewsFromLedgerProjection()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-09T03:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult funding = await rig.SubmitMintAsync(
            CancellationToken.None,
            new TransactionOutput(0, Alice, Amount.FromInt64(10)));
        Assert.True(funding.Accepted);
        var registry = new BusinessObjectCastRegistry(new IBusinessObjectCaster[]
        {
            new AccountSettingsBusinessObjectCaster(rig.Time),
            new GenericBusinessObjectCaster(rig.Time)
        });

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                new BusinessObjectId("account-settings"),
                "settings",
                "{\"theme\":\"dense\"}",
                MainAmount: Amount.FromInt64(10),
                ItemType: TransactionDataItemType.AccountSettings),
            CancellationToken.None);
        Assert.True(create.Accepted);

        rig.Time.Advance(TimeSpan.FromMinutes(15));
        LedgerSubmitResult update = await alice.UpdateBusinessObjectAsync(
            new UpdateBusinessObjectRequest(new BusinessObjectId("account-settings"), "{\"theme\":\"light\"}"),
            CancellationToken.None);
        Assert.True(update.Accepted);

        LedgerBusinessObjectHolding holding = Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal("settings", holding.ObjectType);
        Assert.Equal("1.0", holding.SchemaVersion);
        Assert.Equal("{\"theme\":\"light\"}", holding.StateJson);
        Assert.Equal(TransactionDataItemType.BusinessObject, holding.ItemType);

        IBusinessObjectView view = Assert.Single(await alice.GetBusinessObjectViewsAsync(registry, CancellationToken.None));
        var settings = Assert.IsType<AccountSettingsBusinessObjectView>(view);
        Assert.Equal("light", settings.Settings["theme"]);
        Assert.Equal(DateTimeOffset.Parse("2026-05-09T03:15:00Z"), settings.ViewedAt);
    }

    private static void AssertSingleSelfUpdateTransaction(
        LedgerTransaction transaction,
        LedgerBusinessObjectHolding current,
        Address owner,
        BusinessObjectId objectId,
        Amount expectedMainAmount,
        int expectedVersion,
        string expectedStateJson,
        string? expectedExternalRef)
    {
        Assert.Equal(TransactionKind.BusinessObject, transaction.Kind);
        TransactionInput input = Assert.Single(transaction.Inputs);
        Assert.Equal(owner, input.Address);
        Assert.Equal(current.CurrentOutPoint, input.PreviousOutPoint);
        Assert.NotEmpty(input.SignatureBundle.Signatures);

        TransactionOutput output = Assert.Single(transaction.Outputs);
        Assert.Equal(owner, output.Address);
        Assert.Equal(expectedMainAmount, output.MainAmount);
        Assert.Null(output.Token);
        BusinessObjectRef? businessObject = output.BusinessObject;
        Assert.NotNull(businessObject);
        Assert.Equal(objectId, businessObject!.ObjectId);
        Assert.Equal(expectedVersion, businessObject.Version);
        Assert.Equal(BusinessObjectOperation.Update, businessObject.Operation);
        Assert.NotEqual(current.DataHash, businessObject.DataHash);

        TransactionData? data = transaction.Data;
        Assert.NotNull(data);
        Assert.Equal(TransactionDataItemType.BusinessObject, data!.ItemType);
        Assert.Equal(expectedStateJson, data.InlineJson);
        Assert.Equal(expectedExternalRef, data.ExternalRef);
        Assert.NotNull(data.DataHash);
        Assert.Equal(data.DataHash.Value, businessObject.DataHash);
    }

    private static string CreateEventStateJson(BusinessObjectId objectId)
    {
        var fields = new Dictionary<string, string>
        {
            ["eventId"] = objectId.Value,
            ["eventDate"] = "2026-07-01T18:00:00Z",
            ["eventClass"] = "Conference",
            ["location"] = "Prague",
            ["locationCoordinates"] = "50.087,14.421",
            ["organizer"] = "CanDoItAll",
            ["mintAuthorAddress"] = Alice.Value,
            ["status"] = "Scheduled"
        };
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            BusinessObjectFamilyCatalog.FindOrDefault("event"),
            "Ledger Conference",
            "Event used by ticket verification tests.",
            "Ledger Conference validates event and ticket BO relationships.",
            "#event #ledger",
            Array.Empty<BusinessObjectDataItem>(),
            fields);
        return BusinessObjectPayloadBuilder.Serialize(payload);
    }

    private static string CreateTicketStateJson(BusinessObjectId ticketId, BusinessObjectId eventId, bool used)
    {
        var fields = new Dictionary<string, string>
        {
            ["ticketNumber"] = "001",
            ["eventId"] = eventId.Value,
            ["eventAddress"] = Alice.Value,
            ["eventDate"] = "2026-07-01T18:00:00Z",
            ["location"] = "Prague",
            ["locationCoordinates"] = "50.087,14.421",
            ["ticketClass"] = "VIP",
            ["ticketDuration"] = "Day",
            ["holder"] = "Alice",
            ["seat"] = "A1",
            ["used"] = used ? "true" : "false",
            ["status"] = used ? "Used" : "FreeToUse",
            ["externalId"] = ticketId.Value
        };
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            BusinessObjectFamilyCatalog.FindOrDefault("ticket"),
            "Ledger Conference Ticket 001",
            "Ticket related to Ledger Conference.",
            "Ticket verification follows BO history back to its origin.",
            "#ticket #event",
            Array.Empty<BusinessObjectDataItem>(),
            fields);
        return BusinessObjectPayloadBuilder.Serialize(payload);
    }

    private static TestRig CreateRig(
        DateTimeOffset utcNow,
        bool enforceTokenPolicy = false,
        bool includeRootPermission = false)
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(utcNow);
        ITokenCreationPolicy? tokenCreationPolicy = enforceTokenPolicy
            ? new TokenCreationPolicy(new TokenCreationPolicyOptions { TreasuryAdminAddress = Bootstrap })
            : null;
        var gateway = new InProcessLedgerGateway(
            new LedgerExecutor(
                store,
                hasher,
                new SignaturePayloadBuilder(hasher),
                new TestAdminSigner(),
                timeProvider,
                new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() }),
                tokenCreationPolicy),
            hasher,
            timeProvider);
        LedgerSubmitResult genesis = gateway.SubmitTransactionAsync(CreateBootstrapGenesis(timeProvider.UtcNow, includeRootPermission), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.True(genesis.Accepted);

        var signer = new AccountInputSigner(new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() }));
        var builder = new BusinessObjectTransactionBuilder(gateway, hasher, new SignaturePayloadBuilder(hasher), signer, timeProvider);
        return new TestRig(gateway, builder, timeProvider);
    }

    private static LedgerTransaction CreateBootstrapGenesis(DateTimeOffset timestamp, bool includeRootPermission)
    {
        var outputs = new List<TransactionOutput>
        {
            new(0, Bootstrap, Amount.Zero)
        };
        if (includeRootPermission)
        {
            outputs.Add(new TransactionOutput(
                1,
                Bootstrap,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(10_000))));
        }

        return new LedgerTransaction(
            LedgerId,
            Context.ScenarioId,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            outputs,
            null);
    }

    private sealed class TestRig(InProcessLedgerGateway gateway, BusinessObjectTransactionBuilder builder, ManualTimeProvider time)
    {
        public InProcessLedgerGateway Gateway { get; } = gateway;

        public BusinessObjectTransactionBuilder Builder { get; } = builder;

        public ManualTimeProvider Time { get; } = time;

        public BusinessObjectAccount CreateAccount(string keyId)
            => new(Context, CreateKey(keyId), Gateway, Gateway, Builder);

        public IAccountKeySource CreateKey(string keyId)
            => new InMemoryAccountKeySource(
                SignatureAlgorithmId.EcdsaP256Sha256,
                keyId,
                Hex.Utf8($"{keyId}-public"),
                Hex.Utf8($"{keyId}-private"));

        public async Task<LedgerSubmitResult> SubmitMintAsync(CancellationToken cancellationToken, params TransactionOutput[] outputs)
        {
            IAccountKeySource bootstrapKey = CreateKey("bootstrap");
            LedgerSpendableOutput selected = (await Gateway
                .GetSpendableOutputsAsync(new LedgerAddressQuery(Context, Bootstrap), cancellationToken)
                .ConfigureAwait(false))
                .First(output => output.Token is null && output.BusinessObject is null);

            var allOutputs = outputs.ToList();
            allOutputs.Add(new TransactionOutput(allOutputs.Count, Bootstrap, selected.MainAmount, selected.Token, selected.BusinessObject));

            var unsigned = new LedgerTransaction(
                LedgerId,
                Context.ScenarioId,
                Version: 1,
                TransactionKind.Mint,
                SigningMode.Independent,
                Time.UtcNow,
                new[] { new TransactionInput(0, selected.OutPoint, Bootstrap, SignatureBundle.Empty) },
                allOutputs,
                null);

            return await Gateway.SubmitTransactionAsync(
                await SignAsync(unsigned, bootstrapKey, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<LedgerTransaction> SignAsync(
            LedgerTransaction transaction,
            IAccountKeySource keySource,
            CancellationToken cancellationToken)
        {
            var hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var payloadBuilder = new SignaturePayloadBuilder(hasher);
            var signer = new AccountInputSigner(new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() }));
            LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
            var signedInputs = new List<TransactionInput>();

            foreach (TransactionInput input in withTxId.Inputs.OrderBy(x => x.InputIndex))
            {
                HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
                SignatureEnvelope signature = await signer.SignAsync(keySource, payloadHash, cancellationToken).ConfigureAwait(false);
                signedInputs.Add(input with
                {
                    SignaturePayloadHash = payloadHash,
                    SignatureBundle = new SignatureBundle(new[] { signature })
                });
            }

            return withTxId with { Inputs = signedInputs };
        }
    }

    private sealed class EchoSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => System.Text.Encoding.UTF8.GetBytes($"{System.Text.Encoding.UTF8.GetString(payload)}|{System.Text.Encoding.UTF8.GetString(privateKeyMaterial)}");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => signature.Length > 0 && payload.Length > 0 && publicKeyMaterial.Length > 0;
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "test-admin",
                    Hex.Sha256Hex(Hex.Utf8("admin-public")),
                    payloadHash,
                    Hex.Utf8("admin-signature"))
            });
    }
}
