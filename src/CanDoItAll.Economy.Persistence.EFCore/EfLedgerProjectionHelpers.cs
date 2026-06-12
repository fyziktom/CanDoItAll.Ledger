using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Persistence.EFCore;

public static class EfLedgerQueryBatcher
{
    public const int DefaultBatchSize = 500;

    public static async Task<List<T>> ToListByBatchedKeysAsync<T, TKey>(
        IReadOnlyCollection<TKey> keys,
        Func<IReadOnlyList<TKey>, IQueryable<T>> createQuery,
        CancellationToken cancellationToken,
        int batchSize = DefaultBatchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        if (keys.Count == 0)
        {
            return [];
        }

        var rows = new List<T>();
        foreach (TKey[] batch in keys.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.AddRange(await createQuery(batch)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        return rows;
    }
}

internal static class EfLedgerProjectionCalculator
{
    public static EfProjectionBalance CalculateBalance(IReadOnlyCollection<UnspentOutputRecord> rows)
    {
        decimal mainBalance = rows.Aggregate(0m, (sum, row) => sum + row.MainAmount);
        var tokenBalances = new SortedDictionary<string, decimal>(StringComparer.Ordinal);

        foreach (IGrouping<string, UnspentOutputRecord> group in rows
            .Where(row => !string.IsNullOrWhiteSpace(row.TokenId))
            .GroupBy(row => row.TokenId!, StringComparer.Ordinal)
            .OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            decimal tokenBalance = group.Aggregate(0m, (sum, row) => sum + (row.TokenAmount ?? 0m));
            if (tokenBalance != 0m)
            {
                tokenBalances[group.Key] = tokenBalance;
            }
        }

        return new EfProjectionBalance(mainBalance, tokenBalances, rows.Count);
    }

    public static HashHex CalculateAddressProjectionHash(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        string address,
        decimal mainBalance,
        IReadOnlyDictionary<string, decimal> tokenBalances,
        int utxoCount,
        long ledgerSequence)
    {
        string tokenText = string.Join(';', tokenBalances.Select(item => $"{item.Key}={FormatAmount(item.Value)}"));
        string value = string.Join('|',
            "cdia.economy.address-projection.v1",
            ledgerId.Value,
            scenarioId?.Value ?? string.Empty,
            address,
            FormatAmount(mainBalance),
            tokenText,
            utxoCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ledgerSequence.ToString(System.Globalization.CultureInfo.InvariantCulture));

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new HashHex(Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static string FormatAmount(decimal value)
        => new Amount(value).ToString();
}

internal sealed record EfProjectionBalance(
    decimal MainBalance,
    IReadOnlyDictionary<string, decimal> TokenBalances,
    int UtxoCount);
