using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyAccountHandlerTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("handler-ledger"));

    [Fact]
    public void AddBusinessObjectAccountLayer_PreservesCustomAccountHandler()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddScoped<IEconomyAccountHandler, ReplacementAccountHandler>()
            .AddBusinessObjectAccountLayer()
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = provider.CreateScope();

        Assert.IsType<ReplacementAccountHandler>(scope.ServiceProvider.GetRequiredService<IEconomyAccountHandler>());
        Assert.IsType<InMemoryAccountVaultStore>(scope.ServiceProvider.GetRequiredService<IAccountVaultStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AccountVaultService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAccountSetupService>());
    }

    [Fact]
    public async Task Handler_ManagesSingleRunningRefreshPerAccountAndStopsIt()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-13T13:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        var handler = new EconomyAccountHandler();
        var options = new AccountRefreshOptions
        {
            Interval = TimeSpan.FromMilliseconds(25),
            MaxTransactionCount = 20
        };

        IAccountRefreshHandle first = await handler.StartAutoRefreshAsync(alice, null, options, CancellationToken.None);
        IAccountRefreshHandle second = await handler.StartAutoRefreshAsync(alice, null, options, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(handler.RunningRefreshes);

        await handler.StopAutoRefreshAsync(alice);

        Assert.Empty(handler.RunningRefreshes);
        Assert.True(first.RunningTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Handler_RoutesSubAccountTransactionEventsThroughParent()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-13T14:00:00Z"));
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectSubAccount subAccount = alice.AddSubAccount("operations", CreateKeySource("operations"));
        var handler = new EconomyAccountHandler();
        var parentEvents = new List<AccountTransactionsObservedEventArgs>();
        var handlerEvents = new List<AccountTransactionsObservedEventArgs>();
        alice.NewTransactionsObserved += (_, args) => parentEvents.Add(args);
        handler.NewTransactionsObserved += (_, args) => handlerEvents.Add(args);
        var options = new AccountRefreshOptions { MaxTransactionCount = 20 };

        await handler.RefreshAsync(subAccount, null, options, CancellationToken.None);

        rig.Time.Advance(TimeSpan.FromMinutes(3));
        LedgerSubmitResult payment = await alice.SendPaymentAsync(
            new PaymentRequest(subAccount.Address, Amount.FromInt64(10)),
            CancellationToken.None);
        Assert.True(payment.Accepted);
        TxId paymentTxId = payment.TxId ?? throw new InvalidOperationException("Payment must have a transaction id.");

        AccountRefreshResult subAccountRefresh = await handler.RefreshAsync(subAccount, null, options, CancellationToken.None);

        Assert.Equal(paymentTxId, Assert.Single(subAccountRefresh.Delta.NewTransactions).TxId);
        AccountTransactionsObservedEventArgs parentEvent = Assert.Single(parentEvents);
        Assert.True(parentEvent.IsSubAccountEvent);
        Assert.Same(alice, parentEvent.ParentAccount);
        Assert.Same(subAccount, parentEvent.SubAccount);
        Assert.Same(subAccount.Account, parentEvent.Account);
        Assert.Equal(paymentTxId, Assert.Single(parentEvent.NewTransactions).TxId);

        AccountTransactionsObservedEventArgs handlerEvent = Assert.Single(handlerEvents);
        Assert.True(handlerEvent.IsSubAccountEvent);
        Assert.Same(alice, handlerEvent.ParentAccount);
        Assert.Same(subAccount, handlerEvent.SubAccount);
        Assert.Equal(paymentTxId, Assert.Single(handlerEvent.NewTransactions).TxId);
    }

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
            new[] { new TransactionOutput(0, aliceKey.Address, Amount.FromInt64(100)) },
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

    private sealed class ReplacementAccountHandler : IEconomyAccountHandler
    {
        public event EventHandler<AccountRefreshCompletedEventArgs>? RefreshCompleted;

        public event EventHandler<AccountTransactionsObservedEventArgs>? NewTransactionsObserved;

        public IReadOnlyCollection<IAccountRefreshHandle> RunningRefreshes => [];

        public void Observe(BusinessObjectAccount account)
        {
        }

        public void Observe(BusinessObjectSubAccount subAccount)
        {
        }

        public Task<AccountRefreshResult> RefreshAsync(
            BusinessObjectAccount account,
            IBusinessObjectCastRegistry? castRegistry,
            AccountRefreshOptions? options,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AccountRefreshResult> RefreshAsync(
            BusinessObjectSubAccount subAccount,
            IBusinessObjectCastRegistry? castRegistry,
            AccountRefreshOptions? options,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IAccountRefreshHandle> StartAutoRefreshAsync(
            BusinessObjectAccount account,
            IBusinessObjectCastRegistry? castRegistry,
            AccountRefreshOptions? options,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IAccountRefreshHandle> StartAutoRefreshAsync(
            BusinessObjectSubAccount subAccount,
            IBusinessObjectCastRegistry? castRegistry,
            AccountRefreshOptions? options,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task StopAutoRefreshAsync(BusinessObjectAccount account, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAutoRefreshAsync(BusinessObjectSubAccount subAccount, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAllAutoRefreshesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void RaiseRefreshCompleted(AccountRefreshCompletedEventArgs args)
            => RefreshCompleted?.Invoke(this, args);

        public void RaiseNewTransactionsObserved(AccountTransactionsObservedEventArgs args)
            => NewTransactionsObserved?.Invoke(this, args);
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
