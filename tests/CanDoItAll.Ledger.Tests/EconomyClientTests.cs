using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Sdk;
using CanDoItAll.Economy.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyClientTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("sdk-ledger"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly Address Warehouse = Address.FromPublicKey(Hex.Utf8("warehouse-public"));
    private static readonly Address Bootstrap = Address.FromPublicKey(Hex.Utf8("bootstrap-public"));
    private static readonly TokenId CarrierToken = new("bo.carrier");

    [Fact]
    public async Task Client_LoadsAccountExportsKeyAndSubAccountAndSubmitsTransactions()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-07T10:00:00Z"));
        EconomyClient client = rig.Client;
        EconomyAccountKeyEnvelope aliceKey = CreateKey("alice");

        BusinessObjectAccount alice = await client.LoadAccountAsync(Context, aliceKey, CancellationToken.None);

        Assert.Equal(Alice, alice.Address);
        Assert.Equal("alice", alice.KeySource.KeyId);

        EconomyAccountKeyEnvelope exported = await client.ExportAccountKeyAsync(alice.KeySource, CancellationToken.None);
        Assert.Equal(aliceKey.Address, exported.Address);
        Assert.Equal(aliceKey.KeyId, exported.KeyId);
        Assert.Equal(aliceKey.PrivateKeyMaterial, exported.PrivateKeyMaterial);

        BusinessObjectSubAccount subAccount = await client.CreateSubAccountAsync(
            alice,
            "warehouse",
            CreateKey("warehouse"),
            CancellationToken.None);

        EconomySubAccountEnvelope exportedSubAccount = await client.ExportSubAccountAsync(subAccount, CancellationToken.None);
        Assert.Equal("warehouse", exportedSubAccount.Name);
        Assert.Equal(Warehouse, exportedSubAccount.Key.Address);

        BusinessObjectSubAccount importedSubAccount = await client.ImportSubAccountAsync(alice, exportedSubAccount, CancellationToken.None);
        Assert.Equal(Warehouse, importedSubAccount.Address);
        Assert.Single(alice.SubAccounts);

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "settings",
                "{\"theme\":\"sdk\"}",
                MainAmount: Amount.FromInt64(100),
                ItemType: TransactionDataItemType.AccountSettings),
            CancellationToken.None);
        Assert.True(create.Accepted);
        TxId createTxId = create.TxId ?? throw new InvalidOperationException("Accepted create must have a transaction id.");
        BusinessObjectId createdObjectId = BusinessObjectId.FromOutPoint(createTxId, 0);

        rig.Time.Advance(TimeSpan.FromMinutes(30));
        LedgerTransaction transfer = await alice.BuildTransferBusinessObjectTransactionAsync(
            new TransferBusinessObjectRequest(createdObjectId, Bob, Amount.FromInt64(40)),
            CancellationToken.None);

        LedgerSubmitResult transferResult = await client.SubmitTransactionAsync(transfer, CancellationToken.None);

        Assert.True(transferResult.Accepted);
        TxId transferTxId = transferResult.TxId ?? throw new InvalidOperationException("Accepted transfer must have a transaction id.");
        Assert.Equal("60", (await client.GetBalanceAsync(Context, Alice, CancellationToken.None)).MainBalance.ToString());
        Assert.Equal("40", (await client.GetBalanceAsync(Context, Bob, CancellationToken.None)).MainBalance.ToString());
        Assert.Equal(3, (await client.GetTransactionsAsync(Context, Alice, CancellationToken.None)).Count);
        IReadOnlyList<LedgerTransactionSummary> aliceTransactions = await client.GetTransactionsAsync(Context, Alice, includeData: true, CancellationToken.None);
        LedgerTransactionSummary transferHistory = Assert.Single(aliceTransactions, x => x.TxId == transferTxId);
        Assert.Equal(2, transferHistory.Inputs.Count);
        LedgerTransactionInputSummary historyInput = transferHistory.Inputs[0];
        SignatureEnvelope historySignature = Assert.Single(historyInput.SignatureBundle.Signatures);
        Assert.NotNull(historyInput.SignaturePayloadHash);
        Assert.Equal(historyInput.SignaturePayloadHash, historySignature.PayloadHash);
        Assert.NotNull(historySignature.PublicKeyMaterial);
        Assert.Equal(Hex.Utf8("alice-public"), historySignature.PublicKeyMaterial!);
        Assert.Equal(Alice, Address.FromPublicKey(historySignature.PublicKeyMaterial!));
        Assert.Equal(Address.CalculatePublicKeyHash(historySignature.PublicKeyMaterial!), historySignature.PublicKeyHash);
        Assert.NotEmpty(historySignature.SignatureBytes);
        Assert.All(transferHistory.Inputs, input => Assert.NotEmpty(input.SignatureBundle.Signatures));
        Assert.Equal(DateTimeOffset.Parse("2026-05-07T10:30:00Z"), transfer.Timestamp);

        LedgerState? state = await client.GetLedgerStateAsync(Context, CancellationToken.None);
        Assert.NotNull(state);
        Assert.Equal(Context.LedgerId, state.LedgerId);
        Assert.Null(state.ScenarioId);
        Assert.Equal(3, state.CurrentSequence);

        LedgerTransactionSummary? transferDetails = await client.GetTransactionAsync(Context, transferTxId, includeData: true, CancellationToken.None);
        Assert.NotNull(transferDetails);
        Assert.Equal(transferTxId, transferDetails.TxId);
        Assert.Equal(TransactionKind.BusinessObject, transferDetails.Kind);
        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(
            transferDetails.Data?.InlineJson,
            out BusinessObjectPayloadEnvelope transferEnvelope));
        BusinessObjectDataReference previousVersion = Assert.Single(transferEnvelope.References);
        Assert.Equal(BusinessObjectReferenceRelationships.PreviousVersion, previousVersion.Relationship);
        Assert.Equal(createdObjectId, previousVersion.ObjectId);

        LedgerTransactionSummary? createDetails = await client.GetTransactionAsync(Context, createTxId, includeData: true, CancellationToken.None);
        Assert.NotNull(createDetails);
        Assert.NotNull(createDetails.Data?.InlineJson);
        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(
            createDetails.Data.InlineJson,
            out BusinessObjectPayloadEnvelope createEnvelope));
        Assert.Equal("settings", createEnvelope.ObjectType);
        Assert.Equal("1.0", createEnvelope.SchemaVersion);
        Assert.Equal("{\"theme\":\"sdk\"}", createEnvelope.StateJson);

        BusinessObjectId transferredObjectId = BusinessObjectId.FromOutPoint(transferTxId, 0);
        LedgerBusinessObjectHolding? currentSettings = await client.GetBusinessObjectAsync(
            new LedgerBusinessObjectQuery(Context, ObjectId: transferredObjectId),
            CancellationToken.None);
        Assert.NotNull(currentSettings);
        Assert.Equal(Bob, currentSettings.OwnerAddress);
        Assert.Equal("settings", currentSettings.ObjectType);
        Assert.Equal("{\"theme\":\"sdk\"}", currentSettings.StateJson);

        IReadOnlyList<LedgerBusinessObjectHolding> settingsByState = await client.QueryBusinessObjectsAsync(
            new LedgerBusinessObjectQuery(
                Context,
                ObjectType: "settings",
                StateProperties: new Dictionary<string, string> { ["theme"] = "sdk" }),
            CancellationToken.None);
        LedgerBusinessObjectHolding queriedSettings = Assert.Single(settingsByState);
        Assert.Equal(transferredObjectId, queriedSettings.ObjectId);

        IReadOnlyList<LedgerTransactionSummary> creationTransaction = await client.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                Context,
                BusinessObjectId: createdObjectId,
                IncludeData: true),
            CancellationToken.None);
        IReadOnlyList<LedgerTransactionSummary> transferTransaction = await client.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                Context,
                BusinessObjectId: transferredObjectId,
                IncludeData: true),
            CancellationToken.None);
        Assert.Equal(2, creationTransaction.Count);
        Assert.Equal(2, transferTransaction.Count);
        Assert.Contains(creationTransaction, transaction => transaction.TxId == createTxId);
        Assert.Contains(creationTransaction, transaction => transaction.TxId == transferTxId);
        Assert.Contains(transferTransaction, transaction => transaction.TxId == createTxId);
        Assert.Contains(transferTransaction, transaction => transaction.TxId == transferTxId);
    }

    [Fact]
    public async Task Client_UsesInjectedTimeAndPreservesScenarioIsolation()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-10T00:00:00Z"));
        EconomyClient client = rig.Client;
        EconomyAccountKeyEnvelope aliceKey = CreateKey("alice");
        var scenarioA = new LedgerAccountContext(Context.LedgerId, new ScenarioId("simulation-a"));
        var scenarioB = new LedgerAccountContext(Context.LedgerId, new ScenarioId("simulation-b"));
        await SeedGenesisAsync(rig, scenarioA, Amount.FromInt64(100), CancellationToken.None);
        await SeedGenesisAsync(rig, scenarioB, Amount.FromInt64(50), CancellationToken.None);

        BusinessObjectAccount aliceA = await client.LoadAccountAsync(scenarioA, aliceKey, CancellationToken.None);
        BusinessObjectAccount aliceB = await client.LoadAccountAsync(scenarioB, aliceKey, CancellationToken.None);

        LedgerSubmitResult createA = await aliceA.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest("settings", "{\"scenario\":\"a\"}", MainAmount: Amount.FromInt64(100)),
            CancellationToken.None);
        Assert.True(createA.Accepted);
        BusinessObjectId objectIdA = BusinessObjectId.FromOutPoint(
            createA.TxId ?? throw new InvalidOperationException("Accepted create must have a transaction id."),
            0);

        rig.Time.Advance(TimeSpan.FromDays(1));
        LedgerSubmitResult createB = await aliceB.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest("settings", "{\"scenario\":\"b\"}", MainAmount: Amount.FromInt64(50)),
            CancellationToken.None);
        Assert.True(createB.Accepted);

        rig.Time.Advance(TimeSpan.FromHours(2));
        LedgerTransaction transferA = await aliceA.BuildTransferBusinessObjectTransactionAsync(
            new TransferBusinessObjectRequest(objectIdA, Bob, Amount.FromInt64(30)),
            CancellationToken.None);

        Assert.Equal(scenarioA.ScenarioId, transferA.ScenarioId);
        Assert.Equal(DateTimeOffset.Parse("2026-05-11T02:00:00Z"), transferA.Timestamp);
        LedgerSubmitResult transferResult = await client.SubmitTransactionAsync(transferA, CancellationToken.None);
        Assert.True(transferResult.Accepted);

        Assert.Equal("70", (await client.GetBalanceAsync(scenarioA, Alice, CancellationToken.None)).MainBalance.ToString());
        Assert.Equal("30", (await client.GetBalanceAsync(scenarioA, Bob, CancellationToken.None)).MainBalance.ToString());
        Assert.Equal("50", (await client.GetBalanceAsync(scenarioB, Alice, CancellationToken.None)).MainBalance.ToString());
        Assert.Equal("0", (await client.GetBalanceAsync(scenarioB, Bob, CancellationToken.None)).MainBalance.ToString());
        Assert.Equal(3, (await client.GetTransactionsAsync(scenarioA, Alice, CancellationToken.None)).Count);
        Assert.Equal(2, (await client.GetTransactionsAsync(scenarioB, Alice, CancellationToken.None)).Count);
    }

    private static EconomyAccountKeyEnvelope CreateKey(string keyId)
        => new(
            SignatureAlgorithmId.EcdsaP256Sha256,
            keyId,
            Hex.Utf8($"{keyId}-public"),
            Hex.Utf8($"{keyId}-private"));

    private static TestRig CreateRig(DateTimeOffset utcNow)
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(utcNow);
        var gateway = new InProcessLedgerGateway(
            new LedgerExecutor(store, hasher, new SignaturePayloadBuilder(hasher), new TestAdminSigner(), timeProvider, new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() })),
            hasher,
            timeProvider);
        LedgerSubmitResult genesis = gateway.SubmitTransactionAsync(CreateBootstrapGenesis(Context, timeProvider.UtcNow), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.True(genesis.Accepted);

        var signer = new AccountInputSigner(new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() }));
        var builder = new BusinessObjectTransactionBuilder(gateway, hasher, new SignaturePayloadBuilder(hasher), signer, timeProvider);
        return new TestRig(new EconomyClient(gateway, gateway, builder), gateway, timeProvider);
    }

    private static Task<LedgerSubmitResult> SeedGenesisAsync(TestRig rig, LedgerAccountContext context, Amount amount, CancellationToken cancellationToken)
        => rig.Gateway.SubmitTransactionAsync(CreateBootstrapGenesis(context, rig.Time.UtcNow, amount), cancellationToken);

    private static LedgerTransaction CreateBootstrapGenesis(LedgerAccountContext context, DateTimeOffset timestamp)
        => CreateBootstrapGenesis(context, timestamp, Amount.FromInt64(100));

    private static LedgerTransaction CreateBootstrapGenesis(LedgerAccountContext context, DateTimeOffset timestamp, Amount amount)
        => new(
            context.LedgerId,
            context.ScenarioId,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, Alice, amount),
                new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(CarrierToken, Amount.FromInt64(20)))
            },
            null);

    private sealed record TestRig(EconomyClient Client, ILedgerGateway Gateway, ManualTimeProvider Time);

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
