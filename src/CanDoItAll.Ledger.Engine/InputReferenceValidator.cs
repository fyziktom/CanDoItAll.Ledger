using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

public sealed class InputReferenceValidator
{
    public ValidationResult Validate(
        LedgerTransaction transaction,
        IReadOnlyList<OutPointKey> inputKeys,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> unspent,
        IReadOnlyDictionary<OutPointKey, SpentOutput> spent)
    {
        var errors = new List<ValidationError>();

        for (var index = 0; index < transaction.Inputs.Count; index++)
        {
            TransactionInput input = transaction.Inputs[index];
            OutPointKey key = inputKeys[index];

            if (spent.ContainsKey(key))
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.SpentInput,
                    $"Input is already spent: {input.PreviousOutPoint}."));
                continue;
            }

            if (!unspent.TryGetValue(key, out UnspentOutput? previousOutput))
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnknownInput,
                    $"Input is unknown: {input.PreviousOutPoint}."));
                continue;
            }

            if (previousOutput.Address != input.Address)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.AddressMismatch,
                    $"Input address does not match previous output: {input.PreviousOutPoint}."));
            }
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }
}
