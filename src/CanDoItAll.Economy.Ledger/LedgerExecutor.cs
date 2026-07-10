using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class LedgerExecutor(
    ILedgerStore store,
    ITransactionHasher hasher,
    ISignaturePayloadBuilder payloadBuilder,
    ILedgerAdminSigner adminSigner,
    ITimeProvider timeProvider,
    ISignatureProviderRegistry signatureProviderRegistry,
    ITokenCreationPolicy? tokenCreationPolicy = null,
    IMainCurrencyIssuancePolicy? mainCurrencyIssuancePolicy = null,
    LedgerTransactionValidationPipeline? providedValidationPipeline = null)
{
    private readonly TransactionPreflightValidator preflightValidator = new();
    private readonly TransactionShapeValidator shapeValidator = new();
    private readonly LedgerTransactionValidationPipeline validationPipeline = providedValidationPipeline
        ?? new LedgerTransactionValidationPipeline(
            new InputReferenceValidator(),
            new BusinessObjectTransitionValidator(),
            mainCurrencyIssuancePolicy ?? new MainCurrencyIssuancePolicy(new MainCurrencyIssuancePolicyOptions()),
            tokenCreationPolicy ?? new TokenCreationPolicy(new TokenCreationPolicyOptions()),
            new InputSignatureValidator(payloadBuilder, signatureProviderRegistry),
            new AssetConservationValidator());

    public async Task<LedgerSubmitResult> SubmitAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
    {
        ValidationResult preflight = preflightValidator.Validate(transaction);
        if (!preflight.IsValid)
        {
            return LedgerSubmitResult.Rejected(preflight.Errors);
        }

        ValidationResult shape = shapeValidator.ValidateShape(transaction);
        if (!shape.IsValid)
        {
            return LedgerSubmitResult.Rejected(shape.Errors);
        }

        TxId txId = hasher.CalculateTxId(transaction);
        HashHex txHash = hasher.CalculateTxHash(transaction);
        LedgerTransaction normalized = transaction with { TxId = txId, TxHash = txHash };

        OutPointKey[] inputKeys = normalized.Inputs
            .Select(input => hasher.CalculateOutPointKey(
                normalized.LedgerId,
                normalized.ScenarioId,
                input.PreviousOutPoint,
                input.Address))
            .ToArray();

        if (store is ILedgerConfirmationStore confirmationStore)
        {
            return await confirmationStore
                .ConfirmTransactionAsync(
                    normalized,
                    inputKeys,
                    (context, token) => EvaluateConfirmationAsync(normalized, inputKeys, context, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        LedgerTransaction? existing = await store
            .GetTransactionAsync(normalized.LedgerId, normalized.ScenarioId, txId, cancellationToken)
            .ConfigureAwait(false);
        LedgerState state = await store
            .GetLedgerStateAsync(normalized.LedgerId, normalized.ScenarioId, cancellationToken)
            .ConfigureAwait(false)
            ?? CreateInitialState(normalized);
        IReadOnlyDictionary<OutPointKey, UnspentOutput> unspent = await store
            .GetUnspentOutputsAsync(normalized.LedgerId, normalized.ScenarioId, inputKeys, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<OutPointKey, SpentOutput> spent = await store
            .GetSpentOutputsAsync(normalized.LedgerId, normalized.ScenarioId, inputKeys, cancellationToken)
            .ConfigureAwait(false);

        ValidationResult validation = await validationPipeline
            .ValidateAsync(
                new LedgerTransactionValidationContext(
                    normalized,
                    state,
                    existing,
                    inputKeys,
                    unspent,
                    spent,
                    store),
                cancellationToken)
            .ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return LedgerSubmitResult.Rejected(validation.Errors);
        }

        ConfirmedTransaction confirmed = CreateConfirmedTransaction(normalized, state);
        try
        {
            await store.AppendConfirmedTransactionAsync(confirmed, cancellationToken).ConfigureAwait(false);
        }
        catch (LedgerStoreConflictException ex)
        {
            return LedgerSubmitResult.Rejected(new[] { new ValidationError(ex.Code, ex.Message) });
        }

        return LedgerSubmitResult.Confirmed(txId, confirmed.LedgerSequence);
    }

    private async Task<LedgerConfirmationDecision> EvaluateConfirmationAsync(
        LedgerTransaction normalized,
        IReadOnlyList<OutPointKey> inputKeys,
        LedgerConfirmationContext context,
        CancellationToken cancellationToken)
    {
        LedgerState state = context.State ?? CreateInitialState(normalized);
        ValidationResult validation = await validationPipeline
            .ValidateAsync(
                new LedgerTransactionValidationContext(
                    normalized,
                    state,
                    context.ExistingTransaction,
                    inputKeys,
                    context.UnspentOutputs,
                    context.SpentOutputs,
                    store),
                cancellationToken)
            .ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return LedgerConfirmationDecision.Rejected(validation.Errors);
        }

        return LedgerConfirmationDecision.Confirmed(CreateConfirmedTransaction(normalized, state));
    }

    private LedgerState CreateInitialState(LedgerTransaction transaction)
        => new(
            transaction.LedgerId,
            transaction.ScenarioId,
            0,
            new HashHex(new string('0', 64)),
            timeProvider.UtcNow);

    private ConfirmedTransaction CreateConfirmedTransaction(LedgerTransaction normalized, LedgerState state)
    {
        DateTimeOffset confirmedAt = timeProvider.UtcNow;
        long sequence = checked(state.CurrentSequence + 1);
        var temporary = new ConfirmedTransaction(
            normalized with { Status = TransactionStatus.Confirmed },
            sequence,
            state.CurrentLedgerHash,
            new HashHex(new string('0', 64)),
            SignatureBundle.Empty,
            confirmedAt);

        HashHex entryHash = payloadBuilder.BuildLedgerEntryPayloadHash(temporary);
        SignatureBundle adminSignature = adminSigner.Sign(entryHash);
        return temporary with { LedgerEntryHash = entryHash, AdminSignature = adminSignature };
    }
}
