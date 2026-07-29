using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.BusinessObjects;

public static class LedgerBalanceCalculator
{
    public static LedgerAccountBalance Calculate(
        LedgerAddressQuery query,
        IReadOnlyCollection<LedgerSpendableOutput> spendableOutputs,
        DateTimeOffset asOfUtc)
    {
        decimal mainBalance = 0m;
        var tokenTotals = new Dictionary<TokenId, decimal>();

        foreach (LedgerSpendableOutput output in spendableOutputs)
        {
            mainBalance += output.MainAmount.Value;
            if (output.Token is null)
            {
                continue;
            }

            tokenTotals.TryGetValue(output.Token.TokenId, out decimal current);
            tokenTotals[output.Token.TokenId] = current + output.Token.Amount.Value;
        }

        IReadOnlyDictionary<TokenId, Amount> tokenBalances = tokenTotals
            .Where(item => item.Value != 0m)
            .OrderBy(item => item.Key.Value, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key,
                item => new Amount(item.Value));

        return new LedgerAccountBalance(
            query.Context,
            query.Address,
            new Amount(mainBalance),
            tokenBalances,
            spendableOutputs.Count,
            asOfUtc);
    }
}
