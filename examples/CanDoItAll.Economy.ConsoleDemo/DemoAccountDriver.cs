using System.Net.Http.Json;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Sdk;

namespace CanDoItAll.Economy.ConsoleDemo;

internal sealed class DemoAccountDriver : IDisposable
{
    private readonly EconomyNodeLedgerClient ledgerDriver;

    private DemoAccountDriver(
        string name,
        string keyPath,
        LedgerAccountContext context,
        EconomyNodeLedgerClient ledgerDriver,
        EconomyClient client,
        BusinessObjectAccount account)
    {
        Name = name;
        KeyPath = keyPath;
        Context = context;
        this.ledgerDriver = ledgerDriver;
        Client = client;
        Account = account;
    }

    public string Name { get; }

    public string KeyPath { get; }

    public LedgerAccountContext Context { get; }

    public EconomyClient Client { get; }

    public BusinessObjectAccount Account { get; }

    public EconomyNodeLedgerClient NodeDriver => ledgerDriver;

    public Address Address => Account.Address;

    public static async Task<DemoAccountDriver> CreateAsync(
        string name,
        DemoOptions options,
        DemoKeyStore keyStore,
        CancellationToken cancellationToken)
    {
        StoredDemoKey storedKey = await keyStore.LoadOrCreateAsync(name, cancellationToken).ConfigureAwait(false);
        var ledgerDriver = new EconomyNodeLedgerClient(options.NodeBaseUri);
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var signer = new AccountInputSigner(SignatureProviderRegistry.CreateDefault());
        var builder = new BusinessObjectTransactionBuilder(
            ledgerDriver,
            hasher,
            new SignaturePayloadBuilder(hasher),
            signer,
            new SystemClock());
        var client = new EconomyClient(ledgerDriver, ledgerDriver, builder);
        BusinessObjectAccount account = await client
            .LoadAccountAsync(options.Context, storedKey.Envelope, cancellationToken)
            .ConfigureAwait(false);

        ConsoleUi.Success(storedKey.Created
            ? $"Created {name} key at {storedKey.Path}."
            : $"Loaded {name} key from {storedKey.Path}.");

        return new DemoAccountDriver(name, storedKey.Path, options.Context, ledgerDriver, client, account);
    }

    public async Task<LedgerSubmitResult> MintMainAsync(Amount amount, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { BaseAddress = ledgerDriver.NodeBaseUri };
        using HttpResponseMessage response = await httpClient
            .PostAsJsonAsync(
                "api/v1/transactions/mint",
                new NodeMintTransactionRequest(
                    Address.Value,
                    amount.Value,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    Context.ScenarioId?.Value),
                cancellationToken)
            .ConfigureAwait(false);
        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Node treasury mint failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {content}");
        }

        NodeTransactionReceipt receipt = System.Text.Json.JsonSerializer.Deserialize<NodeTransactionReceipt>(
            content,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Node treasury mint returned an empty receipt.");

        return LedgerSubmitResult.Confirmed(new TxId(receipt.TxId), receipt.LedgerSequence);
    }

