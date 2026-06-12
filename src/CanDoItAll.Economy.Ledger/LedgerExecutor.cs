using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class LedgerExecutor(
    ILedgerStore store,
    ITransactionHasher hasher,
    ISignaturePayloadBuilder payloadBuilder,
    ILedgerAdminSigner adminSigner,
    ITimeProvider timeProvider,
    ISignatureProviderRegistry signatureProviderRegistry,
    ITokenCreationPolicy? tokenCreationPolicy = null)
{
    private readonly TransactionShapeValidator shapeValidator = new();
    private readonly AssetConservationValidator conservationValidator = new();
    private readonly BusinessObjectTransitionValidator businessObjectTransitionValidator = new();

    public async Task<LedgerSubmitResult> SubmitAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
    {
        var txId = hasher.CalculateTxId(transaction);
        var txHash = hasher.CalculateTxHash(transaction);
        var normalized = transaction with { TxId = txId, TxHash = txHash };

        var shape = shapeValidator.ValidateShape(normalized);
        if (!shape.IsValid)
        {
            return LedgerSubmitResult.Rejected(shape.Errors);
        }

        var inputKeys = normalized.Inputs
            .Select(x => hasher.CalculateOutPointKey(normalized.LedgerId, normalized.ScenarioId, x.PreviousOutPoint, x.Address))
            .ToArray();

        if (store is ILedgerConfirmationStore confirmationStore)
        {
            return await confirmationStore
                .ConfirmTransactionAsync(
                    normalized,
                    inputKeys,
                    (context, token) => EvaluateConfirmationAsync(normalized, txId, inputKeys, context, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        LedgerTransaction? existing = await store.GetTransactionAsync(normalized.LedgerId, normalized.ScenarioId, txId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return LedgerSubmitResult.Rejected(new[]
            {
                new ValidationError(ValidationErrorCodes.DuplicateTransaction, $"Transaction already exists: {txId.Value}.")
            });
        }

        var state = await store.GetLedgerStateAsync(normalized.LedgerId, normalized.ScenarioId, cancellationToken).ConfigureAwait(false)
            ?? new LedgerState(normalized.LedgerId, normalized.ScenarioId, 0, new HashHex(new string('0', 64)), timeProvider.UtcNow);

        ValidationResult genesisSequence = ValidateGenesisSequence(normalized, state);
        if (!genesisSequence.IsValid)
        {
            return LedgerSubmitResult.Rejected(genesisSequence.Errors);
        }

        var unspent = await store.GetUnspentOutputsAsync(normalized.LedgerId, normalized.ScenarioId, inputKeys, cancellationToken).ConfigureAwait(false);
        var spent = await store.GetSpentOutputsAsync(normalized.LedgerId, normalized.ScenarioId, inputKeys, cancellationToken).ConfigureAwait(false);

        var referenceErrors = ValidateInputReferences(normalized, inputKeys, unspent, spent);
        if (referenceErrors.Count > 0)
        {
            return LedgerSubmitResult.Rejected(referenceErrors);
        }

        ValidationResult businessObjectTransitions = businessObjectTransitionValidator.Validate(normalized, inputKeys, unspent);
        if (!businessObjectTransitions.IsValid)
        {
            return LedgerSubmitResult.Rejected(businessObjectTransitions.Errors);
        }

        if (tokenCreationPolicy is not null)
        {
            ValidationResult tokenPolicy = await tokenCreationPolicy
                .ValidateAsync(normalized, unspent, store, cancellationToken)
                .ConfigureAwait(false);
            if (!tokenPolicy.IsValid)
            {
                return LedgerSubmitResult.Rejected(tokenPolicy.Errors);
            }
        }

        ValidationResult signatures = ValidateInputSignatures(normalized);
        if (!signatures.IsValid)
        {
            return LedgerSubmitResult.Rejected(signatures.Errors);
        }

        var conservation = conservationValidator.ValidateTransfer(normalized, unspent);
        if (!conservation.IsValid)
        {
            return LedgerSubmitResult.Rejected(conservation.Errors);
        }

        var confirmedAt = timeProvider.UtcNow;
        var sequence = state.CurrentSequence + 1;
        var temporary = new ConfirmedTransaction(
            normalized with { Status = TransactionStatus.Confirmed },
            sequence,
            state.CurrentLedgerHash,
            new HashHex(new string('0', 64)),
            SignatureBundle.Empty,
            confirmedAt);

        var entryHash = payloadBuilder.BuildLedgerEntryPayloadHash(temporary);
        var adminSignature = adminSigner.Sign(entryHash);
        var confirmed = temporary with { LedgerEntryHash = entryHash, AdminSignature = adminSignature };

        try
        {
            await store.AppendConfirmedTransactionAsync(confirmed, cancellationToken).ConfigureAwait(false);
        }
        catch (LedgerStoreConflictException ex)
        {
            return LedgerSubmitResult.Rejected(new[]
            {
                new ValidationError(ex.Code, ex.Message)
            });
        }

        return LedgerSubmitResult.Confirmed(txId, sequence);
    }

    private async Task<LedgerConfirmationDecision> EvaluateConfirmationAsync(
        LedgerTransaction normalized,
        TxId txId,
        IReadOnlyList<OutPointKey> inputKeys,
        LedgerConfirmationContext context,
        CancellationToken cancellationToken)
    {
        if (context.ExistingTransaction is not null)
        {
            return LedgerConfirmationDecision.Rejected(new[]
            {
                new ValidationError(ValidationErrorCodes.DuplicateTransaction, $"Transaction already exists: {txId.Value}.")
            });
        }

        LedgerState state = context.State
            ?? new LedgerState(normalized.LedgerId, normalized.ScenarioId, 0, new HashHex(new string('0', 64)), timeProvider.UtcNow);

        ValidationResult genesisSequence = ValidateGenesisSequence(normalized, state);
        if (!genesisSequence.IsValid)
        {
            return LedgerConfirmationDecision.Rejected(genesisSequence.Errors);
        }

        var referenceErrors = ValidateInputReferences(normalized, inputKeys, context.UnspentOutputs, context.SpentOutputs);
        if (referenceErrors.Count > 0)
        {
            return LedgerConfirmationDecision.Rejected(referenceErrors);
        }

        ValidationResult businessObjectTransitions = businessObjectTransitionValidator.Validate(normalized, inputKeys, context.UnspentOutputs);
        if (!businessObjectTransitions.IsValid)
        {
            return LedgerConfirmationDecision.Rejected(businessObjectTransitions.Errors);
        }

        if (tokenCreationPolicy is not null)
        {
            ValidationResult tokenPolicy = await tokenCreationPolicy
                .ValidateAsync(normalized, context.UnspentOutputs, store, cancellationToken)
                .ConfigureAwait(false);
            if (!tokenPolicy.IsValid)
            {
                return LedgerConfirmationDecision.Rejected(tokenPolicy.Errors);
            }
        }

        ValidationResult signatures = ValidateInputSignatures(normalized);
        if (!signatures.IsValid)
        {
            return LedgerConfirmationDecision.Rejected(signatures.Errors);
        }

        var conservation = conservationValidator.ValidateTransfer(normalized, context.UnspentOutputs);
        if (!conservation.IsValid)
        {
            return LedgerConfirmationDecision.Rejected(conservation.Errors);
        }

        var confirmedAt = timeProvider.UtcNow;
        var sequence = state.CurrentSequence + 1;
        var temporary = new ConfirmedTransaction(
            normalized with { Status = TransactionStatus.Confirmed },
            sequence,
            state.CurrentLedgerHash,
            new HashHex(new string('0', 64)),
            SignatureBundle.Empty,
            confirmedAt);

        var entryHash = payloadBuilder.BuildLedgerEntryPayloadHash(temporary);
        var adminSignature = adminSigner.Sign(entryHash);
        return LedgerConfirmationDecision.Confirmed(temporary with { LedgerEntryHash = entryHash, AdminSignature = adminSignature });
    }

    private ValidationResult ValidateInputSignatures(LedgerTransaction transaction)
    {
        if (transaction.Inputs.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<ValidationError>();

        foreach (TransactionInput input in transaction.Inputs.OrderBy(x => x.InputIndex))
        {
            HashHex expectedPayloadHash = transaction.SigningMode == SigningMode.Ordered
                ? payloadBuilder.BuildOrderedInputPayloadHash(transaction, input)
                : payloadBuilder.BuildIndependentInputPayloadHash(transaction, input);

            if (input.SignaturePayloadHash != expectedPayloadHash)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidSignature,
                    $"Input {input.InputIndex} signature payload hash is missing or does not match the transaction payload."));
            }

            if (input.SignatureBundle.Signatures.Count == 0)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidSignature,
                    $"Input {input.InputIndex} must contain at least one signature."));
                continue;
            }

            foreach (SignatureEnvelope signature in input.SignatureBundle.Signatures)
            {
                ValidateSignatureEnvelope(input, expectedPayloadHash, signature, errors);
            }
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private void ValidateSignatureEnvelope(
        TransactionInput input,
        HashHex expectedPayloadHash,
        SignatureEnvelope signature,
        List<ValidationError> errors)
    {
        if (signature.PayloadHash != expectedPayloadHash)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidSignature,
                $"Input {input.InputIndex} signature payload hash does not match the expected payload."));
            return;
        }

        if (signature.PublicKeyMaterial is not { Length: > 0 } publicKeyMaterial)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidSignature,
                $"Input {input.InputIndex} signature is missing public key material."));
            return;
        }

        if (!input.Address.MatchesPublicKey(publicKeyMaterial))
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidSignature,
                $"Input {input.InputIndex} signature public key does not match the input address."));
            return;
        }

        if (Address.CalculatePublicKeyHash(publicKeyMaterial) != signature.PublicKeyHash)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidSignature,
                $"Input {input.InputIndex} signature public key hash does not match the public key material."));
            return;
        }

        try
        {
            ISignatureProvider provider = signatureProviderRegistry.Get(signature.Algorithm);
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(expectedPayloadHash.Value);
            if (!provider.Verify(payload, signature.SignatureBytes, publicKeyMaterial))
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidSignature,
                    $"Input {input.InputIndex} signature failed verification."));
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException or InvalidOperationException or ArgumentException)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidSignature,
                $"Input {input.InputIndex} signature could not be verified: {ex.Message}"));
        }
    }

    private static ValidationResult ValidateGenesisSequence(LedgerTransaction transaction, LedgerState state)
    {
        if (state.CurrentSequence == 0 && transaction.Kind != TransactionKind.Genesis)
        {
            return ValidationResult.Failure(new ValidationError(
                ValidationErrorCodes.InvalidGenesisSequence,
                "The first transaction for a ledger state must be a genesis transaction."));
        }

        if (state.CurrentSequence > 0 && transaction.Kind == TransactionKind.Genesis)
        {
            return ValidationResult.Failure(new ValidationError(
                ValidationErrorCodes.InvalidGenesisSequence,
                "A genesis transaction already exists for this ledger state."));
        }

        return ValidationResult.Success;
    }

    private static List<ValidationError> ValidateInputReferences(
        LedgerTransaction transaction,
        IReadOnlyList<OutPointKey> inputKeys,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> unspent,
        IReadOnlyDictionary<OutPointKey, SpentOutput> spent)
    {
        var errors = new List<ValidationError>();

        for (var i = 0; i < transaction.Inputs.Count; i++)
        {
            var input = transaction.Inputs[i];
            var key = inputKeys[i];

            if (spent.ContainsKey(key))
            {
                errors.Add(new ValidationError(ValidationErrorCodes.SpentInput, $"Input is already spent: {input.PreviousOutPoint}."));
                continue;
            }

            if (!unspent.TryGetValue(key, out var previousOutput))
            {
                errors.Add(new ValidationError(ValidationErrorCodes.UnknownInput, $"Input is unknown: {input.PreviousOutPoint}."));
                continue;
            }

            if (previousOutput.Address != input.Address)
            {
                errors.Add(new ValidationError(ValidationErrorCodes.AddressMismatch, $"Input address does not match previous output: {input.PreviousOutPoint}."));
            }
        }

        return errors;
    }
}
