using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectCarrierTokenPolicyTests
{
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("carrier-policy-alice"));
    private static readonly TokenId OperatingToken = new("ops.carrier");

    [Fact]
    public void SelectDefaultCarrierToken_PrefersFirstReusableNonMainToken()
    {
        IReadOnlyList<EconomyTokenOption> options =
        [
            new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(100), IsMain: true),
            new("unique", "unique", new TokenId("ops.unique"), Amount.FromInt64(1), IsMain: false, UniqueAmount: Amount.FromInt64(1), UniqueUtxoCount: 1, TotalUtxoCount: 1),
            new(OperatingToken.Value, OperatingToken.Value, OperatingToken, Amount.FromInt64(9), IsMain: false, SourceAmount: Amount.FromInt64(9), SourceUtxoCount: 1, TotalUtxoCount: 1)
        ];

        EconomyTokenOption? selected = BusinessObjectCarrierTokenPolicy.SelectDefaultCarrierToken(options);

        Assert.NotNull(selected);
        Assert.Equal(OperatingToken, selected!.TokenId);
        Assert.Equal(OperatingToken.Value, BusinessObjectCarrierTokenPolicy.GetDefaultCarrierTokenKey(options));
    }

    [Fact]
    public void WithDefaultCarrierToken_AttachesOneTokenAndPreservesMainPaymentAmount()
    {
        BusinessObjectTransactionDraft draft = new(
            new BusinessObjectId("bo:payment:carrier-policy"),
            "payment",
            "1.0",
            Alice,
            Alice,
            Alice,
            Amount.FromInt64(7),
            Amount.FromInt64(7),
            "{}",
            "bo-commerce://payment/carrier-policy");
        IReadOnlyList<EconomyTokenOption> options =
        [
            new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(100), IsMain: true),
            new(OperatingToken.Value, OperatingToken.Value, OperatingToken, Amount.FromInt64(9), IsMain: false, SourceAmount: Amount.FromInt64(9), SourceUtxoCount: 1, TotalUtxoCount: 1)
        ];

        BusinessObjectTransactionDraft updated = BusinessObjectCarrierTokenPolicy.WithDefaultCarrierToken(draft, options);

        Assert.Equal(OperatingToken, updated.TokenId);
        Assert.Equal("1", updated.TokenAmount!.Value.ToString());
        Assert.Equal("7", updated.MainAmount.ToString());
        Assert.Equal(Alice, updated.OwnerAddress);
    }

    [Fact]
    public void WithDefaultCarrierToken_CanUseBalanceFallbackWhenReusableSourceMetadataIsMissing()
    {
        BusinessObjectTransactionDraft draft = new(
            new BusinessObjectId("bo:receipt:carrier-policy"),
            "receipt",
            "1.0",
            Alice,
            Alice,
            Alice,
            Amount.Zero,
            Amount.Zero,
            "{}",
            "bo-commerce://receipt/carrier-policy");
        IReadOnlyList<EconomyTokenOption> options =
        [
            new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(100), IsMain: true),
            new(OperatingToken.Value, OperatingToken.Value, OperatingToken, Amount.FromInt64(9), IsMain: false, UniqueAmount: Amount.FromInt64(9), UniqueUtxoCount: 9, TotalUtxoCount: 9)
        ];

        BusinessObjectTransactionDraft strict = BusinessObjectCarrierTokenPolicy.WithDefaultCarrierToken(draft, options);
        BusinessObjectTransactionDraft fallback = BusinessObjectCarrierTokenPolicy.WithDefaultCarrierToken(draft, options, allowBalanceFallback: true);

        Assert.Null(strict.TokenId);
        Assert.Null(strict.TokenAmount);
        Assert.Equal(OperatingToken, fallback.TokenId);
        Assert.Equal("1", fallback.TokenAmount!.Value.ToString());
    }

    [Fact]
    public void WithDefaultCarrierToken_LeavesExplicitTokenAndMainOnlyDraftsAlone()
    {
        BusinessObjectTransactionDraft explicitTokenDraft = new(
            new BusinessObjectId("bo:explicit-token"),
            "post",
            "1.0",
            Alice,
            Alice,
            Alice,
            Amount.Zero,
            Amount.Zero,
            "{}",
            "bo://explicit",
            TokenId: new TokenId("ops.explicit"),
            TokenAmount: Amount.FromInt64(1));
        IReadOnlyList<EconomyTokenOption> mainOnly =
        [
            new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(100), IsMain: true)
        ];

        BusinessObjectTransactionDraft unchangedExplicit = BusinessObjectCarrierTokenPolicy.WithDefaultCarrierToken(explicitTokenDraft, mainOnly);
        BusinessObjectTransactionDraft unchangedMainOnly = BusinessObjectCarrierTokenPolicy.WithDefaultCarrierToken(explicitTokenDraft with { TokenId = null, TokenAmount = null }, mainOnly);

        Assert.Equal(new TokenId("ops.explicit"), unchangedExplicit.TokenId);
        Assert.Equal("1", unchangedExplicit.TokenAmount!.Value.ToString());
        Assert.Null(unchangedMainOnly.TokenId);
        Assert.Null(unchangedMainOnly.TokenAmount);
    }
}
