using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class AssetConservationValidator
{
    public ValidationResult ValidateTransfer(LedgerTransaction transaction, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        return transaction.Kind switch
        {
            TransactionKind.Genesis => ValidationResult.Success,
            TransactionKind.Mint => ValidateMint(transaction, inputs),
            TransactionKind.Burn => ValidateBurn(transaction, inputs),
            _ => ValidateExactConservation(transaction, inputs)
        };
    }

    private static ValidationResult ValidateExactConservation(LedgerTransaction transaction, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        var errors = new List<ValidationError>();
        if (!TrySumAmounts(inputs.Values.Select(x => x.MainAmount.Value), "input main amount", errors, out decimal inputMain)
            || !TrySumAmounts(transaction.Outputs.Select(x => x.MainAmount.Value), "output main amount", errors, out decimal outputMain)
            || !TrySumTokens(inputs.Values.Select(x => x.Token), "input token amounts", errors, out Dictionary<TokenId, decimal> tokenInputs)
            || !TrySumTokens(transaction.Outputs.Select(x => x.Token), "output token amounts", errors, out Dictionary<TokenId, decimal> tokenOutputs))
        {
            return new ValidationResult(errors);
        }

        if (inputMain != outputMain)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.AssetImbalance, $"Main amount imbalance. Inputs={inputMain}, Outputs={outputMain}."));
        }

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
        if (!TrySumAmounts(inputs.Values.Select(x => x.MainAmount.Value), "input main amount", errors, out decimal inputMain)
            || !TrySumAmounts(transaction.Outputs.Select(x => x.MainAmount.Value), "output main amount", errors, out decimal outputMain)
            || !TrySumTokens(inputs.Values.Select(x => x.Token), "input token amounts", errors, out Dictionary<TokenId, decimal> tokenInputs)
            || !TrySumTokens(transaction.Outputs.Select(x => x.Token), "output token amounts", errors, out Dictionary<TokenId, decimal> tokenOutputs))
        {
            return new ValidationResult(errors);
        }

        if (outputMain > inputMain)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.AssetImbalance, $"Burn transaction cannot create main amount. Inputs={inputMain}, Outputs={outputMain}."));
        }

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

    private static ValidationResult ValidateMint(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        var errors = new List<ValidationError>();
        if (!TrySumAmounts(inputs.Values.Select(x => x.MainAmount.Value), "input main amount", errors, out decimal inputMain)
            || !TrySumAmounts(transaction.Outputs.Select(x => x.MainAmount.Value), "output main amount", errors, out decimal outputMain)
            || !TrySumTokens(inputs.Values.Select(x => x.Token), "input token amounts", errors, out Dictionary<TokenId, decimal> tokenInputs)
            || !TrySumTokens(transaction.Outputs.Select(x => x.Token), "output token amounts", errors, out Dictionary<TokenId, decimal> tokenOutputs))
        {
            return new ValidationResult(errors);
        }

        if (outputMain < inputMain)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.AssetImbalance,
                $"Mint transaction cannot destroy existing main amount. Inputs={inputMain}, Outputs={outputMain}."));
        }

        foreach (TokenId tokenId in tokenInputs.Keys)
        {
            decimal inputAmount = tokenInputs[tokenId];
            tokenOutputs.TryGetValue(tokenId, out decimal outputAmount);

            if (tokenId == PermissionTokenAuthority.PermissionTokenId)
            {
                if (outputAmount > inputAmount)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCodes.AssetImbalance,
                        $"Mint transaction cannot increase existing permission token supply for {tokenId.Value}. Inputs={inputAmount}, Outputs={outputAmount}."));
                }

                continue;
            }

            if (tokenId == RuleTokenAuthority.RuleTokenId)
            {
                if (outputAmount > inputAmount)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCodes.AssetImbalance,
                        $"Mint transaction cannot increase a consumed rule token balance for {tokenId.Value}. Inputs={inputAmount}, Outputs={outputAmount}."));
                }

                continue;
            }

            if (outputAmount != inputAmount)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.AssetImbalance,
                    $"Mint transaction must conserve pre-existing token {tokenId.Value}. Inputs={inputAmount}, Outputs={outputAmount}."));
            }
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static bool TrySumAmounts(
        IEnumerable<decimal> amounts,
        string scope,
        List<ValidationError> errors,
        out decimal result)
    {
        result = 0m;
        try
        {
            foreach (decimal amount in amounts)
            {
                result = checked(result + amount);
            }

            return true;
        }
        catch (OverflowException)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.AmountOverflow,
                $"Amount aggregation overflowed for {scope}."));
            result = 0m;
            return false;
        }
    }

    private static bool TrySumTokens(
        IEnumerable<TokenTransfer?> tokens,
        string scope,
        List<ValidationError> errors,
        out Dictionary<TokenId, decimal> result)
    {
        result = new Dictionary<TokenId, decimal>();

        try
        {
            foreach (TokenTransfer token in tokens.Where(x => x is not null).Select(x => x!))
            {
                result.TryGetValue(token.TokenId, out decimal current);
                result[token.TokenId] = checked(current + token.Amount.Value);
            }

            return true;
        }
        catch (OverflowException)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.AmountOverflow,
                $"Amount aggregation overflowed for {scope}."));
            result.Clear();
            return false;
        }
    }
}
