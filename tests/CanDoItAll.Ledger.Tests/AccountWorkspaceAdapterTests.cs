using System.Text;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Sdk;
using CanDoItAll.Economy.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class AccountWorkspaceAdapterTests : TestContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("account-app-ledger"), new ScenarioId("browser-demo"));
    private static readonly Address Receiver = Address.FromPublicKey(Encoding.UTF8.GetBytes("adapter-receiver"));

    public AccountWorkspaceAdapterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void AdapterHost_RendersSameWorkspaceSurfacesForSsrAndWasmAdapters()
    {
        foreach (string hostMode in new[] { "SSR", "WASM" })
        {
            IRenderedComponent<EconomyAccountWorkspaceAdapterHost> component = RenderComponent<EconomyAccountWorkspaceAdapterHost>(
                parameters => parameters.Add(x => x.Adapter, new SampleWorkspaceAdapter(hostMode)));

            Assert.Contains(hostMode, component.Markup, StringComparison.Ordinal);
            Assert.Contains("Recent Transactions", component.Markup, StringComparison.Ordinal);
            Assert.Contains("UTXOs", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Transaction composer", component.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(component.FindAll("[data-testid='adapter-workspace-host']"));
            Assert.NotEmpty(component.FindAll("[data-testid='adapter-utxo-explorer']"));
            Assert.NotEmpty(component.FindAll("[data-testid='adapter-transaction-history']"));
            Assert.NotEmpty(component.FindAll("[data-testid='adapter-transaction-composer']"));
        }
    }

    [Fact]
    public async Task WasmWorkspaceAdapter_LoadsWorkspaceAndBroadcastsSignedPaymentWithoutNodeSession()
    {
        WasmAdapterRig rig = await WasmAdapterRig.CreateAsync();
        var adapter = new WasmAccountWorkspaceAdapter(
            rig.Gateway,
            rig.Gateway,
            rig.TransactionBuilder,
            rig.TransactionSigner,
            rig.CastRegistry,
            rig.TimeProvider,
            new WasmAccountWorkspaceOptions
            {
                LedgerId = Context.LedgerId.Value,
                ScenarioId = Context.ScenarioId?.Value,
                AccountName = "Browser account",
                ConnectionLabel = "Browser API"
            });
        await adapter.UseUnlockedAccountAsync(rig.UnlockedKey, CancellationToken.None);

        BusinessObjectWorkspaceModel workspace = await adapter.GetWorkspaceAsync(CancellationToken.None);
        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(
            workspace.TransactionDraft with
            {
                SelectedMode = BusinessObjectTransactionMode.MainPayment,
                PaymentReceiverAddress = Receiver,
                PaymentAmount = Amount.FromInt64(5)
            },
            CancellationToken.None);

        Assert.Equal("WASM", adapter.HostMode);
        Assert.False(adapter.UsesServerSideAccountSigning);
        Assert.Equal("Browser account", workspace.AccountName);
        Assert.True(result.Accepted, result.Message);
        Assert.NotNull(result.TxId);
        LedgerAccountBalance balance = await rig.Gateway.GetBalanceAsync(new LedgerAddressQuery(Context, rig.UnlockedKey.Address), CancellationToken.None);
        Assert.Equal("95", balance.MainBalance.ToString());
    }

    [Fact]
    public void WasmWorkspaceRegistration_DoesNotRegisterSsrNodeSession()
    {
        var services = new ServiceCollection();
        var ledgerClient = new ThrowingLedgerClient();

        services.AddSingleton<ILedgerGateway>(ledgerClient);
        services.AddSingleton<ILedgerReadClient>(ledgerClient);
        services.AddEconomyWasmAccountWorkspaceAdapter();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<EconomyNodeWorkspaceSession>());
        Assert.IsType<WasmAccountWorkspaceAdapter>(provider.GetRequiredService<IEconomyAccountWorkspaceAdapter>());
    }

    private sealed class SampleWorkspaceAdapter(string hostMode) : IEconomyAccountWorkspaceAdapter
    {
        public string HostMode => hostMode;

        public bool UsesServerSideAccountSigning => hostMode == "SSR";

        public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        {
            BusinessObjectWorkspaceModel sample = EconomyComponentSampleData.CreateWorkspace() with
            {
                AccountName = $"{hostMode} account workspace",
                ConnectionLabel = $"{hostMode} adapter"
            };
            return Task.FromResult(sample);
        }

        public Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(TxId txId, CancellationToken cancellationToken = default)
            => Task.FromResult<EconomyTransactionDetailsModel?>(EconomyComponentSampleData.CreateTransactionDetails() with { TxId = txId });

        public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
            BusinessObjectTransactionDraft draft,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new EconomyTransactionExecutionResult(
                draft.SelectedMode,
                Accepted: true,
                TxId: new TxId($"tx-{hostMode.ToLowerInvariant()}"),
                LedgerSequence: 1,
                $"{hostMode} adapter accepted transaction."));
    }

    private sealed class ThrowingLedgerClient : ILedgerGateway, ILedgerReadClient
    {
        public Task<LedgerSubmitResult> SubmitTransactionAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The DI registration test must not submit transactions.");

        public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(
            LedgerAddressQuery query,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The DI registration test must not read ledger spendable outputs.");

        public Task<LedgerAccountBalance> GetBalanceAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The DI registration test must not read ledger balances.");

        public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(
            LedgerAddressQuery query,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The DI registration test must not read business objects.");

        public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
            LedgerAddressQuery query,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The DI registration test must not read ledger transactions.");
    }

    private sealed class WasmAdapterRig
    {
        private WasmAdapterRig(
            InProcessLedgerGateway gateway,
            BusinessObjectTransactionBuilder transactionBuilder,
            IAccountTransactionSigner transactionSigner,
            IBusinessObjectCastRegistry castRegistry,
            ManualTimeProvider timeProvider,
            AccountVaultUnlockedKey unlockedKey)
        {
            Gateway = gateway;
            TransactionBuilder = transactionBuilder;
            TransactionSigner = transactionSigner;
            CastRegistry = castRegistry;
            TimeProvider = timeProvider;
            UnlockedKey = unlockedKey;
        }

        public InProcessLedgerGateway Gateway { get; }

        public BusinessObjectTransactionBuilder TransactionBuilder { get; }

        public IAccountTransactionSigner TransactionSigner { get; }

        public IBusinessObjectCastRegistry CastRegistry { get; }

        public ManualTimeProvider TimeProvider { get; }

        public AccountVaultUnlockedKey UnlockedKey { get; }

        public static async Task<WasmAdapterRig> CreateAsync()
        {
            ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var payloadBuilder = new SignaturePayloadBuilder(hasher);
            var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-06-29T12:00:00Z"));
            var store = new InMemoryLedgerStore(hasher);
            var gateway = new InProcessLedgerGateway(
                new LedgerExecutor(store, hasher, payloadBuilder, new TestAdminSigner(), timeProvider, SignatureProviderRegistry.CreateDefault()),
                hasher,
                timeProvider);
            var cryptoDriver = new DotNetEconomyCryptoDriver();
            var vaultStore = new InMemoryAccountVaultStore();
            var vaultService = new AccountVaultService(cryptoDriver, vaultStore);
            AccountVaultCreationResult created = await vaultService.CreateAsync(
                new AccountVaultCreateRequest("browser-vault", "passphrase", "Browser account", "browser-key"),
                CancellationToken.None);

            LedgerSubmitResult genesis = await gateway.SubmitTransactionAsync(
                new LedgerTransaction(
                    Context.LedgerId,
                    Context.ScenarioId,
                    Version: 1,
                    TransactionKind.Genesis,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    Array.Empty<TransactionInput>(),
                    new[] { new TransactionOutput(0, created.UnlockedKey.Address, Amount.FromInt64(100)) },
                    Data: null),
                CancellationToken.None);
            Assert.True(genesis.Accepted, string.Join(Environment.NewLine, genesis.Errors.Select(error => $"{error.Code}: {error.Message}")));

            IAccountInputSigner inputSigner = new CryptoDriverAccountInputSigner(cryptoDriver);
            var transactionSigner = new AccountTransactionSigner(hasher, payloadBuilder, inputSigner);
            var transactionBuilder = new BusinessObjectTransactionBuilder(gateway, hasher, payloadBuilder, inputSigner, timeProvider);
            IBusinessObjectCastRegistry castRegistry = new BusinessObjectCastRegistry(new IBusinessObjectCaster[]
            {
                new AccountSettingsBusinessObjectCaster(timeProvider),
                new ImageBusinessObjectCaster(timeProvider),
                new PostBusinessObjectCaster(timeProvider),
                new GenericBusinessObjectCaster(timeProvider)
            });

            return new WasmAdapterRig(gateway, transactionBuilder, transactionSigner, castRegistry, timeProvider, created.UnlockedKey);
        }
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "test-admin",
                    Hex.Sha256Hex(Encoding.UTF8.GetBytes("admin-public")),
                    payloadHash,
                    Encoding.UTF8.GetBytes("admin-signature"))
            });
    }
}
