using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectAccountRefreshTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("refresh-ledger"));
    private static readonly TokenId CarrierToken = new("bo.carrier");

    [Fact]
    public async Task RefreshAsync_EmitsOnlyTransactionsObservedAfterBaseline()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-13T10:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        IAccountKeySource bobKey = CreateKeySource("bob");
        var observed = new List<AccountTransactionsObservedEventArgs>();
        alice.NewTransactionsObserved += (_, args) => observed.Add(args);

        AccountRefreshResult baseline = await alice.RefreshAsync(
            new AccountRefreshOptions { MaxTransactionCount = 20 },
            CancellationToken.None);

        Assert.Empty(baseline.Delta.NewTransactions);
        Assert.Empty(observed);

        rig.Time.Advance(TimeSpan.FromMinutes(5));
        LedgerSubmitResult payment = await alice.SendPaymentAsync(
            new PaymentRequest(bobKey.Address, Amount.FromInt64(10)),
            CancellationToken.None);
        Assert.True(payment.Accepted);
        TxId paymentTxId = payment.TxId ?? throw new InvalidOperationException("Payment must have a transaction id.");

        AccountRefreshResult refreshed = await alice.RefreshAsync(
            new AccountRefreshOptions { MaxTransactionCount = 20 },
            CancellationToken.None);

        LedgerTransactionSummary newTransaction = Assert.Single(refreshed.Delta.NewTransactions);
        Assert.Equal(paymentTxId, newTransaction.TxId);
        AccountTransactionsObservedEventArgs eventArgs = Assert.Single(observed);
        Assert.Equal(alice, eventArgs.Account);
        Assert.Equal(paymentTxId, Assert.Single(eventArgs.NewTransactions).TxId);
    }

    [Fact]
    public async Task RefreshAsync_ReusesUnchangedBusinessObjectViewsAndRemovesTransferredObjects()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-13T11:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount bob = rig.CreateAccount("bob");
        var caster = new CountingBusinessObjectCaster(DateTimeOffset.Parse("2026-05-13T11:00:00Z"));
        var castRegistry = new BusinessObjectCastRegistry(new IBusinessObjectCaster[] { caster });
        var options = new AccountRefreshOptions { MaxTransactionCount = 20 };

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "settings",
                "{\"theme\":\"refresh\"}",
                MainAmount: Amount.FromInt64(100),
                ItemType: TransactionDataItemType.AccountSettings),
            CancellationToken.None);
        Assert.True(create.Accepted);
        BusinessObjectId objectId = CreatedObjectId(create);

        AccountRefreshResult first = await alice.RefreshAsync(castRegistry, options, CancellationToken.None);
        IBusinessObjectView firstView = Assert.Single(first.Snapshot.BusinessObjectViews);
        Assert.Equal(1, caster.CastCount);

        AccountRefreshResult second = await alice.RefreshAsync(castRegistry, options, CancellationToken.None);
        IBusinessObjectView secondView = Assert.Single(second.Snapshot.BusinessObjectViews);
        Assert.Same(firstView, secondView);
        Assert.Equal(1, caster.CastCount);

        rig.Time.Advance(TimeSpan.FromMinutes(10));
        LedgerSubmitResult transfer = await alice.TransferBusinessObjectAsync(
            new TransferBusinessObjectRequest(objectId, bob.Address),
            CancellationToken.None);
        Assert.True(transfer.Accepted);
        BusinessObjectId transferredObjectId = CreatedObjectId(transfer);

        AccountRefreshResult aliceAfterTransfer = await alice.RefreshAsync(castRegistry, options, CancellationToken.None);
        Assert.Empty(aliceAfterTransfer.Snapshot.BusinessObjectViews);
        LedgerBusinessObjectHolding removed = Assert.Single(aliceAfterTransfer.Delta.RemovedBusinessObjects);
        Assert.Equal(objectId, removed.ObjectId);
        Assert.Equal(1, caster.CastCount);

        AccountRefreshResult bobAfterTransfer = await bob.RefreshAsync(castRegistry, options, CancellationToken.None);
        IBusinessObjectView bobView = Assert.Single(bobAfterTransfer.Snapshot.BusinessObjectViews);
        Assert.Equal(transferredObjectId, bobView.ObjectId);
        Assert.Equal(bob.Address, bobView.OwnerAddress);
        Assert.Equal(2, caster.CastCount);
    }

    [Fact]
    public async Task StartAutoRefreshAsync_ReturnsAwaitableHandleAndStopsCleanly()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-13T12:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");

        await using IAccountRefreshHandle handle = await alice.StartAutoRefreshAsync(
            new AccountRefreshOptions
            {
                Interval = TimeSpan.FromMilliseconds(25),
                MaxTransactionCount = 20
            },
            CancellationToken.None);

        Assert.Equal(alice, handle.Account);
        Assert.NotNull(alice.CurrentRefreshSnapshot);

        await handle.StopAsync();

        Assert.True(handle.RunningTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RefreshObserver_TracksPaymentsBusinessObjectTransfersAndSubAccountEventsAcrossAccounts()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-13T15:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount bob = rig.CreateAccount("bob");
        BusinessObjectSubAccount operations = alice.AddSubAccount("operations", CreateKeySource("alice-operations"));
        var handler = new LedgerAccountHandler();
        var observed = new List<AccountTransactionsObservedEventArgs>();
        var routedParentEvents = new List<AccountTransactionsObservedEventArgs>();
        handler.NewTransactionsObserved += (_, args) => observed.Add(args);
        alice.NewTransactionsObserved += (_, args) => routedParentEvents.Add(args);
        var caster = new CountingBusinessObjectCaster(DateTimeOffset.Parse("2026-05-13T15:00:00Z"));
        var castRegistry = new BusinessObjectCastRegistry(new IBusinessObjectCaster[] { caster });
        var options = new AccountRefreshOptions { MaxTransactionCount = 50 };

        await handler.RefreshAsync(alice, castRegistry, options, CancellationToken.None);
        await handler.RefreshAsync(bob, castRegistry, options, CancellationToken.None);
        await handler.RefreshAsync(operations, castRegistry, options, CancellationToken.None);

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult payment = await alice.SendPaymentAsync(
            new PaymentRequest(bob.Address, Amount.FromInt64(7)),
            CancellationToken.None);
        Assert.True(payment.Accepted);
        TxId paymentTxId = payment.TxId ?? throw new InvalidOperationException("Payment must have a transaction id.");

        AccountRefreshResult bobAfterPayment = await handler.RefreshAsync(bob, castRegistry, options, CancellationToken.None);
        Assert.Equal(paymentTxId, Assert.Single(bobAfterPayment.Delta.NewTransactions).TxId);

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "asset",
                "{\"name\":\"refresh proof\"}",
                MainAmount: Amount.FromInt64(20),
                ItemType: TransactionDataItemType.AccountSettings),
            CancellationToken.None);
        Assert.True(create.Accepted);
        BusinessObjectId objectId = CreatedObjectId(create);

        AccountRefreshResult aliceAfterCreate = await handler.RefreshAsync(alice, castRegistry, options, CancellationToken.None);
        IBusinessObjectView aliceView = Assert.Single(aliceAfterCreate.Snapshot.BusinessObjectViews);
        Assert.Equal(objectId, aliceView.ObjectId);
        Assert.Equal(alice.Address, aliceView.OwnerAddress);
        Assert.Equal(1, caster.CastCount);

        AccountRefreshResult aliceUnchanged = await handler.RefreshAsync(alice, castRegistry, options, CancellationToken.None);
        Assert.Same(aliceView, Assert.Single(aliceUnchanged.Snapshot.BusinessObjectViews));
        Assert.Equal(1, caster.CastCount);

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult transfer = await alice.TransferBusinessObjectAsync(
            new TransferBusinessObjectRequest(objectId, bob.Address),
            CancellationToken.None);
        Assert.True(transfer.Accepted);
        TxId transferTxId = transfer.TxId ?? throw new InvalidOperationException("Transfer must have a transaction id.");
        BusinessObjectId transferredObjectId = BusinessObjectId.FromOutPoint(transferTxId, 0);

        AccountRefreshResult aliceAfterTransfer = await handler.RefreshAsync(alice, castRegistry, options, CancellationToken.None);
        Assert.DoesNotContain(aliceAfterTransfer.Snapshot.BusinessObjects, holding => holding.ObjectId == objectId);
        Assert.Equal(objectId, Assert.Single(aliceAfterTransfer.Delta.RemovedBusinessObjects).ObjectId);

        AccountRefreshResult bobAfterTransfer = await handler.RefreshAsync(bob, castRegistry, options, CancellationToken.None);
        IBusinessObjectView bobView = Assert.Single(bobAfterTransfer.Snapshot.BusinessObjectViews);
        Assert.Equal(transferredObjectId, bobView.ObjectId);
        Assert.Equal(bob.Address, bobView.OwnerAddress);
        Assert.Equal(transferTxId, Assert.Single(bobAfterTransfer.Delta.NewTransactions).TxId);
        Assert.Equal(2, caster.CastCount);

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult subAccountPayment = await alice.SendPaymentAsync(
            new PaymentRequest(operations.Address, Amount.FromInt64(3)),
            CancellationToken.None);
        Assert.True(subAccountPayment.Accepted);
        TxId subAccountPaymentTxId = subAccountPayment.TxId
            ?? throw new InvalidOperationException("Subaccount payment must have a transaction id.");

        AccountRefreshResult operationsAfterPayment = await handler.RefreshAsync(operations, castRegistry, options, CancellationToken.None);
        Assert.Equal(subAccountPaymentTxId, Assert.Single(operationsAfterPayment.Delta.NewTransactions).TxId);

        Assert.Contains(observed, args =>
            args.Account == bob
            && !args.IsSubAccountEvent
            && args.NewTransactions.Any(transaction => transaction.TxId == paymentTxId));
        Assert.Contains(observed, args =>
            args.Account == bob
            && !args.IsSubAccountEvent
            && args.NewTransactions.Any(transaction => transaction.TxId == transferTxId));
        Assert.Contains(observed, args =>
            args.IsSubAccountEvent
            && args.ParentAccount == alice
            && args.SubAccount == operations
            && args.NewTransactions.Any(transaction => transaction.TxId == subAccountPaymentTxId));
        Assert.Contains(routedParentEvents, args =>
            args.IsSubAccountEvent
            && args.SubAccount == operations
            && args.NewTransactions.Any(transaction => transaction.TxId == subAccountPaymentTxId));
    }

    private static BusinessObjectId CreatedObjectId(LedgerSubmitResult result)
        => BusinessObjectId.FromOutPoint(
            result.TxId ?? throw new InvalidOperationException("Accepted BusinessObject transaction must have a transaction id."),
            0);

    private static TestRig CreateRig(DateTimeOffset utcNow)
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(utcNow);
        var gateway = new InProcessLedgerGateway(
            new LedgerExecutor(
                store,
                hasher,
                new SignaturePayloadBuilder(hasher),
                new TestAdminSigner(),
                timeProvider,
                new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() })),
            hasher,
            timeProvider);
        LedgerSubmitResult genesis = gateway.SubmitTransactionAsync(
                CreateBootstrapGenesis(timeProvider.UtcNow),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.True(genesis.Accepted);

        var builder = new BusinessObjectTransactionBuilder(
            gateway,
            hasher,
            new SignaturePayloadBuilder(hasher),
            new AccountInputSigner(new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() })),
            timeProvider);

        return new TestRig(gateway, builder, timeProvider);
    }

    private static LedgerTransaction CreateBootstrapGenesis(DateTimeOffset timestamp)
    {
        IAccountKeySource aliceKey = CreateKeySource("alice");
        return new LedgerTransaction(
            Context.LedgerId,
            Context.ScenarioId,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, aliceKey.Address, Amount.FromInt64(100)),
                new TransactionOutput(1, aliceKey.Address, Amount.Zero, new TokenTransfer(CarrierToken, Amount.FromInt64(20)))
            },
            null);
    }

    private static IAccountKeySource CreateKeySource(string keyId)
        => new InMemoryAccountKeySource(
            SignatureAlgorithmId.EcdsaP256Sha256,
            keyId,
            Hex.Utf8($"{keyId}-public"),
            Hex.Utf8($"{keyId}-private"));

    private sealed record TestRig(
        InProcessLedgerGateway Gateway,
        BusinessObjectTransactionBuilder Builder,
        ManualTimeProvider Time)
    {
        public BusinessObjectAccount CreateAccount(string keyId)
        {
            IAccountKeySource keySource = CreateKeySource(keyId);
            return new BusinessObjectAccount(Context, keySource, Gateway, Gateway, Builder);
        }
    }

    private sealed class CountingBusinessObjectCaster(DateTimeOffset viewedAt) : IBusinessObjectCaster
    {
        public int CastCount { get; private set; }

        public int Priority => 0;

        public bool CanCast(BusinessObjectCastSource source) => true;

        public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
        {
            CastCount++;
            return ValueTask.FromResult<IBusinessObjectView>(new GenericBusinessObjectView(
                source.Context,
                source.ObjectId,
                source.ObjectType,
                source.SchemaVersion,
                source.Version,
                source.OwnerAddress,
                source.Operation,
                source.DataHash,
                source.CurrentOutPoint,
                source.StateJson,
                source.ExternalRef,
                source.ItemType,
                source.CreatedAt,
                viewedAt));
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
