using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.BusinessObjects;

public sealed record AccountRefreshOptions
{
    public static AccountRefreshOptions Default { get; } = new();

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(5);

    public bool IncludeTransactionData { get; init; }

    public int? MaxTransactionCount { get; init; } = 100;

    public bool EmitExistingTransactionsOnInitialRefresh { get; init; }

    public bool RefreshBusinessObjectViews { get; init; } = true;

    public AccountRuleExecutionOptions? RuleExecution { get; init; }

    internal void Validate()
    {
        if (Interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Interval), "Refresh interval must be greater than zero.");
        }

        if (MaxTransactionCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxTransactionCount), "Max transaction count must be greater than zero when provided.");
        }

        RuleExecution?.Validate();
    }
}

public sealed record AccountRuleExecutionOptions
{
    public bool Enabled { get; init; }

    public int MaxActionsPerRefresh { get; init; } = 10;

    public bool MarkTriggerProcessed { get; init; } = true;

    public string ProcessedProperty { get; init; } = "processed";

    public bool KeepProcessedBusinessObject { get; init; } = true;

    internal void Validate()
    {
        if (MaxActionsPerRefresh <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxActionsPerRefresh), "Max actions per refresh must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(ProcessedProperty))
        {
            throw new ArgumentException("Processed property cannot be empty.", nameof(ProcessedProperty));
        }
    }
}

public readonly record struct BusinessObjectRefreshKey(
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    TransactionDataItemType ItemType)
{
    public static BusinessObjectRefreshKey FromHolding(LedgerBusinessObjectHolding holding)
        => new(
            holding.ObjectId,
            holding.ObjectType,
            holding.SchemaVersion,
            holding.Version,
            holding.OwnerAddress,
            holding.Operation,
            holding.DataHash,
            holding.CurrentOutPoint,
            holding.ItemType);
}

public sealed record AccountRefreshSnapshot(
    LedgerAccountContext Context,
    Address Address,
    LedgerAccountBalance Balance,
    IReadOnlyList<LedgerSpendableOutput> SpendableOutputs,
    IReadOnlyList<LedgerBusinessObjectHolding> BusinessObjects,
    IReadOnlyList<IBusinessObjectView> BusinessObjectViews,
    IReadOnlyList<LedgerTransactionSummary> Transactions,
    DateTimeOffset RefreshedAtUtc);

public sealed record AccountRefreshDelta(
    AccountRefreshSnapshot? PreviousSnapshot,
    AccountRefreshSnapshot CurrentSnapshot,
    IReadOnlyList<LedgerTransactionSummary> NewTransactions,
    IReadOnlyList<LedgerBusinessObjectHolding> AddedBusinessObjects,
    IReadOnlyList<LedgerBusinessObjectHolding> RemovedBusinessObjects,
    IReadOnlyList<LedgerBusinessObjectHolding> ChangedBusinessObjects);

public sealed record AccountRefreshResult(
    AccountRefreshSnapshot Snapshot,
    AccountRefreshDelta Delta,
    IReadOnlyList<AccountRuleExecutionResult> RuleExecutions);

public sealed record AccountRuleExecutionResult(
    string RuleId,
    BusinessObjectId? TriggerObjectId,
    AccountRuleActionKind ActionKind,
    AccountRuleExecutionStatus Status,
    TxId? TxId,
    string Message)
{
    public TxId? TriggerTransactionId { get; init; }
}

public enum AccountRuleExecutionStatus
{
    Executed = 0,
    Skipped = 1,
    Failed = 2
}

public sealed class AccountRefreshCompletedEventArgs(
    BusinessObjectAccount account,
    AccountRefreshResult result,
    BusinessObjectAccount? parentAccount = null,
    BusinessObjectSubAccount? subAccount = null) : EventArgs
{
    public BusinessObjectAccount Account { get; } = account;

    public AccountRefreshResult Result { get; } = result;

    public BusinessObjectAccount? ParentAccount { get; } = parentAccount;

    public BusinessObjectSubAccount? SubAccount { get; } = subAccount;

    public bool IsSubAccountEvent => ParentAccount is not null && SubAccount is not null;

    public AccountRefreshCompletedEventArgs RouteToParent(
        BusinessObjectAccount parentAccount,
        BusinessObjectSubAccount subAccount)
        => new(Account, Result, parentAccount, subAccount);
}

public sealed class AccountTransactionsObservedEventArgs(
    BusinessObjectAccount account,
    AccountRefreshResult result,
    BusinessObjectAccount? parentAccount = null,
    BusinessObjectSubAccount? subAccount = null) : EventArgs
{
    public BusinessObjectAccount Account { get; } = account;

    public AccountRefreshResult Result { get; } = result;

    public IReadOnlyList<LedgerTransactionSummary> NewTransactions => Result.Delta.NewTransactions;

    public BusinessObjectAccount? ParentAccount { get; } = parentAccount;

    public BusinessObjectSubAccount? SubAccount { get; } = subAccount;

    public bool IsSubAccountEvent => ParentAccount is not null && SubAccount is not null;

    public AccountTransactionsObservedEventArgs RouteToParent(
        BusinessObjectAccount parentAccount,
        BusinessObjectSubAccount subAccount)
        => new(Account, Result, parentAccount, subAccount);
}

public interface IAccountRefreshHandle : IAsyncDisposable, IDisposable
{
    BusinessObjectAccount Account { get; }

    AccountRefreshOptions Options { get; }

    Task RunningTask { get; }

    Task StopAsync(CancellationToken cancellationToken = default);
}

internal sealed class AccountRefreshHandle(
    BusinessObjectAccount account,
    AccountRefreshOptions options,
    Task runningTask,
    CancellationTokenSource cancellation) : IAccountRefreshHandle
{
    private int stopRequested;

    public BusinessObjectAccount Account { get; } = account;

    public AccountRefreshOptions Options { get; } = options;

    public Task RunningTask { get; } = runningTask;

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref stopRequested, 1) == 0)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await RunningTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref stopRequested, 1) == 0)
        {
            cancellation.Cancel();
        }

        if (RunningTask.IsCompleted)
        {
            cancellation.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
        => await StopAsync().ConfigureAwait(false);
}
