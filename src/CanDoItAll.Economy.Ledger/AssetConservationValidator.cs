using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class AssetConservationValidator
{
    public ValidationResult ValidateTransfer(LedgerTransaction transaction, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        return transaction.Kind switch
        {
            TransactionKind.Genesis or TransactionKind.Mint => ValidationResult.Success,
            TransactionKind.Burn => ValidateBurn(transaction, inputs),
            _ => ValidateExactConservation(transaction, inputs)
        };
    }

    private static ValidationResult ValidateExactConservation(LedgerTransaction transaction, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        var errors = new List<ValidationError>();
        decimal inputMain = inputs.Values.Aggregate(0m, (sum, x) => sum + x.MainAmount.Value);
        decimal outputMain = transaction.Outputs.Aggregate(0m, (sum, x) => sum + x.MainAmount.Value);

        if (inputMain != outputMain)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.AssetImbalance, $"Main amount imbalance. Inputs={inputMain}, Outputs={outputMain}."));
        }

        var tokenInputs = SumTokens(inputs.Values.Select(x => x.Token));
        var tokenOutputs = SumTokens(transaction.Outputs.Select(x => x.Token));

        foreach (var tokenId in tokenInputs.Keys.Union(tokenOutputs.Keys))
        {
            tokenInputs.TryGetValue(tokenId, out var inputAmount);
            tokenOutputs.TryGetValue(tokenId, out var outputAmount);

            if (inputAmount != outputAmount)
            {
                errors.Add(new ValidationError(ValidationErrorCodes.AssetImbalance, $"Token imbalance for {tokenId.Value}. Inputs={inputAmount}, Outputs={outputAmount}."));
            }
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static ValidationResult ValidateBurn(LedgerTransaction transaction, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        var errors = new List<ValidationError>();
        decimal inputMain = inputs.Values.Aggregate(0m, (sum, x) => sum + x.MainAmount.Value);
        decimal outputMain = transaction.Outputs.Aggregate(0m, (sum, x) => sum + x.MainAmount.Value);

        if (outputMain > inputMain)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.AssetImbalance, $"Burn transaction cannot create main amount. Inputs={inputMain}, Outputs={outputMain}."));
        }

        var tokenInputs = SumTokens(inputs.Values.Select(x => x.Token));
        var tokenOutputs = SumTokens(transaction.Outputs.Select(x => x.Token));

        foreach (var tokenId in tokenOutputs.Keys)
        {
            tokenInputs.TryGetValue(tokenId, out var inputAmount);
            tokenOutputs.TryGetValue(tokenId, out var outputAmount);

            if (outputAmount > inputAmount)
            {
                errors.Add(new ValidationError(ValidationErrorCodes.AssetImbalance, $"Burn transaction cannot create token amount for {tokenId.Value}. Inputs={inputAmount}, Outputs={outputAmount}."));
            }
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static Dictionary<TokenId, decimal> SumTokens(IEnumerable<TokenTransfer?> tokens)
    {
        var result = new Dictionary<TokenId, decimal>();

        foreach (var token in tokens.Where(x => x is not null).Select(x => x!))
        {
            result.TryGetValue(token.TokenId, out var current);
            result[token.TokenId] = current + token.Amount.Value;
        }

        return result;
    }
}
