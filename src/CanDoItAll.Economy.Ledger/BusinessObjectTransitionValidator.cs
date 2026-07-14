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
            .GroupBy(x => x.BusinessObject!.LineageKey)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var producedByObjectId = transaction.Outputs
            .Where(x => x.BusinessObject is not null)
            .GroupBy(x => x.BusinessObject!.LineageKey)
            .ToDictionary(x => x.Key, x => x.ToArray());

        if (consumedByObjectId.Count == 0 && producedByObjectId.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<ValidationError>();

        foreach ((BusinessObjectLineageKey objectId, UnspentOutput[] consumed) in consumedByObjectId)
        {
            if (consumed.Length > 1)
            {
                errors.Add(InvalidTransition(
                    $"BusinessObject {objectId.Value} cannot consume multiple current outputs in one transaction."));
            }
        }

        foreach ((BusinessObjectLineageKey objectId, TransactionOutput[] produced) in producedByObjectId)
        {
            if (produced.Length > 1)
            {
                errors.Add(InvalidTransition(
                    $"BusinessObject {objectId.Value} cannot emit multiple next outputs in one transaction."));
                continue;
            }

            TransactionOutput output = produced[0];
            consumedByObjectId.TryGetValue(objectId, out UnspentOutput[]? consumed);
            UnspentOutput? current = consumed?.Length == 1 ? consumed[0] : null;

            ValidateProducedReference(transaction, objectId, output, current, errors);
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
        BusinessObjectLineageKey objectId,
        TransactionOutput output,
        UnspentOutput? current,
        List<ValidationError> errors)
    {
        BusinessObjectRef next = output.BusinessObject!;
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
                ValidateCurrentVersionTransition(objectId, output, current, errors);
                break;
            case BusinessObjectOperation.Close:
                errors.Add(InvalidTransition(
                    $"BusinessObject {objectId.Value} close must consume the current output without emitting a next BusinessObject output."));
                break;
        }

        ValidateInlineDataBinding(transaction, objectId, next, current?.BusinessObject, errors);
    }

    private static void ValidateCreate(
        BusinessObjectLineageKey objectId,
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
        BusinessObjectLineageKey objectId,
        TransactionOutput output,
        UnspentOutput? current,
        List<ValidationError> errors)
    {
        BusinessObjectRef next = output.BusinessObject!;
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

        if (current.Token is null || output.Token is null || current.Token.TokenId != output.Token.TokenId)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} {next.Operation} must preserve carrier token id {current.Token?.TokenId.Value ?? "<missing>"}."));
        }

        if (next.Operation == BusinessObjectOperation.Update && output.Address != current.Address)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} update must keep owner {current.Address.Value}."));
        }

        if (next.Operation == BusinessObjectOperation.Transfer && output.Address == current.Address)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} transfer must change owner from {current.Address.Value}."));
        }
    }

    private static void ValidateInlineDataBinding(
        LedgerTransaction transaction,
        BusinessObjectLineageKey objectId,
        BusinessObjectRef next,
        BusinessObjectRef? current,
        List<ValidationError> errors)
    {
        TransactionData? data = transaction.Data;
        if (data is null)
        {
            return;
        }

        if (data.DataHash is null)
        {
            errors.Add(InvalidTransition(
                $"BusinessObject {objectId.Value} carries transaction data without a transaction data hash."));
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
                $"BusinessObject {objectId.Value} data hash must match the declared transaction data hash when the object state changes."));
        }
    }

    private static ValidationError InvalidTransition(string message)
        => new(ValidationErrorCodes.InvalidBusinessObjectTransition, message);
}
