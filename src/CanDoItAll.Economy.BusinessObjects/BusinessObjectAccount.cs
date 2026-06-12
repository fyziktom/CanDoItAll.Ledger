using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed class BusinessObjectAccount(
    LedgerAccountContext context,
    IAccountKeySource keySource,
    ILedgerGateway ledgerGateway,
    ILedgerReadClient ledgerReadClient,
    BusinessObjectTransactionBuilder transactionBuilder)
{
    private readonly Dictionary<Address, BusinessObjectSubAccount> subAccounts = new();
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private AccountRefreshState refreshState = AccountRefreshState.Empty;

    public LedgerAccountContext Context { get; } = context;

    public Address Address => keySource.Address;

    public IAccountKeySource KeySource => keySource;

    public IReadOnlyCollection<BusinessObjectSubAccount> SubAccounts => subAccounts.Values;

    public AccountRefreshSnapshot? CurrentRefreshSnapshot => refreshState.Snapshot;

    public event EventHandler<AccountRefreshCompletedEventArgs>? RefreshCompleted;

    public event EventHandler<AccountTransactionsObservedEventArgs>? NewTransactionsObserved;

    public BusinessObjectSubAccount AddSubAccount(string name, IAccountKeySource subAccountKeySource)
    {
        var account = new BusinessObjectAccount(Context, subAccountKeySource, ledgerGateway, ledgerReadClient, transactionBuilder);
        var subAccount = new BusinessObjectSubAccount(name, this, account);
        account.RefreshCompleted += (_, args) => PublishRefreshCompleted(args.RouteToParent(this, subAccount));
        account.NewTransactionsObserved += (_, args) => PublishNewTransactionsObserved(args.RouteToParent(this, subAccount));
        subAccounts[subAccount.Address] = subAccount;
        return subAccount;
    }

    public Task<LedgerTransaction> BuildCreateBusinessObjectTransactionAsync(CreateBusinessObjectRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildCreateAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildTransferBusinessObjectTransactionAsync(TransferBusinessObjectRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildTransferAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildPaymentTransactionAsync(PaymentRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildPaymentAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildTokenTransferTransactionAsync(TokenTransferRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildTokenTransferAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildTokenMintTransactionAsync(TokenMintRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildTokenMintAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildPermissionIssueTransactionAsync(PermissionIssueRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildPermissionIssueAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildCreateRuleTokenTransactionAsync(RuleTokenCreateRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildCreateRuleTokenAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildUpdateBusinessObjectTransactionAsync(UpdateBusinessObjectRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildUpdateAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildCloseBusinessObjectTransactionAsync(CloseBusinessObjectRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildCloseAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildCloneBusinessObjectTransactionAsync(CloneBusinessObjectRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildCloneAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildMergeBusinessObjectsTransactionAsync(MergeBusinessObjectsRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildMergeAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildSplitBusinessObjectPaymentTransactionAsync(SplitBusinessObjectPaymentRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildSplitPaymentAsync(Context, keySource, request, cancellationToken);

    public Task<LedgerTransaction> BuildSetBusinessObjectPriceTransactionAsync(SetBusinessObjectPriceRequest request, CancellationToken cancellationToken)
        => transactionBuilder.BuildSetPriceAsync(Context, keySource, request, cancellationToken);

    public async Task<LedgerSubmitResult> CreateBusinessObjectAsync(CreateBusinessObjectRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildCreateBusinessObjectTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> TransferBusinessObjectAsync(TransferBusinessObjectRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildTransferBusinessObjectTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> SendPaymentAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildPaymentTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> SendTokenTransferAsync(TokenTransferRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildTokenTransferTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> MintTokenAsync(TokenMintRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildTokenMintTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> IssuePermissionAsync(PermissionIssueRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildPermissionIssueTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> CreateRuleTokenAsync(RuleTokenCreateRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildCreateRuleTokenTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> UpdateBusinessObjectAsync(UpdateBusinessObjectRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildUpdateBusinessObjectTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> CloseBusinessObjectAsync(CloseBusinessObjectRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildCloseBusinessObjectTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> CloneBusinessObjectAsync(CloneBusinessObjectRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildCloneBusinessObjectTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> MergeBusinessObjectsAsync(MergeBusinessObjectsRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildMergeBusinessObjectsTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> SplitBusinessObjectPaymentAsync(SplitBusinessObjectPaymentRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildSplitBusinessObjectPaymentTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerSubmitResult> SetBusinessObjectPriceAsync(SetBusinessObjectPriceRequest request, CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = await BuildSetBusinessObjectPriceTransactionAsync(request, cancellationToken).ConfigureAwait(false);
        return await SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task<LedgerSubmitResult> SubmitTransactionAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
        => ledgerGateway.SubmitTransactionAsync(transaction, cancellationToken);

    public Task<LedgerAccountBalance> GetBalanceAsync(CancellationToken cancellationToken)
        => ledgerReadClient.GetBalanceAsync(new LedgerAddressQuery(Context, Address), cancellationToken);

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(CancellationToken cancellationToken)
        => ledgerReadClient.GetSpendableOutputsAsync(new LedgerAddressQuery(Context, Address), cancellationToken);

    public async Task<IReadOnlyList<LedgerTokenType>> GetTokenTypesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> outputs = await GetSpendableOutputsAsync(cancellationToken).ConfigureAwait(false);
        return outputs
            .Where(output => output.Token is not null)
            .GroupBy(output => output.Token!.TokenId)
            .OrderBy(group => group.Key.Value, StringComparer.Ordinal)
            .Select(group =>
            {
                LedgerSpendableOutput[] typedOutputs = group.ToArray();
                LedgerSpendableOutput[] sourceOutputs = typedOutputs
                    .Where(output => output.Token!.Amount.Value > 1m)
                    .ToArray();
                LedgerSpendableOutput[] uniqueOutputs = typedOutputs
                    .Where(output => output.Token!.Amount.Value == 1m)
                    .ToArray();

                return new LedgerTokenType(
                    group.Key,
                    new Amount(typedOutputs.Sum(output => output.Token!.Amount.Value)),
                    new Amount(sourceOutputs.Sum(output => output.Token!.Amount.Value)),
                    new Amount(uniqueOutputs.Sum(output => output.Token!.Amount.Value)),
                    sourceOutputs.Length,
                    uniqueOutputs.Length,
                    typedOutputs.Length);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<LedgerSpendableOutput>> GetTokenSourceOutputsAsync(
        TokenId tokenId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> outputs = await GetSpendableOutputsAsync(cancellationToken).ConfigureAwait(false);
        return outputs
            .Where(output => output.BusinessObject is null
                && output.Token?.TokenId == tokenId
                && output.Token.Amount.Value > 1m)
            .OrderBy(output => output.CreatedAt)
            .ThenBy(output => output.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(output => output.OutPoint.OutputIndex)
            .ToArray();
    }

    public Task<IReadOnlyList<LedgerPermissionSource>> GetPermissionSourcesAsync(CancellationToken cancellationToken)
        => PermissionSourceResolver.GetPermissionSourcesAsync(
            ledgerReadClient,
            new LedgerAddressQuery(Context, Address),
            cancellationToken);

    public async Task<IReadOnlyList<LedgerPermissionSource>> GetMintPermissionSourcesAsync(
        TokenId tokenId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerPermissionSource> sources = await GetPermissionSourcesAsync(cancellationToken).ConfigureAwait(false);
        return sources
            .Where(source => source.AllowsMint(tokenId))
            .OrderBy(source => source.Grant.Namespace.Length)
            .ThenBy(source => source.Output.CreatedAt)
            .ThenBy(source => source.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(source => source.OutPoint.OutputIndex)
            .ToArray();
    }

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(CancellationToken cancellationToken)
        => ledgerReadClient.GetBusinessObjectsAsync(new LedgerAddressQuery(Context, Address), cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(CancellationToken cancellationToken)
        => GetTransactionsAsync(includeData: false, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        bool includeData,
        CancellationToken cancellationToken)
        => ledgerReadClient.GetTransactionsAsync(new LedgerAddressQuery(Context, Address), includeData, cancellationToken);

    public async Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        int maxCount,
        CancellationToken cancellationToken)
        => await GetTransactionsAsync(maxCount, includeData: false, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        int maxCount,
        bool includeData,
        CancellationToken cancellationToken)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<LedgerTransactionSummary>();
        }

        IReadOnlyList<LedgerTransactionSummary> transactions = await GetTransactionsAsync(includeData, cancellationToken).ConfigureAwait(false);
        return transactions.Take(maxCount).ToArray();
    }

    public async Task<IReadOnlyList<IBusinessObjectView>> GetBusinessObjectViewsAsync(
        IBusinessObjectCastRegistry castRegistry,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerBusinessObjectHolding> holdings = await GetBusinessObjectsAsync(cancellationToken).ConfigureAwait(false);
        var views = new List<IBusinessObjectView>(holdings.Count);

        foreach (LedgerBusinessObjectHolding holding in holdings)
        {
            views.Add(await castRegistry
                .CastAsync(
                    new BusinessObjectCastSource(
                        Context,
                        holding.ObjectId,
                        holding.ObjectType,
                        holding.SchemaVersion,
                        holding.Version,
                        holding.OwnerAddress,
                        holding.Operation,
                        holding.DataHash,
                        holding.CurrentOutPoint,
                        holding.StateJson,
                        holding.ExternalRef,
                        holding.ItemType,
                        holding.CreatedAt),
                    cancellationToken)
                .ConfigureAwait(false));
        }

        return views;
    }

    public Task<AccountRefreshResult> RefreshAsync(CancellationToken cancellationToken)
        => RefreshAsync(castRegistry: null, options: null, cancellationToken);

    public Task<AccountRefreshResult> RefreshAsync(
        AccountRefreshOptions options,
        CancellationToken cancellationToken)
        => RefreshAsync(castRegistry: null, options, cancellationToken);

    public Task<AccountRefreshResult> RefreshAsync(
        IBusinessObjectCastRegistry castRegistry,
        CancellationToken cancellationToken)
        => RefreshAsync(castRegistry, options: null, cancellationToken);

    public async Task<AccountRefreshResult> RefreshAsync(
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= AccountRefreshOptions.Default;
        options.Validate();

        AccountRefreshResult result;
        await refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LedgerAddressQuery query = new(Context, Address);
            IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
                .GetTransactionsAsync(query, options.IncludeTransactionData, cancellationToken)
                .ConfigureAwait(false);
            LedgerAccountBalance balance = await ledgerReadClient.GetBalanceAsync(query, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<LedgerSpendableOutput> outputs = await ledgerReadClient.GetSpendableOutputsAsync(query, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<LedgerBusinessObjectHolding> holdings = await ledgerReadClient.GetBusinessObjectsAsync(query, cancellationToken).ConfigureAwait(false);

            if (options.MaxTransactionCount is int maxTransactionCount)
            {
                transactions = transactions.Take(maxTransactionCount).ToArray();
            }

            result = await BuildRefreshResultAsync(
                    balance,
                    outputs,
                    holdings,
                    transactions,
                    castRegistry,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            refreshLock.Release();
        }

        if (options.RuleExecution?.Enabled == true)
        {
            IReadOnlyList<AccountRuleExecutionResult> ruleExecutions = await AccountRuleExecutor
                .ExecuteAsync(this, result, options.RuleExecution, cancellationToken)
                .ConfigureAwait(false);
            result = result with { RuleExecutions = ruleExecutions };
        }

        PublishRefreshCompleted(new AccountRefreshCompletedEventArgs(this, result));

        if (result.Delta.NewTransactions.Count > 0)
        {
            PublishNewTransactionsObserved(new AccountTransactionsObservedEventArgs(this, result));
        }

        return result;
    }

    public Task<IAccountRefreshHandle> StartAutoRefreshAsync(CancellationToken cancellationToken)
        => StartAutoRefreshAsync(castRegistry: null, options: null, cancellationToken);

    public Task<IAccountRefreshHandle> StartAutoRefreshAsync(
        AccountRefreshOptions options,
        CancellationToken cancellationToken)
        => StartAutoRefreshAsync(castRegistry: null, options, cancellationToken);

    public Task<IAccountRefreshHandle> StartAutoRefreshAsync(
        IBusinessObjectCastRegistry castRegistry,
        CancellationToken cancellationToken)
        => StartAutoRefreshAsync(castRegistry, options: null, cancellationToken);

    public async Task<IAccountRefreshHandle> StartAutoRefreshAsync(
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= AccountRefreshOptions.Default;
        options.Validate();

        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await RefreshAsync(castRegistry, options, linkedCancellation.Token).ConfigureAwait(false);
        }
        catch
        {
            linkedCancellation.Dispose();
            throw;
        }

        Task runningTask = RunAutoRefreshLoopAsync(castRegistry, options, linkedCancellation.Token);
        return new AccountRefreshHandle(this, options, runningTask, linkedCancellation);
    }

    public Task<EconomyMessageSignature> SignMessageAsync(string message, CancellationToken cancellationToken)
        => AccountMessageSecurity.SignMessageAsync(keySource, message, cancellationToken);

    public bool VerifyMessage(string message, EconomyMessageSignature signature)
        => AccountMessageSecurity.VerifyMessage(message, signature);

    public Task<HashHex> DeriveSharedSecretHashAsync(byte[] otherPublicKeyMaterial, CancellationToken cancellationToken)
        => AccountMessageSecurity.DeriveSharedSecretHashAsync(keySource, otherPublicKeyMaterial, cancellationToken);

    public Task<EconomyEncryptedMessage> EncryptMessageAsync(
        string message,
        Address receiverAddress,
        byte[] receiverPublicKeyMaterial,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.EncryptMessageAsync(keySource, message, receiverAddress, receiverPublicKeyMaterial, cancellationToken);

    public Task<string> DecryptMessageAsync(
        EconomyEncryptedMessage encryptedMessage,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.DecryptMessageAsync(keySource, encryptedMessage, cancellationToken);

    private async Task<AccountRefreshResult> BuildRefreshResultAsync(
        LedgerAccountBalance balance,
        IReadOnlyList<LedgerSpendableOutput> outputs,
        IReadOnlyList<LedgerBusinessObjectHolding> holdings,
        IReadOnlyList<LedgerTransactionSummary> transactions,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions options,
        CancellationToken cancellationToken)
    {
        AccountRefreshSnapshot? previousSnapshot = refreshState.Snapshot;
        Dictionary<BusinessObjectRefreshKey, LedgerBusinessObjectHolding> currentHoldingsByKey = holdings
            .ToDictionary(BusinessObjectRefreshKey.FromHolding);
        HashSet<BusinessObjectRefreshKey> previousKeys = refreshState.BusinessObjectsByKey.Keys.ToHashSet();
        HashSet<BusinessObjectRefreshKey> currentKeys = currentHoldingsByKey.Keys.ToHashSet();

        LedgerBusinessObjectHolding[] addedBusinessObjects = currentKeys
            .Except(previousKeys)
            .Select(key => currentHoldingsByKey[key])
            .ToArray();
        LedgerBusinessObjectHolding[] removedBusinessObjects = previousKeys
            .Except(currentKeys)
            .Select(key => refreshState.BusinessObjectsByKey[key])
            .ToArray();
        HashSet<BusinessObjectId> previousObjectIds = refreshState.BusinessObjectsByKey.Values
            .Select(holding => holding.ObjectId)
            .ToHashSet();
        LedgerBusinessObjectHolding[] changedBusinessObjects = addedBusinessObjects
            .Where(holding => previousObjectIds.Contains(holding.ObjectId))
            .ToArray();

        Dictionary<BusinessObjectRefreshKey, IBusinessObjectView> nextViewsByKey = new();
        var views = new List<IBusinessObjectView>(holdings.Count);
        if (options.RefreshBusinessObjectViews)
        {
            foreach (LedgerBusinessObjectHolding holding in holdings)
            {
                BusinessObjectRefreshKey key = BusinessObjectRefreshKey.FromHolding(holding);
                if (!refreshState.BusinessObjectViewsByKey.TryGetValue(key, out IBusinessObjectView? view))
                {
                    if (castRegistry is null)
                    {
                        continue;
                    }

                    view = await castRegistry
                        .CastAsync(CreateCastSource(holding), cancellationToken)
                        .ConfigureAwait(false);
                }

                nextViewsByKey[key] = view;
                views.Add(view);
            }
        }

        LedgerTransactionSummary[] newTransactions = GetNewTransactions(transactions, options);
        var snapshot = new AccountRefreshSnapshot(
            Context,
            Address,
            balance,
            outputs,
            holdings,
            views,
            transactions,
            DateTimeOffset.UtcNow);
        var delta = new AccountRefreshDelta(
            previousSnapshot,
            snapshot,
            newTransactions,
            addedBusinessObjects,
            removedBusinessObjects,
            changedBusinessObjects);

        refreshState = new AccountRefreshState(
            snapshot,
            currentHoldingsByKey,
            nextViewsByKey,
            transactions.Select(transaction => transaction.TxId).ToHashSet());

        return new AccountRefreshResult(snapshot, delta, Array.Empty<AccountRuleExecutionResult>());
    }

    private LedgerTransactionSummary[] GetNewTransactions(
        IReadOnlyList<LedgerTransactionSummary> transactions,
        AccountRefreshOptions options)
    {
        if (refreshState.Snapshot is null && !options.EmitExistingTransactionsOnInitialRefresh)
        {
            return [];
        }

        return transactions
            .Where(transaction => !refreshState.ObservedTransactionIds.Contains(transaction.TxId))
            .ToArray();
    }

    private async Task RunAutoRefreshLoopAsync(
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(options.Interval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await RefreshAsync(castRegistry, options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private BusinessObjectCastSource CreateCastSource(LedgerBusinessObjectHolding holding)
        => new(
            Context,
            holding.ObjectId,
            holding.ObjectType,
            holding.SchemaVersion,
            holding.Version,
            holding.OwnerAddress,
            holding.Operation,
            holding.DataHash,
            holding.CurrentOutPoint,
            holding.StateJson,
            holding.ExternalRef,
            holding.ItemType,
            holding.CreatedAt);

    private void PublishRefreshCompleted(AccountRefreshCompletedEventArgs args)
        => RefreshCompleted?.Invoke(args.ParentAccount ?? this, args);

    private void PublishNewTransactionsObserved(AccountTransactionsObservedEventArgs args)
        => NewTransactionsObserved?.Invoke(args.ParentAccount ?? this, args);

    private sealed record AccountRefreshState(
        AccountRefreshSnapshot? Snapshot,
        IReadOnlyDictionary<BusinessObjectRefreshKey, LedgerBusinessObjectHolding> BusinessObjectsByKey,
        IReadOnlyDictionary<BusinessObjectRefreshKey, IBusinessObjectView> BusinessObjectViewsByKey,
        IReadOnlySet<TxId> ObservedTransactionIds)
    {
        public static AccountRefreshState Empty { get; } = new(
            null,
            new Dictionary<BusinessObjectRefreshKey, LedgerBusinessObjectHolding>(),
            new Dictionary<BusinessObjectRefreshKey, IBusinessObjectView>(),
            new HashSet<TxId>());
    }
}
