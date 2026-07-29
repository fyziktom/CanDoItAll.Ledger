using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record AccountWorkspaceRefreshSnapshot(
    LedgerAccountContext Context,
    Address Address,
    IReadOnlyList<string> TransactionKeys,
    string Fingerprint,
    DateTimeOffset CheckedAtUtc)
{
    public bool HasSameTransactions(AccountWorkspaceRefreshSnapshot other)
        => string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);

    public static AccountWorkspaceRefreshSnapshot Create(
        BusinessObjectWorkspaceModel workspace,
        int maxTransactions,
        DateTimeOffset checkedAtUtc)
        => Create(
            workspace.Context,
            workspace.Address,
            workspace.Transactions,
            maxTransactions,
            checkedAtUtc);

    public static AccountWorkspaceRefreshSnapshot Create(
        LedgerAccountContext context,
        Address address,
        IEnumerable<LedgerTransactionSummary> transactions,
        int maxTransactions,
        DateTimeOffset checkedAtUtc)
    {
        int take = Math.Clamp(maxTransactions, 1, 100);
        string[] keys = transactions
            .Take(take)
            .Select(TransactionKey)
            .ToArray();

        return new AccountWorkspaceRefreshSnapshot(
            context,
            address,
            keys,
            string.Join('\n', keys),
            checkedAtUtc);
    }

    private static string TransactionKey(LedgerTransactionSummary transaction)
        => string.Join(
            '|',
            transaction.TxId.Value,
            (int)transaction.Kind,
            (int)transaction.Status,
            transaction.Timestamp.UtcTicks);
}
