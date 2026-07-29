using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

public sealed class LedgerV1SemanticRuleSet(
    ITransactionHasher hasher,
    LedgerTransactionValidationPipeline validationPipeline)
    : LedgerSemanticRuleSetBase(1, hasher, validationPipeline);

public sealed class LedgerV2SemanticRuleSet(
    ITransactionHasher hasher,
    LedgerTransactionValidationPipeline validationPipeline)
    : LedgerSemanticRuleSetBase(2, hasher, validationPipeline);

public abstract class LedgerSemanticRuleSetBase(
    int transactionVersion,
    ITransactionHasher hasher,
    LedgerTransactionValidationPipeline validationPipeline) : ILedgerSemanticRuleSet
{
    private readonly TransactionPreflightValidator preflight = new();
    private readonly TransactionShapeValidator shape = new();

    public int TransactionVersion { get; } = transactionVersion;

    public async Task<ValidationResult> ValidateAsync(
        ConfirmedTransaction confirmed,
        ILedgerStore replayStore,
        CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = confirmed.Transaction;
        if (transaction.Version != TransactionVersion)
        {
            return ValidationResult.Failure(new ValidationError(
                "semantic_rule_version_mismatch",
                $"V{TransactionVersion} semantic rules cannot validate transaction version {transaction.Version}."));
        }

        ValidationResult preflightResult = preflight.Validate(transaction);
        if (!preflightResult.IsValid)
        {
            return preflightResult;
        }

        ValidationResult shapeResult = shape.ValidateShape(transaction);
        if (!shapeResult.IsValid)
        {
            return shapeResult;
        }

        if (transaction.TxId is not { } txId)
        {
            return ValidationResult.Failure(new ValidationError(
                ValidationErrorCodes.InvalidIdentifier,
                "Semantic replay requires a persisted transaction id."));
        }

        OutPointKey[] inputKeys = transaction.Inputs.Select(input => hasher.CalculateOutPointKey(
            transaction.LedgerId,
            transaction.ScenarioId,
            input.PreviousOutPoint,
            input.Address)).ToArray();

        LedgerState state = await replayStore
            .GetLedgerStateAsync(transaction.LedgerId, transaction.ScenarioId, cancellationToken)
            .ConfigureAwait(false)
            ?? new LedgerState(
                transaction.LedgerId,
                transaction.ScenarioId,
                0,
                new HashHex(new string('0', 64)),
                confirmed.ConfirmedAt);
        LedgerTransaction? existing = await replayStore
            .GetTransactionAsync(transaction.LedgerId, transaction.ScenarioId, txId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<OutPointKey, UnspentOutput> unspent = await replayStore
            .GetUnspentOutputsAsync(transaction.LedgerId, transaction.ScenarioId, inputKeys, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<OutPointKey, SpentOutput> spent = await replayStore
            .GetSpentOutputsAsync(transaction.LedgerId, transaction.ScenarioId, inputKeys, cancellationToken)
            .ConfigureAwait(false);

        return await validationPipeline.ValidateAsync(
                new LedgerTransactionValidationContext(
                    transaction,
                    state,
                    existing,
                    inputKeys,
                    unspent,
                    spent,
                    replayStore),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
