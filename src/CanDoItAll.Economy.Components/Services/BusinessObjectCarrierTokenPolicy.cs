using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Services;

public static class BusinessObjectCarrierTokenPolicy
{
    public static EconomyTokenOption? SelectDefaultCarrierToken(
        IEnumerable<EconomyTokenOption>? tokenOptions,
        bool allowBalanceFallback = false)
    {
        EconomyTokenOption[] options = tokenOptions?.ToArray() ?? [];
        EconomyTokenOption? reusable = options
            .FirstOrDefault(option => !option.IsMain && option.TokenId is not null && option.HasReusableSource);
        if (reusable is not null || !allowBalanceFallback)
        {
            return reusable;
        }

        return options.FirstOrDefault(option =>
            !option.IsMain
            && option.TokenId is not null
            && option.AvailableAmount.Value > 0m);
    }

    public static string? GetDefaultCarrierTokenKey(IEnumerable<EconomyTokenOption>? tokenOptions)
        => SelectDefaultCarrierToken(tokenOptions)?.Key;

    public static BusinessObjectTransactionDraft WithDefaultCarrierToken(
        BusinessObjectTransactionDraft draft,
        IEnumerable<EconomyTokenOption>? tokenOptions,
        bool allowBalanceFallback = false)
    {
        if (draft.TokenId is not null || draft.TokenAmount is not null)
        {
            return draft;
        }

        EconomyTokenOption? token = SelectDefaultCarrierToken(tokenOptions, allowBalanceFallback);
        return token?.TokenId is null
            ? draft
            : draft with
            {
                TokenId = token.TokenId,
                TokenAmount = Amount.FromInt64(1)
            };
    }
}
