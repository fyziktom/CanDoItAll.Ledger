using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class BusinessObjectTransitionValidator
{
    public ValidationResult Validate(
        LedgerTransaction transaction,
        IReadOnlyList<OutPointKey> inputKeys,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        var consumedByObjectId = GetConsumedBusinessObjects(inputKeys, inputs)
            .GroupBy(x => x.BusinessObject!.ObjectId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var producedByObjectId = transaction.Outputs
            .Where(x => x.BusinessObject is not null)
            .GroupBy(x => x.BusinessObject!.ObjectId)
            .ToDictionary(x => x.Key, x => x.ToArray());

        if (consumedByObjectId.Count == 0 && producedByObjectId.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<ValidationError>();

        foreach ((BusinessObjectId objectId, UnspentOutput[] consumed) in consumedByObjectId)
        {
            if (consumed.Length > 1)
            {
                errors.Add(InvalidTransition(
                    $"BusinessObject {objectId.Value} cannot consume multiple current outputs in one transaction."));
            }
        }

        foreach ((BusinessObjectId objectId, TransactionOutput[] produced) in producedByObjectId)
        {
            if (produced.Length > 1)
            {
                errors.Add(InvalidTransition(
                    $"BusinessObject {objectId.Value} cannot emit multiple next outputs in one transaction."));
                continue;
            }

            TransactionOutput output = produced[0];
            BusinessObjectRef next = output.BusinessObject!;
            consumedByObjectId.TryGetValue(objectId, out UnspentOutput[]? consumed);
            UnspentOutput? current = consumed?.Length == 1 ? consumed[0] : null;

            ValidateProducedReference(transaction, objectId, next, current, errors);
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static IReadOnlyList<UnspentOutput> GetConsumedBusinessObjects(
        IReadOnlyList<OutPointKey> inputKeys,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        var result = new List<UnspentOutput>();
        foreach (OutPointKey inputKey in inputKeys)
        {
            if (inputs.TryGetValue(inputKey, out UnspentOutput? input)
                && input.BusinessObject is not null)
            {
                result.Add(input);
            }
        }

        return result;
    }

    private static void ValidateProducedReference(
        LedgerTransaction transaction,
        BusinessObjectId objectId,
        BusinessObjectRef next,
        UnspentOutput? current,
        List<ValidationError> errors)
    {
        if (!Enum.IsDefined(next.Operation))
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} has an unknown operation value: {(int)next.Operation}."));
            return;
        }

        if (next.Version <= 0)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} version must be positive."));
        }

        switch (next.Operation)
        {
            case BusinessObjectOperation.Create:
                ValidateCreate(objectId, next, current, errors);
                break;
            case BusinessObjectOperation.Update:
            case BusinessObjectOperation.Transfer:
                ValidateCurrentVersionTransition(objectId, next, current, errors);
                break;
            case BusinessObjectOperation.Close:
                errors.Add(InvalidTransition(
                    $"BusinessObject {objectId.Value} close must consume the current output without emitting a next BusinessObject output."));
                break;
        }

        ValidateInlineDataBinding(transaction, objectId, next, current?.BusinessObject, errors);
    }

    private static void ValidateCreate(
        BusinessObjectId objectId,
        BusinessObjectRef next,
        UnspentOutput? current,
        List<ValidationError> errors)
    {
        if (current is not null)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} create cannot consume an existing output for the same object id."));
        }

        if (next.Version != 1)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} create must start at version 1."));
        }
    }

    private static void ValidateCurrentVersionTransition(
        BusinessObjectId objectId,
        BusinessObjectRef next,
        UnspentOutput? current,
        List<ValidationError> errors)
    {
        if (current?.BusinessObject is null)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} {next.Operation} must consume the current output for the same object id."));
            return;
        }

        int expectedVersion = current.BusinessObject.Version + 1;
        if (next.Version != expectedVersion)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} {next.Operation} must advance from version {current.BusinessObject.Version} to {expectedVersion}, not {next.Version}."));
        }
    }

    private static void ValidateInlineDataBinding(
        LedgerTransaction transaction,
        BusinessObjectId objectId,
        BusinessObjectRef next,
        BusinessObjectRef? current,
        List<ValidationError> errors)
    {
        TransactionData? data = transaction.Data;
        if (data?.InlineJson is null)
        {
            return;
        }

        if (data.DataHash is null)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} carries inline transaction data without a transaction data hash."));
            return;
        }

        bool dataHashChanged = current is null || next.DataHash != current.DataHash;
        if (!dataHashChanged)
        {
            return;
        }

        if (next.DataHash != data.DataHash.Value)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} data hash must match transaction inline data hash when the object state changes."));
        }
    }

    private static ValidationError InvalidTransition(string message)
        => new(ValidationErrorCodes.InvalidBusinessObjectTransition, message);
}