    public Task<LedgerSubmitResult> CreateBusinessObjectAsync(
        BusinessObjectId objectId,
        string objectType,
        string stateJson,
        Amount mainAmount,
        CancellationToken cancellationToken,
        TokenTransfer? token = null,
        OutPoint? sourceOutPoint = null)
        => Account.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                objectId,
                objectType,
                stateJson,
                MainAmount: mainAmount,
                Token: token,
                SourceOutPoint: sourceOutPoint),
            cancellationToken);

    public Task<LedgerSubmitResult> MintTokenAsync(
        TokenId tokenId,
        Amount amount,
        CancellationToken cancellationToken,
        OutPoint? permissionSourceOutPoint = null)
        => Account.MintTokenAsync(new TokenMintRequest(
            tokenId,
            amount,
            PermissionSourceOutPoint: permissionSourceOutPoint),
            cancellationToken);

    public Task<LedgerSubmitResult> SendTokenTransferAsync(
        DemoAccountDriver receiver,
        TokenId tokenId,
        Amount amount,
        CancellationToken cancellationToken,
        Amount? mainAmount = null)
        => Account.SendTokenTransferAsync(
            new TokenTransferRequest(tokenId, amount, receiver.Address, mainAmount),
            cancellationToken);

    public Task<LedgerSubmitResult> IssuePermissionAsync(
        DemoAccountDriver receiver,
        string tokenNamespace,
        Amount amount,
        CancellationToken cancellationToken,
        bool repeatable = true,
        string action = PermissionTokenAuthority.MintNewTokenTypeAction,
        OutPoint? sourceOutPoint = null)
        => Account.IssuePermissionAsync(
            new PermissionIssueRequest(receiver.Address, tokenNamespace, amount, repeatable, action, sourceOutPoint),
            cancellationToken);

    public Task<LedgerSubmitResult> SendPaymentAsync(
        DemoAccountDriver receiver,
        Amount amount,
        CancellationToken cancellationToken)
        => Account.SendPaymentAsync(new PaymentRequest(receiver.Address, amount), cancellationToken);

    public Task<LedgerSubmitResult> SplitUtxosAsync(
        IReadOnlyList<OutPoint> sourceOutPoints,
        IReadOnlyList<UtxoSplitOutput> outputs,
        CancellationToken cancellationToken)
        => Account.SplitUtxosAsync(
            new UtxoSplitRequest(sourceOutPoints, outputs),
            cancellationToken);

    public Task<LedgerSubmitResult> MergeUtxosAsync(
        IReadOnlyList<OutPoint> sourceOutPoints,
        CancellationToken cancellationToken,
        Address? receiverAddress = null)
        => Account.MergeUtxosAsync(
            new UtxoMergeRequest(sourceOutPoints, receiverAddress),
            cancellationToken);

    public Task<LedgerTransaction> BuildBusinessObjectTransferAsync(
        BusinessObjectId objectId,
        DemoAccountDriver receiver,
        CancellationToken cancellationToken)
        => Account.BuildTransferBusinessObjectTransactionAsync(
            new TransferBusinessObjectRequest(objectId, receiver.Address),
            cancellationToken);

    public Task<LedgerSubmitResult> TransferBusinessObjectAsync(
        BusinessObjectId objectId,
        DemoAccountDriver receiver,
        CancellationToken cancellationToken,
        Amount? mainAmount = null,
        string? stateJson = null,
        string? externalRef = null)
        => Account.TransferBusinessObjectAsync(
            new TransferBusinessObjectRequest(objectId, receiver.Address, mainAmount, stateJson, externalRef),
            cancellationToken);

    public Task<LedgerSubmitResult> UpdateBusinessObjectAsync(
        BusinessObjectId objectId,
        string stateJson,
        CancellationToken cancellationToken,
        string? externalRef = null)
        => Account.UpdateBusinessObjectAsync(
            new UpdateBusinessObjectRequest(objectId, stateJson, externalRef),
            cancellationToken);

    public Task<LedgerSubmitResult> CloseBusinessObjectAsync(
        BusinessObjectId objectId,
        CancellationToken cancellationToken,
        string stateJson = "{}",
        string? externalRef = null)
        => Account.CloseBusinessObjectAsync(
            new CloseBusinessObjectRequest(objectId, stateJson, externalRef),
            cancellationToken);

    public Task<LedgerSubmitResult> CloneBusinessObjectAsync(
        BusinessObjectId sourceObjectId,
        BusinessObjectId newObjectId,
        string objectType,
        string stateJson,
        CancellationToken cancellationToken,
        string schemaVersion = "1.0",
        Address? ownerAddress = null)
        => Account.CloneBusinessObjectAsync(
            new CloneBusinessObjectRequest(sourceObjectId, newObjectId, objectType, stateJson, schemaVersion, ownerAddress),
            cancellationToken);

    public Task<LedgerSubmitResult> MergeBusinessObjectsAsync(
        BusinessObjectId firstObjectId,
        BusinessObjectId secondObjectId,
        BusinessObjectId targetObjectId,
        string stateJson,
        CancellationToken cancellationToken,
        string? externalRef = null)
        => Account.MergeBusinessObjectsAsync(
            new MergeBusinessObjectsRequest(firstObjectId, secondObjectId, targetObjectId, stateJson, externalRef),
            cancellationToken);

    public Task<LedgerSubmitResult> SplitBusinessObjectPaymentAsync(
        BusinessObjectId objectId,
        IReadOnlyList<BusinessObjectPaymentOutput> payments,
        CancellationToken cancellationToken,
        string? stateJson = null,
        string? externalRef = null)
        => Account.SplitBusinessObjectPaymentAsync(
            new SplitBusinessObjectPaymentRequest(objectId, payments, stateJson, externalRef),
            cancellationToken);

    public Task<LedgerSubmitResult> SetBusinessObjectPriceAsync(
        BusinessObjectId objectId,
        Amount priceAmount,
        CancellationToken cancellationToken,
        TokenId? tokenId = null,
        string stateJson = "{}",
        string? externalRef = null)
        => Account.SetBusinessObjectPriceAsync(
            new SetBusinessObjectPriceRequest(objectId, priceAmount, tokenId, stateJson, externalRef),
            cancellationToken);

    public Task<LedgerSubmitResult> SubmitAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
        => Client.SubmitTransactionAsync(transaction, cancellationToken);

    public Task<LedgerAccountBalance> GetBalanceAsync(CancellationToken cancellationToken)
        => Account.GetBalanceAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(CancellationToken cancellationToken)
        => Account.GetSpendableOutputsAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerTokenType>> GetTokenTypesAsync(CancellationToken cancellationToken)
        => Account.GetTokenTypesAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetTokenSourceOutputsAsync(
        TokenId tokenId,
        CancellationToken cancellationToken)
        => Account.GetTokenSourceOutputsAsync(tokenId, cancellationToken);

    public Task<IReadOnlyList<LedgerPermissionSource>> GetMintPermissionSourcesAsync(
        TokenId tokenId,
        CancellationToken cancellationToken)
        => Account.GetMintPermissionSourcesAsync(tokenId, cancellationToken);

    public Task<IReadOnlyList<LedgerPermissionSource>> GetPermissionSourcesAsync(CancellationToken cancellationToken)
        => Account.GetPermissionSourcesAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(CancellationToken cancellationToken)
        => Account.GetBusinessObjectsAsync(cancellationToken);

    public Task<IReadOnlyList<IBusinessObjectView>> GetBusinessObjectViewsAsync(
        IBusinessObjectCastRegistry castRegistry,
        CancellationToken cancellationToken)
        => Account.GetBusinessObjectViewsAsync(castRegistry, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactions(int maxCount, CancellationToken cancellationToken)
        => GetTransactions(maxCount, includeData: false, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactions(
        int maxCount,
        bool includeData,
        CancellationToken cancellationToken)
        => Account.GetTransactionsAsync(maxCount, includeData, cancellationToken);

    public Task<EconomyNodeTransactionDetails?> GetTransactionDetailsAsync(
        TxId txId,
        CancellationToken cancellationToken)
        => ledgerDriver.GetTransactionAsync(txId, cancellationToken);

    public async Task<AccountSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        LedgerAccountBalance balance = await GetBalanceAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerBusinessObjectHolding> businessObjects = await GetBusinessObjectsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await GetTransactions(maxCount: 100, includeData: false, cancellationToken).ConfigureAwait(false);

        return new AccountSnapshot(
            Name,
            Address.Value,
            balance.MainBalance.ToString(),
            balance.UtxoCount,
            businessObjects,
            transactions.Count);
    }

    public void Dispose()
        => ledgerDriver.Dispose();
}

internal sealed record AccountSnapshot(
    string Name,
    string Address,
    string MainBalance,
    int UtxoCount,
    IReadOnlyList<LedgerBusinessObjectHolding> BusinessObjects,
    int TransactionCount)
{
    public int BusinessObjectCount => BusinessObjects.Count;
}

internal sealed record NodeMintTransactionRequest(
    string Address,
    decimal MainAmount,
    string? TokenId,
    decimal? TokenAmount,
    string? DataItemType,
    string? DataJson,
    string? ExternalRef,
    string? BusinessObjectId,
    string? ScenarioId);

internal sealed record NodeTransactionReceipt(string TxId, long LedgerSequence);
