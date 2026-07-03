using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Services;

public static class LedgerTransactionOrdering
{
    public static IOrderedEnumerable<LedgerTransactionSummary> NewestFirst(IEnumerable<LedgerTransactionSummary> transactions)
        => transactions
            .OrderByDescending(transaction => transaction.LedgerSequence ?? long.MinValue)
            .ThenByDescending(transaction => transaction.Timestamp)
            .ThenByDescending(transaction => transaction.TxId.Value, StringComparer.Ordinal);

    public static LedgerTransactionSummary? Newest(IEnumerable<LedgerTransactionSummary> transactions)
        => NewestFirst(transactions).FirstOrDefault();
}
