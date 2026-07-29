using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Services;

public static class BusinessObjectCarrierTokenPolicy
{
    public static LedgerTokenOption? SelectDefaultCarrierToken(
        IEnumerable<LedgerTokenOption>? tokenOptions,
        bool allowBalanceFallback = false)
    {
        LedgerTokenOption[] options = tokenOptions?.ToArray() ?? [];
        return options
            .FirstOrDefault(option => !option.IsMain && option.TokenId is not null && option.HasReusableSource);
    }

    public static string? GetDefaultCarrierTokenKey(IEnumerable<LedgerTokenOption>? tokenOptions)
        => SelectDefaultCarrierToken(tokenOptions)?.Key;

    public static BusinessObjectTransactionDraft WithDefaultCarrierToken(
        BusinessObjectTransactionDraft draft,
        IEnumerable<LedgerTokenOption>? tokenOptions,
        bool allowBalanceFallback = false)
    {
        if (draft.TokenId is not null || draft.TokenAmount is not null)
        {
            return draft;
        }

        LedgerTokenOption? token = SelectDefaultCarrierToken(tokenOptions, allowBalanceFallback);
        return token?.TokenId is null
            ? draft
            : draft with
            {
                TokenId = token.TokenId,
                TokenAmount = Amount.FromInt64(1)
            };
    }
}
