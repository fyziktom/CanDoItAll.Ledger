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
    public void AdapterHost_FullShellStartsOnInitialGalleryView()
    {
        IRenderedComponent<EconomyAccountWorkspaceAdapterHost> component = RenderComponent<EconomyAccountWorkspaceAdapterHost>(
            parameters => parameters
                .Add(x => x.Adapter, new SampleWorkspaceAdapter("WASM"))
                .Add(x => x.UseFullAppShell, true)
                .Add(x => x.AutoRefreshEnabled, false)
                .Add(x => x.InitialView, "gallery"));

        component.WaitForAssertion(
            () => Assert.NotEmpty(component.FindAll("[data-testid='ssr-bo-gallery']")),
            TimeSpan.FromSeconds(2));
        Assert.DoesNotContain("New Payment", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("New Purchase", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("New Receipt", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("New Profile", component.Markup, StringComparison.Ordinal);
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
            rig.CryptoDriver,
            new InMemoryAccountWorkspaceCache(),
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
    public async Task AdapterHost_AutoRefreshSkipsWorkspaceReloadWhenSnapshotIsUnchanged()
    {
        var adapter = new RefreshProbeWorkspaceAdapter(
            "WASM",
            initialTransactionId: "tx-refresh-stable",
            probeTransactionId: "tx-refresh-stable");

        IRenderedComponent<EconomyAccountWorkspaceAdapterHost> component = RenderComponent<EconomyAccountWorkspaceAdapterHost>(
            parameters => parameters
                .Add(x => x.Adapter, adapter)
                .Add(x => x.AutoRefreshInterval, TimeSpan.FromMilliseconds(20))
                .Add(x => x.AutoRefreshTransactionProbeCount, 10));

        await WaitForAsync(() => adapter.RefreshProbeCount > 0, TimeSpan.FromSeconds(10));
        await component.Instance.DisposeAsync();

        Assert.Equal(1, adapter.WorkspaceLoadCount);
    }

    [Fact]
    public async Task AdapterHost_AutoRefreshReloadsWorkspaceWhenSnapshotChanges()
    {
        var adapter = new RefreshProbeWorkspaceAdapter(
            "WASM",
            initialTransactionId: "tx-refresh-before",
            probeTransactionId: "tx-refresh-after");

        IRenderedComponent<EconomyAccountWorkspaceAdapterHost> component = RenderComponent<EconomyAccountWorkspaceAdapterHost>(
            parameters => parameters
                .Add(x => x.Adapter, adapter)
                .Add(x => x.AutoRefreshInterval, TimeSpan.FromMilliseconds(20))
                .Add(x => x.AutoRefreshTransactionProbeCount, 10));

        await WaitForAsync(() => adapter.WorkspaceLoadCount >= 2, TimeSpan.FromSeconds(10));
        await component.Instance.DisposeAsync();

        Assert.True(adapter.RefreshProbeCount > 0);
    }

    [Fact]
    public async Task WasmWorkspaceAdapter_CachesTransactionDetailsByContextAndTxId()
    {
        WasmAdapterRig rig = await WasmAdapterRig.CreateAsync();
        var readClient = new CountingLedgerReadClient(rig.Gateway);
        var adapter = new WasmAccountWorkspaceAdapter(
            rig.Gateway,
            readClient,
            rig.TransactionBuilder,
            rig.TransactionSigner,
            rig.CryptoDriver,
            new InMemoryAccountWorkspaceCache(),
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
        Assert.NotNull(result.TxId);
        TxId txId = result.TxId ?? throw new InvalidOperationException("Accepted transaction did not return a tx id.");

        EconomyTransactionDetailsModel? first = await adapter.GetTransactionDetailsAsync(txId, CancellationToken.None);
        EconomyTransactionDetailsModel? second = await adapter.GetTransactionDetailsAsync(txId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.TxId, second.TxId);
        Assert.Equal(1, readClient.TransactionDetailReadCount);
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

        public Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
            int maxTransactions = 10,
            CancellationToken cancellationToken = default)
        {
            BusinessObjectWorkspaceModel sample = EconomyComponentSampleData.CreateWorkspace();
            return Task.FromResult(AccountWorkspaceRefreshSnapshot.Create(
                sample,
                maxTransactions,
                DateTimeOffset.Parse("2026-06-29T12:00:00Z")));
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

        public Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(
            BusinessObjectSendRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.TransferObject));

        public Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(
            BusinessObjectPriceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.UpdateObject));

        public Task<EconomyTransactionExecutionResult> UpdateProfileAsync(
            ProfileEditRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(request.CreateNew
                ? BusinessObjectTransactionMode.Create
                : BusinessObjectTransactionMode.UpdateObject));

        public Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
            BusinessObjectTicketVerificationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(VerifiedTicketResult(request.ObjectId));

        public Task<EconomyTransactionExecutionResult> UseTicketAsync(
            BusinessObjectTicketUseRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.UpdateObject));

        public Task<EconomyTransactionExecutionResult> CreateTokenAsync(
            EconomyTokenCreationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.TokenTransfer));

        public Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(
            EconomyRuleTokenCreationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.CreateRuleToken));

        public Task<MessageSecurityActionResult> SignMessageAsync(
            MessageSecuritySignRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail($"{hostMode} sample adapter does not sign messages."));

        public Task<MessageSecurityActionResult> VerifyMessageAsync(
            MessageSecurityVerifyRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail($"{hostMode} sample adapter does not verify messages."));

        public Task<MessageSecurityActionResult> EncryptMessageAsync(
            MessageSecurityEncryptRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail($"{hostMode} sample adapter does not encrypt messages."));

        public Task<MessageSecurityActionResult> DecryptMessageAsync(
            MessageSecurityDecryptRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail($"{hostMode} sample adapter does not decrypt messages."));

        private EconomyTransactionExecutionResult Accepted(BusinessObjectTransactionMode mode)
            => new(
                mode,
                Accepted: true,
                TxId: new TxId($"tx-{hostMode.ToLowerInvariant()}"),
                LedgerSequence: 1,
                $"{hostMode} adapter accepted transaction.");
    }

    private sealed class RefreshProbeWorkspaceAdapter(
        string hostMode,
        string initialTransactionId,
        string probeTransactionId) : IEconomyAccountWorkspaceAdapter
    {
        private readonly DateTimeOffset timestamp = DateTimeOffset.Parse("2026-06-29T12:00:00Z");

        public int WorkspaceLoadCount { get; private set; }

        public int RefreshProbeCount { get; private set; }

        public string HostMode => hostMode;

        public bool UsesServerSideAccountSigning => false;

        public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        {
            WorkspaceLoadCount++;
            string txId = WorkspaceLoadCount == 1 ? initialTransactionId : probeTransactionId;
            BusinessObjectWorkspaceModel sample = EconomyComponentSampleData.CreateWorkspace() with
            {
                AccountName = $"{hostMode} refresh workspace",
                Transactions = new[] { CreateTransaction(txId, timestamp) }
            };
            return Task.FromResult(sample);
        }

        public Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
            int maxTransactions = 10,
            CancellationToken cancellationToken = default)
        {
            RefreshProbeCount++;
            BusinessObjectWorkspaceModel sample = EconomyComponentSampleData.CreateWorkspace() with
            {
                Transactions = new[] { CreateTransaction(probeTransactionId, timestamp) }
            };
            return Task.FromResult(AccountWorkspaceRefreshSnapshot.Create(sample, maxTransactions, timestamp));
        }

        public Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(TxId txId, CancellationToken cancellationToken = default)
            => Task.FromResult<EconomyTransactionDetailsModel?>(EconomyComponentSampleData.CreateTransactionDetails() with { TxId = txId });

        public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
            BusinessObjectTransactionDraft draft,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(draft.SelectedMode));

        public Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(BusinessObjectSendRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.TransferObject));

        public Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(BusinessObjectPriceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.UpdateObject));

        public Task<EconomyTransactionExecutionResult> UpdateProfileAsync(ProfileEditRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(request.CreateNew
                ? BusinessObjectTransactionMode.Create
                : BusinessObjectTransactionMode.UpdateObject));

        public Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
            BusinessObjectTicketVerificationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(VerifiedTicketResult(request.ObjectId));

        public Task<EconomyTransactionExecutionResult> UseTicketAsync(
            BusinessObjectTicketUseRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.UpdateObject));

        public Task<EconomyTransactionExecutionResult> CreateTokenAsync(EconomyTokenCreationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.TokenTransfer));

        public Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(EconomyRuleTokenCreationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.CreateRuleToken));

        public Task<MessageSecurityActionResult> SignMessageAsync(MessageSecuritySignRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));

        public Task<MessageSecurityActionResult> VerifyMessageAsync(MessageSecurityVerifyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));

        public Task<MessageSecurityActionResult> EncryptMessageAsync(MessageSecurityEncryptRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));

        public Task<MessageSecurityActionResult> DecryptMessageAsync(MessageSecurityDecryptRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(MessageSecurityActionResult.Fail("Not needed."));

        private EconomyTransactionExecutionResult Accepted(BusinessObjectTransactionMode mode)
            => new(mode, true, new TxId($"tx-refresh-{mode}"), 1, "Accepted.");
    }

    private sealed class CountingLedgerReadClient(ILedgerReadClient inner) : ILedgerReadClient
    {
        public int TransactionDetailReadCount { get; private set; }

        public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(
            LedgerAddressQuery query,
            CancellationToken cancellationToken)
            => inner.GetSpendableOutputsAsync(query, cancellationToken);

        public Task<LedgerAccountBalance> GetBalanceAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => inner.GetBalanceAsync(query, cancellationToken);

        public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(
            LedgerAddressQuery query,
            CancellationToken cancellationToken)
            => inner.GetBusinessObjectsAsync(query, cancellationToken);

        public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
            LedgerAddressQuery query,
            CancellationToken cancellationToken)
            => inner.GetTransactionsAsync(query, cancellationToken);

        public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
            LedgerAddressQuery query,
            bool includeData,
            CancellationToken cancellationToken)
            => inner.GetTransactionsAsync(query, includeData, cancellationToken);

        public Task<LedgerTransactionSummary?> GetTransactionAsync(
            LedgerAccountContext context,
            TxId txId,
            bool includeData,
            CancellationToken cancellationToken)
        {
            TransactionDetailReadCount++;
            return inner.GetTransactionAsync(context, txId, includeData, cancellationToken);
        }

        public Task<IReadOnlyList<LedgerTransactionSummary>> QueryTransactionsAsync(
            LedgerTransactionQuery query,
            CancellationToken cancellationToken)
            => inner.QueryTransactionsAsync(query, cancellationToken);
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

    private static LedgerTransactionSummary CreateTransaction(string txId, DateTimeOffset timestamp)
        => new(
            Context,
            new TxId(txId),
            TransactionKind.Transfer,
            TransactionStatus.Confirmed,
            timestamp,
            new[] { Receiver },
            new[] { Receiver },
            Array.Empty<LedgerTransactionInputSummary>(),
            Array.Empty<LedgerTransactionOutputSummary>());

    private static BusinessObjectTicketVerificationResult VerifiedTicketResult(BusinessObjectId objectId)
    {
        var history = new BusinessObjectTicketHistoryValidation(
            BusinessObjectTicketHistoryStatus.Valid,
            new TxId($"origin-{objectId.Value}"),
            null,
            Array.Empty<BusinessObjectTicketHistoryEntry>());
        return new BusinessObjectTicketVerificationResult(
            Success: true,
            Summary: "Ticket history is valid.",
            Message: $"CanDoItAll ticket ownership verification\nobject={objectId.Value}",
            MessageHash: new HashHex(new string('0', 64)),
            SignatureJson: "{}",
            GeneratedAtUtc: DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-06-29T12:05:00Z"),
            history);
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition(), "Condition did not become true before the timeout.");
    }

    private sealed class WasmAdapterRig
    {
        private WasmAdapterRig(
            InProcessLedgerGateway gateway,
            BusinessObjectTransactionBuilder transactionBuilder,
            IAccountTransactionSigner transactionSigner,
            IEconomyCryptoDriver cryptoDriver,
            IBusinessObjectCastRegistry castRegistry,
            ManualTimeProvider timeProvider,
            AccountVaultUnlockedKey unlockedKey)
        {
            Gateway = gateway;
            TransactionBuilder = transactionBuilder;
            TransactionSigner = transactionSigner;
            CryptoDriver = cryptoDriver;
            CastRegistry = castRegistry;
            TimeProvider = timeProvider;
            UnlockedKey = unlockedKey;
        }

        public InProcessLedgerGateway Gateway { get; }

        public BusinessObjectTransactionBuilder TransactionBuilder { get; }

        public IAccountTransactionSigner TransactionSigner { get; }

        public IEconomyCryptoDriver CryptoDriver { get; }

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

            return new WasmAdapterRig(gateway, transactionBuilder, transactionSigner, cryptoDriver, castRegistry, timeProvider, created.UnlockedKey);
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
