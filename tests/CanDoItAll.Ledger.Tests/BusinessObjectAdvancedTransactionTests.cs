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
        public ManualTimeProvider Time { get; } = time;

        public BusinessObjectAccount CreateAccount(string keyId)
            => new(Context, CreateKey(keyId), gateway, gateway, builder);

        public IAccountKeySource CreateKey(string keyId)
            => new InMemoryAccountKeySource(
                SignatureAlgorithmId.EcdsaP256Sha256,
                keyId,
                Hex.Utf8($"{keyId}-public"),
                Hex.Utf8($"{keyId}-private"));

        public async Task<LedgerSubmitResult> SubmitMintAsync(CancellationToken cancellationToken, params TransactionOutput[] outputs)
        {
            IAccountKeySource bootstrapKey = CreateKey("bootstrap");
            LedgerSpendableOutput selected = (await gateway
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

            return await gateway.SubmitTransactionAsync(
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
