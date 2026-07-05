using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
        Services.AddEconomySsrAccountWorkspaceAdapters();
    }

    [Fact]
    public void AdapterHost_RendersSameWorkspaceSurfacesForSsrAndWasmAdapters()
    {
        foreach (string hostMode in new[] { "SSR", "WASM" })
        {
            IRenderedComponent<EconomyAccountWorkspaceAdapterHost> component = RenderComponent<EconomyAccountWorkspaceAdapterHost>(
                parameters => parameters.Add(x => x.Adapter, new SampleWorkspaceAdapter(hostMode)));

            Assert.Contains(hostMode, component.Markup, StringComparison.Ordinal);
            Assert.Contains("Transactions history", component.Markup, StringComparison.Ordinal);
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
    public async Task AdapterHost_FullShellSubmitsTicketMultimintBatchWithSingleFinalRefresh()
    {
        var adapter = new CapturingBatchWorkspaceAdapter();
        IRenderedComponent<EconomyAccountWorkspaceAdapterHost> component = RenderComponent<EconomyAccountWorkspaceAdapterHost>(
            parameters => parameters
                .Add(x => x.Adapter, adapter)
                .Add(x => x.UseFullAppShell, true)
                .Add(x => x.AutoRefreshEnabled, false)
                .Add(x => x.InitialView, "ticket"));

        component.WaitForAssertion(
            () => Assert.NotEmpty(component.FindAll("[data-testid='bo-create-wizard']")),
            TimeSpan.FromSeconds(2));

        await ClickButtonAsync(component, "Next");
        component.Find("[data-testid='cda-ticket-multimint-enabled']").Change(true);
        component.Find("[data-testid='cda-ticket-multimint-count']").Input("4");
        await ClickButtonAsync(component, "Next");
        await ClickButtonAsync(component, "Stage BO Transaction");
        await WaitForAsync(() => adapter.SubmittedDrafts.Count == 4, TimeSpan.FromSeconds(5));

        Assert.Equal(4, adapter.SubmittedDrafts.Count);
        Assert.Equal(2, adapter.WorkspaceLoadCount);
        Assert.All(adapter.SubmittedDrafts, draft => Assert.Equal(adapter.Workspace.Address, draft.OwnerAddress));
        Assert.Equal(4, adapter.SubmittedDrafts.Select(draft => draft.ObjectId).Distinct().Count());
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
            new AddressDirectoryService(new InMemoryAccountWorkspaceCache()),
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
    public async Task WasmWorkspaceAdapter_SubmitsExplicitUtxoSplitAndMergeWithoutNodeSession()
    {
        WasmAdapterRig rig = await WasmAdapterRig.CreateAsync();
        var adapter = new WasmAccountWorkspaceAdapter(
            rig.Gateway,
            rig.Gateway,
            rig.TransactionBuilder,
            rig.TransactionSigner,
            rig.CryptoDriver,
            new InMemoryAccountWorkspaceCache(),
            new AddressDirectoryService(new InMemoryAccountWorkspaceCache()),
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

        BusinessObjectWorkspaceModel initial = await adapter.GetWorkspaceAsync(CancellationToken.None);
        LedgerSpendableOutput source = Assert.Single(initial.SpendableOutputsOrEmpty, output => output.BusinessObject is null && output.Token is null);
        EconomyTransactionExecutionResult split = await adapter.SubmitTransactionAsync(
            initial.TransactionDraft with
            {
                SelectedMode = BusinessObjectTransactionMode.UtxoSplit,
                SelectedUtxos = [source.OutPoint],
                UtxoSplitOutputs =
                [
                    new UtxoSplitOutput(rig.UnlockedKey.Address, Amount.FromInt64(40)),
                    new UtxoSplitOutput(rig.UnlockedKey.Address, Amount.FromInt64(60))
                ]
            },
            CancellationToken.None);
        Assert.True(split.Accepted, split.Message);

        BusinessObjectWorkspaceModel afterSplit = await adapter.GetWorkspaceAsync(CancellationToken.None);
        LedgerSpendableOutput[] mergeInputs = afterSplit.SpendableOutputsOrEmpty
            .Where(output => output.BusinessObject is null && output.Token is null)
            .OrderBy(output => output.MainAmount.Value)
            .ToArray();
        Assert.Equal(2, mergeInputs.Length);

        EconomyTransactionExecutionResult merge = await adapter.SubmitTransactionAsync(
            afterSplit.TransactionDraft with
            {
                SelectedMode = BusinessObjectTransactionMode.UtxoMerge,
                PaymentReceiverAddress = Receiver,
                SelectedUtxos = mergeInputs.Select(output => output.OutPoint).ToArray()
            },
            CancellationToken.None);

        Assert.True(merge.Accepted, merge.Message);
        Assert.NotNull(merge.TxId);
        EconomyTransactionDetailsModel? details = await adapter.GetTransactionDetailsAsync(merge.TxId!.Value, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal(2, details!.Inputs.Count);
        Assert.Equal(
            new[] { "40", "60" },
            details.Inputs
                .OrderBy(input => input.MainAmount?.Value ?? -1m)
                .Select(input => input.MainAmount?.ToString() ?? "unknown")
                .ToArray());
        EconomyTransactionOutputDetails output = Assert.Single(details.Outputs);
        Assert.Equal(Receiver, output.Address);
        Assert.Equal("100", output.MainAmount.ToString());
    }

    [Fact]
    public async Task WasmWorkspaceAdapter_SendsAndOpensSecureMessagesBetweenAccounts()
    {
        SecureMessagingAdapterRig rig = await SecureMessagingAdapterRig.CreateAsync();
        byte[] attachmentBytes = Encoding.UTF8.GetBytes("private attachment bytes");
        WasmAccountWorkspaceAdapter aliceAdapter = await rig.CreateAdapterAsync(rig.Alice, "Alice browser");
        WasmAccountWorkspaceAdapter bobAdapter = await rig.CreateAdapterAsync(rig.Bob, "Bob browser");

        await PrimePublicKeyDiscoveryAsync(aliceAdapter, rig.Alice.Address);
        await PrimePublicKeyDiscoveryAsync(bobAdapter, rig.Bob.Address);

        EconomyTransactionExecutionResult aliceToBob = await aliceAdapter.SendSecureMessageAsync(
            new SecureMessageComposeRequest(
                "alice",
                rig.Bob.Address.Value,
                "Hello Bob",
                "message from Alice",
                []),
            CancellationToken.None);
        EconomyTransactionExecutionResult bobToAlice = await bobAdapter.SendSecureMessageAsync(
            new SecureMessageComposeRequest(
                "bob",
                rig.Alice.Address.Value,
                "Hello Alice",
                "message from Bob",
                [new SecureMessageAttachmentDraft("proof.txt", "text/plain", attachmentBytes)]),
            CancellationToken.None);

        Assert.True(aliceToBob.Accepted, aliceToBob.Message);
        Assert.True(bobToAlice.Accepted, bobToAlice.Message);
        Assert.DoesNotContain(attachmentBytes, rig.Storage.StoredPayloads, ByteArrayComparer.Instance);

        BusinessObjectWorkspaceModel bobWorkspace = await bobAdapter.GetWorkspaceAsync(CancellationToken.None);
        (LedgerTransactionSummary aliceTx, SecureLedgerMessagePayload alicePayload) = FindSecureMessage(
            bobWorkspace,
            rig.Alice.Address,
            rig.Bob.Address);
        SecureMessageOpenResult bobOpen = await bobAdapter.OpenSecureMessageAsync(
            new SecureMessageOpenRequest("bob", aliceTx.TxId, alicePayload),
            CancellationToken.None);

        Assert.True(bobOpen.Success, bobOpen.Summary);
        Assert.Equal("Hello Bob", bobOpen.Content?.Subject);
        Assert.Equal("message from Alice", bobOpen.Content?.Body);

        BusinessObjectWorkspaceModel aliceWorkspace = await aliceAdapter.GetWorkspaceAsync(CancellationToken.None);
        (LedgerTransactionSummary bobTx, SecureLedgerMessagePayload bobPayload) = FindSecureMessage(
            aliceWorkspace,
            rig.Bob.Address,
            rig.Alice.Address);
        AssertSecureMessagePublicSurfaceDoesNotLeak(
            bobTx,
            ["Hello Alice", "hello-alice", "message from Bob", "proof.txt", "text/plain", "application/octet-stream", "private attachment bytes", "ipfs://"],
            rig.Storage.StoredHashes);
        Assert.Empty(bobPayload.Attachments);

        SecureMessageOpenResult aliceOpen = await aliceAdapter.OpenSecureMessageAsync(
            new SecureMessageOpenRequest("alice", bobTx.TxId, bobPayload),
            CancellationToken.None);
        SecureMessageOpenResult bobSentOpen = await bobAdapter.OpenSecureMessageAsync(
            new SecureMessageOpenRequest("bob", bobTx.TxId, bobPayload),
            CancellationToken.None);

        Assert.True(aliceOpen.Success, aliceOpen.Summary);
        Assert.True(bobSentOpen.Success, bobSentOpen.Summary);
        Assert.Equal("Hello Alice", aliceOpen.Content?.Subject);
        Assert.Equal("message from Bob", aliceOpen.Content?.Body);
        SecureLedgerMessageContentAttachment descriptor = Assert.Single(aliceOpen.Content!.Attachments);
        Assert.NotNull(descriptor.EncryptedDataItem);
        Assert.NotNull(descriptor.Nonce);
        Assert.NotNull(descriptor.AuthenticationTag);
        Assert.NotNull(descriptor.CipherTextHash);

        SecureMessageAttachmentOpenResult attachment = await aliceAdapter.OpenSecureMessageAttachmentAsync(
            new SecureMessageAttachmentOpenRequest("alice", bobPayload, aliceOpen.Content, descriptor.AttachmentId),
            CancellationToken.None);

        Assert.True(attachment.Success, attachment.Summary);
        Assert.Equal("proof.txt", attachment.Attachment?.FileName);
        Assert.Equal(attachmentBytes, attachment.Attachment?.Data);
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
            new AddressDirectoryService(new InMemoryAccountWorkspaceCache()),
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
        EconomyTransactionInputDetails input = Assert.Single(first.Inputs);
        Assert.Equal("100", input.MainAmount?.ToString());
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

        public Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
            BusinessObjectEventLookupRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BusinessObjectEventLookupResult.NotFound("Sample adapter has no event lookup."));

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

    private sealed class CapturingBatchWorkspaceAdapter : IEconomyAccountWorkspaceAdapter
    {
        public BusinessObjectWorkspaceModel Workspace { get; } = EconomyComponentSampleData.CreateWorkspace();

        public List<BusinessObjectTransactionDraft> SubmittedDrafts { get; } = [];

        public int WorkspaceLoadCount { get; private set; }

        public string HostMode => "WASM";

        public bool UsesServerSideAccountSigning => false;

        public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        {
            WorkspaceLoadCount++;
            return Task.FromResult(Workspace);
        }

        public Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
            int maxTransactions = 10,
            CancellationToken cancellationToken = default)
            => Task.FromResult(AccountWorkspaceRefreshSnapshot.Create(
                Workspace,
                maxTransactions,
                DateTimeOffset.Parse("2026-06-29T12:00:00Z")));

        public Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(TxId txId, CancellationToken cancellationToken = default)
            => Task.FromResult<EconomyTransactionDetailsModel?>(EconomyComponentSampleData.CreateTransactionDetails() with { TxId = txId });

        public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
            BusinessObjectTransactionDraft draft,
            CancellationToken cancellationToken = default)
        {
            SubmittedDrafts.Add(draft);
            return Task.FromResult(new EconomyTransactionExecutionResult(
                draft.SelectedMode,
                Accepted: true,
                TxId: new TxId($"tx-ticket-batch-{SubmittedDrafts.Count}"),
                LedgerSequence: SubmittedDrafts.Count,
                "Accepted."));
        }

        public Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(BusinessObjectSendRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.TransferObject));

        public Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(BusinessObjectPriceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.UpdateObject));

        public Task<EconomyTransactionExecutionResult> UpdateProfileAsync(ProfileEditRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(request.CreateNew ? BusinessObjectTransactionMode.Create : BusinessObjectTransactionMode.UpdateObject));

        public Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
            BusinessObjectTicketVerificationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(VerifiedTicketResult(request.ObjectId));

        public Task<EconomyTransactionExecutionResult> UseTicketAsync(BusinessObjectTicketUseRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Accepted(BusinessObjectTransactionMode.UpdateObject));

        public Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
            BusinessObjectEventLookupRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BusinessObjectEventLookupResult.NotFound("Capturing adapter has no event lookup."));

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

        private static EconomyTransactionExecutionResult Accepted(BusinessObjectTransactionMode mode)
            => new(mode, true, new TxId($"tx-capturing-{mode}"), 1, "Accepted.");
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

        public Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
            BusinessObjectEventLookupRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BusinessObjectEventLookupResult.NotFound("Refresh probe adapter has no event lookup."));

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

    private static async Task ClickButtonAsync<TComponent>(IRenderedComponent<TComponent> component, string text)
        where TComponent : IComponent
    {
        IElement button = component.FindAll("button")
            .Single(item => item.TextContent.Contains(text, StringComparison.Ordinal));
        await button.ClickAsync(new MouseEventArgs());
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

    private static async Task PrimePublicKeyDiscoveryAsync(WasmAccountWorkspaceAdapter adapter, Address address)
    {
        BusinessObjectWorkspaceModel workspace = await adapter.GetWorkspaceAsync(CancellationToken.None);
        EconomyTransactionExecutionResult result = await adapter.SubmitTransactionAsync(
            workspace.TransactionDraft with
            {
                SelectedMode = BusinessObjectTransactionMode.MainPayment,
                PaymentReceiverAddress = address,
                PaymentAmount = Amount.FromInt64(1)
            },
            CancellationToken.None);

        Assert.True(result.Accepted, result.Message);
    }

    private static (LedgerTransactionSummary Transaction, SecureLedgerMessagePayload Payload) FindSecureMessage(
        BusinessObjectWorkspaceModel workspace,
        Address sender,
        Address receiver)
    {
        foreach (LedgerTransactionSummary transaction in workspace.Transactions)
        {
            if (SecureLedgerMessages.TryParseTransactionData(transaction.Data, out SecureLedgerMessagePayload payload)
                && payload.SenderAddress == sender
                && payload.ReceiverAddress == receiver)
            {
                return (transaction, payload);
            }
        }

        throw new InvalidOperationException($"Secure message {sender.Value} -> {receiver.Value} was not found.");
    }

    private static void AssertSecureMessagePublicSurfaceDoesNotLeak(
        LedgerTransactionSummary transaction,
        IReadOnlyList<string> forbiddenValues,
        IReadOnlyCollection<string> storedHashes)
    {
        string publicJson = JsonSerializer.Serialize(new
        {
            transaction.Data,
            transaction.Outputs
        });
        foreach (string forbidden in forbiddenValues.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            Assert.DoesNotContain(forbidden, publicJson, StringComparison.OrdinalIgnoreCase);
        }

        foreach (string hash in storedHashes)
        {
            Assert.DoesNotContain(hash, publicJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class SecureMessagingAdapterRig
    {
        private SecureMessagingAdapterRig(
            InProcessLedgerGateway gateway,
            BusinessObjectTransactionBuilder transactionBuilder,
            IAccountTransactionSigner transactionSigner,
            IEconomyCryptoDriver cryptoDriver,
            IBusinessObjectCastRegistry castRegistry,
            ManualTimeProvider timeProvider,
            MemoryBusinessObjectDataStorageDriver storage,
            AccountVaultUnlockedKey alice,
            AccountVaultUnlockedKey bob)
        {
            Gateway = gateway;
            TransactionBuilder = transactionBuilder;
            TransactionSigner = transactionSigner;
            CryptoDriver = cryptoDriver;
            CastRegistry = castRegistry;
            TimeProvider = timeProvider;
            Storage = storage;
            Alice = alice;
            Bob = bob;
        }

        public InProcessLedgerGateway Gateway { get; }

        public BusinessObjectTransactionBuilder TransactionBuilder { get; }

        public IAccountTransactionSigner TransactionSigner { get; }

        public IEconomyCryptoDriver CryptoDriver { get; }

        public IBusinessObjectCastRegistry CastRegistry { get; }

        public ManualTimeProvider TimeProvider { get; }

        public MemoryBusinessObjectDataStorageDriver Storage { get; }

        public AccountVaultUnlockedKey Alice { get; }

        public AccountVaultUnlockedKey Bob { get; }

        public async Task<WasmAccountWorkspaceAdapter> CreateAdapterAsync(
            AccountVaultUnlockedKey key,
            string accountName)
        {
            var cache = new InMemoryAccountWorkspaceCache();
            var adapter = new WasmAccountWorkspaceAdapter(
                Gateway,
                Gateway,
                TransactionBuilder,
                TransactionSigner,
                CryptoDriver,
                cache,
                new AddressDirectoryService(cache),
                CastRegistry,
                TimeProvider,
                new WasmAccountWorkspaceOptions
                {
                    LedgerId = Context.LedgerId.Value,
                    ScenarioId = Context.ScenarioId?.Value,
                    AccountName = accountName,
                    ConnectionLabel = "Browser API"
                },
                new BusinessObjectDataItemUploadService(Storage),
                Storage);
            await adapter.UseUnlockedAccountAsync(key, CancellationToken.None);
            return adapter;
        }

        public static async Task<SecureMessagingAdapterRig> CreateAsync()
        {
            ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var payloadBuilder = new SignaturePayloadBuilder(hasher);
            var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-02T12:00:00Z"));
            var store = new InMemoryLedgerStore(hasher);
            var gateway = new InProcessLedgerGateway(
                new LedgerExecutor(store, hasher, payloadBuilder, new TestAdminSigner(), timeProvider, SignatureProviderRegistry.CreateDefault()),
                hasher,
                timeProvider);
            var cryptoDriver = new DotNetEconomyCryptoDriver();
            var vaultService = new AccountVaultService(cryptoDriver, new InMemoryAccountVaultStore());
            AccountVaultCreationResult alice = await vaultService.CreateAsync(
                new AccountVaultCreateRequest("alice-vault", "passphrase", "Alice account", "alice-key"),
                CancellationToken.None);
            AccountVaultCreationResult bob = await vaultService.CreateAsync(
                new AccountVaultCreateRequest("bob-vault", "passphrase", "Bob account", "bob-key"),
                CancellationToken.None);

            LedgerSubmitResult genesis = await gateway.SubmitTransactionAsync(
                new LedgerTransaction(
                    Context.LedgerId,
                    Context.ScenarioId,
                    Version: 1,
                    TransactionKind.Genesis,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    [],
                    [
                        new TransactionOutput(0, alice.UnlockedKey.Address, Amount.FromInt64(100)),
                        new TransactionOutput(1, bob.UnlockedKey.Address, Amount.FromInt64(100))
                    ],
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

            return new SecureMessagingAdapterRig(
                gateway,
                transactionBuilder,
                transactionSigner,
                cryptoDriver,
                castRegistry,
                timeProvider,
                new MemoryBusinessObjectDataStorageDriver(),
                alice.UnlockedKey,
                bob.UnlockedKey);
        }
    }

    private sealed class MemoryBusinessObjectDataStorageDriver : IBusinessObjectDataStorageDriver
    {
        private readonly Dictionary<string, byte[]> payloads = new(StringComparer.Ordinal);

        public BusinessObjectDataStorageKind Storage => BusinessObjectDataStorageKind.Ipfs;

        public IReadOnlyCollection<byte[]> StoredPayloads => payloads.Values;

        public IReadOnlyCollection<string> StoredHashes => payloads.Keys;

        public async Task<BusinessObjectStorageWriteResult> SaveAsync(
            BusinessObjectStorageWriteRequest request,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await request.Data.CopyToAsync(memory, cancellationToken);
            byte[] bytes = memory.ToArray();
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            payloads[hash] = bytes;
            return new BusinessObjectStorageWriteResult(
                BusinessObjectDataStorageKind.Ipfs,
                hash,
                new Uri($"ipfs://{hash}"),
                request.FileName,
                request.ContentType,
                bytes.LongLength);
        }

        public Task<BusinessObjectStorageReadResult> ReadAsync(
            string hashOrUri,
            CancellationToken cancellationToken)
        {
            string hash = BusinessObjectStorageUri.GetHash(hashOrUri);
            byte[] bytes = payloads[hash];
            return Task.FromResult(new BusinessObjectStorageReadResult(
                new MemoryStream(bytes, writable: false),
                hash,
                "application/octet-stream"));
        }
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
            => x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(byte[] obj)
            => obj.Aggregate(17, (current, value) => (current * 31) + value);
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
