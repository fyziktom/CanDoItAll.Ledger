using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class BusinessObjectReferenceValidator
{
    public async Task<ValidationResult> ValidateAsync(
        LedgerTransactionValidationContext context,
        CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = context.Transaction;
        if (!BusinessObjectPayloadEnvelopeReader.TryRead(
                transaction.Data?.InlineJson,
                out BusinessObjectPayloadEnvelope envelope,
                out bool hasInvalidReferences))
        {
            return hasInvalidReferences
                ? ValidationResult.Failure(InvalidReference(
                    "Structured BusinessObject source references are malformed."))
                : ValidationResult.Success;
        }

        if (envelope.References.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<ValidationError>();
        HashHex? transactionDataHash = transaction.Data?.DataHash;
        string[] relationships = envelope.References
            .Select(reference => reference.Relationship)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (relationships.Length != 1)
        {
            errors.Add(InvalidReference("A BusinessObject payload cannot mix source reference relationships."));
            return new ValidationResult(errors);
        }

        string relationship = relationships[0];
        int requiredCount = relationship switch
        {
            BusinessObjectReferenceRelationships.CloneSource => 1,
            BusinessObjectReferenceRelationships.MergeSource => 2,
            BusinessObjectReferenceRelationships.PreviousVersion => 1,
            _ => 0
        };
        if (requiredCount == 0)
        {
            errors.Add(InvalidReference($"Unknown BusinessObject source relationship: {relationship}."));
            return new ValidationResult(errors);
        }

        TransactionOutput[] businessObjectOutputs = transaction.Outputs
            .Where(output => output.BusinessObject is not null)
            .ToArray();
        TransactionOutput[] boundOutputs = transactionDataHash is null
            ? []
            : businessObjectOutputs
                .Where(output => output.BusinessObject!.DataHash == transactionDataHash.Value)
                .ToArray();
        bool isClose = relationship == BusinessObjectReferenceRelationships.PreviousVersion
            && businessObjectOutputs.Length == 0;
        BusinessObjectRef? boundTarget = !isClose && boundOutputs.Length == 1
            ? boundOutputs[0].BusinessObject
            : null;
        if (transactionDataHash is null || (!isClose && boundOutputs.Length != 1))
        {
            errors.Add(InvalidReference(
                "Structured BusinessObject references must bind to exactly one BusinessObject output data hash."));
        }

        if (!isClose && boundOutputs.Length == 1)
        {
            ValidateTargetOperation(relationship, boundOutputs[0].BusinessObject!, errors);
        }

        if (envelope.References.Count != requiredCount
            || envelope.References.Select(reference => reference.ObjectId).Distinct().Count() != requiredCount)
        {
            errors.Add(InvalidReference(
                $"BusinessObject relationship {relationship} requires {requiredCount} distinct source reference(s)."));
        }

        UnspentOutput[] consumedBusinessObjects = context.InputKeys
            .Where(context.UnspentOutputs.ContainsKey)
            .Select(key => context.UnspentOutputs[key])
            .Where(output => output.BusinessObject is not null)
            .ToArray();
        HashSet<OutPoint> consumedBusinessObjectOutPoints = consumedBusinessObjects
            .Select(output => output.OutPoint)
            .ToHashSet();

        foreach (BusinessObjectDataReference reference in envelope.References)
        {
            if (!reference.ObjectId.TryGetOutPoint(out OutPoint sourceOutPoint))
            {
                errors.Add(InvalidReference(
                    $"BusinessObject source id must be a UTXO id: {reference.ObjectId.Value}."));
                continue;
            }

            LedgerTransaction? sourceTransaction = await context.Store.GetTransactionAsync(
                transaction.LedgerId,
                transaction.ScenarioId,
                sourceOutPoint.TxId,
                cancellationToken).ConfigureAwait(false);
            TransactionOutput[] sourceOutputs = sourceTransaction?.Outputs
                .Where(output =>
                    output.OutputIndex == sourceOutPoint.OutputIndex && output.BusinessObject is not null)
                .ToArray()
                ?? [];
            if (sourceOutputs.Length != 1)
            {
                errors.Add(InvalidReference(
                    $"BusinessObject source output does not exist: {reference.ObjectId.Value}."));
            }
            else
            {
                ValidateSourcePayload(
                    envelope,
                    reference,
                    sourceTransaction!,
                    sourceOutputs[0],
                    errors);
                ValidatePreviousVersionLineage(
                    relationship,
                    boundTarget,
                    sourceOutputs[0].BusinessObject!,
                    reference,
                    errors);
            }

            if ((relationship is BusinessObjectReferenceRelationships.MergeSource
                    or BusinessObjectReferenceRelationships.PreviousVersion)
                && !consumedBusinessObjectOutPoints.Contains(sourceOutPoint))
            {
                errors.Add(InvalidReference(
                    $"BusinessObject {relationship} source must be consumed by the transaction: {reference.ObjectId.Value}."));
            }
        }

        if (relationship == BusinessObjectReferenceRelationships.CloneSource
            && envelope.References.Any(reference =>
                reference.ObjectId.TryGetOutPoint(out OutPoint source)
                && consumedBusinessObjectOutPoints.Contains(source)))
        {
            errors.Add(InvalidReference("Cloning must reference its source without consuming or mutating it."));
        }

        ValidateConsumedBusinessObjectCoverage(
            relationship,
            envelope.References,
            consumedBusinessObjects,
            businessObjectOutputs,
            errors);

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static void ValidateConsumedBusinessObjectCoverage(
        string relationship,
        IReadOnlyList<BusinessObjectDataReference> references,
        IReadOnlyList<UnspentOutput> consumedBusinessObjects,
        IReadOnlyList<TransactionOutput> producedBusinessObjects,
        List<ValidationError> errors)
    {
        var requiredConsumedSources = new HashSet<OutPoint>();
        if (relationship is BusinessObjectReferenceRelationships.PreviousVersion
            or BusinessObjectReferenceRelationships.MergeSource)
        {
            foreach (BusinessObjectDataReference reference in references)
            {
                if (reference.ObjectId.TryGetOutPoint(out OutPoint sourceOutPoint))
                {
                    requiredConsumedSources.Add(sourceOutPoint);
                }
            }
        }
        HashSet<BusinessObjectLineageKey> producedLineages = producedBusinessObjects
            .Select(output => output.BusinessObject!.LineageKey)
            .ToHashSet();

        foreach (UnspentOutput consumed in consumedBusinessObjects)
        {
            if (requiredConsumedSources.Contains(consumed.OutPoint)
                || producedLineages.Contains(consumed.BusinessObject!.LineageKey))
            {
                continue;
            }

            BusinessObjectId consumedObjectId = BusinessObjectId.FromOutPoint(consumed.OutPoint);
            errors.Add(InvalidReference(
                $"Structured BusinessObject transaction cannot consume unrelated source {consumedObjectId.Value} without re-emitting the same lineage."));
        }
    }

    private static void ValidatePreviousVersionLineage(
        string relationship,
        BusinessObjectRef? target,
        BusinessObjectRef source,
        BusinessObjectDataReference reference,
        List<ValidationError> errors)
    {
        if (relationship != BusinessObjectReferenceRelationships.PreviousVersion
            || target?.Operation is not (BusinessObjectOperation.Update or BusinessObjectOperation.Transfer)
            || target.LineageKey == source.LineageKey)
        {
            return;
        }

        errors.Add(InvalidReference(
            $"BusinessObject previous-version source must belong to the same lineage as the updated or transferred target: {reference.ObjectId.Value}."));
    }

    private static void ValidateTargetOperation(
        string relationship,
        BusinessObjectRef target,
        List<ValidationError> errors)
    {
        BusinessObjectOperation[] allowedOperations = relationship switch
        {
            BusinessObjectReferenceRelationships.PreviousVersion =>
                [BusinessObjectOperation.Update, BusinessObjectOperation.Transfer],
            BusinessObjectReferenceRelationships.CloneSource or BusinessObjectReferenceRelationships.MergeSource =>
                [BusinessObjectOperation.Create],
            _ => []
        };
        if (!allowedOperations.Contains(target.Operation))
        {
            errors.Add(InvalidReference(
                $"BusinessObject relationship {relationship} cannot produce a {target.Operation} output."));
        }
    }

    private static void ValidateSourcePayload(
        BusinessObjectPayloadEnvelope targetEnvelope,
        BusinessObjectDataReference reference,
        LedgerTransaction sourceTransaction,
        TransactionOutput sourceOutput,
        List<ValidationError> errors)
    {
        bool hasSourceEnvelope = BusinessObjectPayloadEnvelopeReader.TryRead(
            sourceTransaction.Data?.InlineJson,
            out BusinessObjectPayloadEnvelope sourceEnvelope,
            out bool hasInvalidReferences);
        if (hasInvalidReferences)
        {
            errors.Add(InvalidReference(
                $"BusinessObject source payload contains malformed references: {reference.ObjectId.Value}."));
            return;
        }

        // Historical BusinessObjects may carry an unstructured payload. Their type and schema
        // cannot be recovered here, so only structured source envelopes participate in this check.
        if (!hasSourceEnvelope)
        {
            return;
        }

        HashHex? sourceDataHash = sourceTransaction.Data?.DataHash;
        if (sourceDataHash is null || sourceOutput.BusinessObject!.DataHash != sourceDataHash.Value)
        {
            errors.Add(InvalidReference(
                $"BusinessObject source payload is not bound to the referenced output data hash: {reference.ObjectId.Value}."));
        }

        if (!string.Equals(sourceEnvelope.ObjectType, targetEnvelope.ObjectType, StringComparison.Ordinal)
            || !string.Equals(sourceEnvelope.SchemaVersion, targetEnvelope.SchemaVersion, StringComparison.Ordinal))
        {
            errors.Add(InvalidReference(
                $"BusinessObject source type and schema must match the target payload: {reference.ObjectId.Value}."));
        }
    }

    private static ValidationError InvalidReference(string message)
        => new(ValidationErrorCodes.InvalidBusinessObjectTransition, message);
}
