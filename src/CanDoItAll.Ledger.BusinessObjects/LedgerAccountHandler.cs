namespace CanDoItAll.Ledger.BusinessObjects;

public interface ILedgerAccountHandler
{
    event EventHandler<AccountRefreshCompletedEventArgs>? RefreshCompleted;

    event EventHandler<AccountTransactionsObservedEventArgs>? NewTransactionsObserved;

    IReadOnlyCollection<IAccountRefreshHandle> RunningRefreshes { get; }

    void Observe(BusinessObjectAccount account);

    void Observe(BusinessObjectSubAccount subAccount);

    Task<AccountRefreshResult> RefreshAsync(
        BusinessObjectAccount account,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken);

    Task<AccountRefreshResult> RefreshAsync(
        BusinessObjectSubAccount subAccount,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken);

    Task<IAccountRefreshHandle> StartAutoRefreshAsync(
        BusinessObjectAccount account,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken);

    Task<IAccountRefreshHandle> StartAutoRefreshAsync(
        BusinessObjectSubAccount subAccount,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken);

    Task StopAutoRefreshAsync(BusinessObjectAccount account, CancellationToken cancellationToken = default);

    Task StopAutoRefreshAsync(BusinessObjectSubAccount subAccount, CancellationToken cancellationToken = default);

    Task StopAllAutoRefreshesAsync(CancellationToken cancellationToken = default);
}

public sealed class LedgerAccountHandler : ILedgerAccountHandler
{
    private readonly object gate = new();
    private readonly Dictionary<AccountRefreshIdentity, IAccountRefreshHandle> runningRefreshes = new();
    private readonly HashSet<BusinessObjectAccount> observedAccounts = new(ReferenceEqualityComparer.Instance);

    public event EventHandler<AccountRefreshCompletedEventArgs>? RefreshCompleted;

    public event EventHandler<AccountTransactionsObservedEventArgs>? NewTransactionsObserved;

    public IReadOnlyCollection<IAccountRefreshHandle> RunningRefreshes
    {
        get
        {
            lock (gate)
            {
                return runningRefreshes.Values.ToArray();
            }
        }
    }

    public void Observe(BusinessObjectAccount account)
    {
        lock (gate)
        {
            if (!observedAccounts.Add(account))
            {
                return;
            }

            account.RefreshCompleted += HandleAccountRefreshCompleted;
            account.NewTransactionsObserved += HandleAccountTransactionsObserved;
        }
    }

    public void Observe(BusinessObjectSubAccount subAccount)
        => Observe(subAccount.ParentAccount);

    public async Task<AccountRefreshResult> RefreshAsync(
        BusinessObjectAccount account,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        Observe(account);
        return await account.RefreshAsync(castRegistry, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountRefreshResult> RefreshAsync(
        BusinessObjectSubAccount subAccount,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        Observe(subAccount);
        return await subAccount.Account.RefreshAsync(castRegistry, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IAccountRefreshHandle> StartAutoRefreshAsync(
        BusinessObjectAccount account,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        Observe(account);
        return await StartAutoRefreshCoreAsync(account, castRegistry, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IAccountRefreshHandle> StartAutoRefreshAsync(
        BusinessObjectSubAccount subAccount,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        Observe(subAccount);
        return await StartAutoRefreshCoreAsync(subAccount.Account, castRegistry, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAutoRefreshAsync(BusinessObjectAccount account, CancellationToken cancellationToken = default)
    {
        IAccountRefreshHandle? handle;
        lock (gate)
        {
            AccountRefreshIdentity identity = AccountRefreshIdentity.FromAccount(account);
            if (!runningRefreshes.Remove(identity, out handle))
            {
                return;
            }
        }

        await handle.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAutoRefreshAsync(BusinessObjectSubAccount subAccount, CancellationToken cancellationToken = default)
        => StopAutoRefreshAsync(subAccount.Account, cancellationToken);

    public async Task StopAllAutoRefreshesAsync(CancellationToken cancellationToken = default)
    {
        IAccountRefreshHandle[] handles;
        lock (gate)
        {
            handles = runningRefreshes.Values.ToArray();
            runningRefreshes.Clear();
        }

        foreach (IAccountRefreshHandle handle in handles)
        {
            await handle.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IAccountRefreshHandle> StartAutoRefreshCoreAsync(
        BusinessObjectAccount account,
        IBusinessObjectCastRegistry? castRegistry,
        AccountRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        AccountRefreshIdentity identity = AccountRefreshIdentity.FromAccount(account);
        lock (gate)
        {
            if (runningRefreshes.TryGetValue(identity, out IAccountRefreshHandle? existing)
                && !existing.RunningTask.IsCompleted)
            {
                return existing;
            }
        }

        IAccountRefreshHandle handle = await account
            .StartAutoRefreshAsync(castRegistry, options, cancellationToken)
            .ConfigureAwait(false);

        lock (gate)
        {
            if (runningRefreshes.TryGetValue(identity, out IAccountRefreshHandle? existing)
                && !existing.RunningTask.IsCompleted)
            {
                _ = handle.StopAsync(CancellationToken.None);
                return existing;
            }

            runningRefreshes[identity] = handle;
        }

        _ = handle.RunningTask.ContinueWith(
            _ => RemoveCompletedHandle(identity, handle),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return handle;
    }

    private void RemoveCompletedHandle(AccountRefreshIdentity identity, IAccountRefreshHandle handle)
    {
        lock (gate)
        {
            if (runningRefreshes.TryGetValue(identity, out IAccountRefreshHandle? current)
                && ReferenceEquals(current, handle))
            {
                runningRefreshes.Remove(identity);
            }
        }
    }

    private void HandleAccountRefreshCompleted(object? sender, AccountRefreshCompletedEventArgs args)
        => RefreshCompleted?.Invoke(this, args);

    private void HandleAccountTransactionsObserved(object? sender, AccountTransactionsObservedEventArgs args)
        => NewTransactionsObserved?.Invoke(this, args);

    private readonly record struct AccountRefreshIdentity(
        string LedgerId,
        string? ScenarioId,
        string Address)
    {
        public static AccountRefreshIdentity FromAccount(BusinessObjectAccount account)
            => new(
                account.Context.LedgerId.Value,
                account.Context.ScenarioId?.Value,
                account.Address.Value);
    }
}
