using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class LedgerBalanceCalculatorTests
{
    [Fact]
    public void CalculateAggregatesMainTokenBalancesAndUtxoCount()
    {
        var context = new LedgerAccountContext(new LedgerId("default"));
        Address address = Address.FromPublicKey(Hex.Utf8("alice-public"));
        var query = new LedgerAddressQuery(context, address);
        var tokenId = new TokenId("ops.credit");
        LedgerSpendableOutput[] outputs =
        [
            CreateOutput(context, address, "a", 0, 25m, tokenId, 3m),
            CreateOutput(context, address, "b", 0, 75m, tokenId, 4m),
            CreateOutput(context, address, "c", 0, 0m, new TokenId("empty"), 0m)
        ];

        LedgerAccountBalance balance = LedgerBalanceCalculator.Calculate(
            query,
            outputs,
            DateTimeOffset.Parse("2026-05-13T00:00:00Z"));

        Assert.Equal(new Amount(100m), balance.MainBalance);
        Assert.Equal(new Amount(7m), balance.TokenBalances[tokenId]);
        Assert.False(balance.TokenBalances.ContainsKey(new TokenId("empty")));
        Assert.Equal(3, balance.UtxoCount);
    }

    private static LedgerSpendableOutput CreateOutput(
        LedgerAccountContext context,
        Address address,
        string txId,
        int outputIndex,
        decimal mainAmount,
        TokenId? tokenId,
        decimal? tokenAmount)
        => new(
            context,
            new OutPointKey(new HashHex(new string(txId[0], 64))),
            new OutPoint(new TxId(txId), outputIndex),
            address,
            new Amount(mainAmount),
            tokenId is { } token ? new TokenTransfer(token, new Amount(tokenAmount ?? 0m)) : null,
            null,
            DateTimeOffset.Parse("2026-05-13T00:00:00Z"));
}
