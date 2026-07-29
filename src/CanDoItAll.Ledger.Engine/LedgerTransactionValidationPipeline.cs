using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

public sealed record LedgerTransactionValidationContext(
    LedgerTransaction Transaction,
    LedgerState State,
    LedgerTransaction? ExistingTransaction,
    IReadOnlyList<OutPointKey> InputKeys,
    IReadOnlyDictionary<OutPointKey, UnspentOutput> UnspentOutputs,
    IReadOnlyDictionary<OutPointKey, SpentOutput> SpentOutputs,
    ILedgerStore Store);

public sealed class LedgerTransactionValidationPipeline(
    InputReferenceValidator inputReferenceValidator,
    BusinessObjectTransitionValidator businessObjectTransitionValidator,
    IMainCurrencyIssuancePolicy mainCurrencyIssuancePolicy,
    ITokenCreationPolicy tokenCreationPolicy,
    InputSignatureValidator inputSignatureValidator,
    AssetConservationValidator assetConservationValidator,
    ITokenDefinitionPolicy? providedTokenDefinitionPolicy = null,
    BusinessObjectReferenceValidator? providedBusinessObjectReferenceValidator = null)
{
    private readonly ITokenDefinitionPolicy tokenDefinitionPolicy = providedTokenDefinitionPolicy ?? new TokenDefinitionPolicy();
    private readonly BusinessObjectReferenceValidator businessObjectReferenceValidator =
        providedBusinessObjectReferenceValidator ?? new BusinessObjectReferenceValidator();

    public async Task<ValidationResult> ValidateAsync(
        LedgerTransactionValidationContext context,
        CancellationToken cancellationToken)
    {
        LedgerTransaction transaction = context.Transaction;
        if (context.ExistingTransaction is not null)
        {
            return ValidationResult.Failure(new ValidationError(
                ValidationErrorCodes.DuplicateTransaction,
                $"Transaction already exists: {transaction.TxId?.Value ?? "<missing>"}."));
        }

        ValidationResult genesis = ValidateGenesisSequence(transaction, context.State);
        if (!genesis.IsValid)
        {
            return genesis;
        }

        ValidationResult references = inputReferenceValidator.Validate(
            transaction,
            context.InputKeys,
            context.UnspentOutputs,
            context.SpentOutputs);
        if (!references.IsValid)
        {
            return references;
        }

        ValidationResult businessObjects = businessObjectTransitionValidator.Validate(
            transaction,
            context.InputKeys,
            context.UnspentOutputs);
        if (!businessObjects.IsValid)
        {
            return businessObjects;
        }

        ValidationResult businessObjectReferences = await businessObjectReferenceValidator
            .ValidateAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (!businessObjectReferences.IsValid)
        {
            return businessObjectReferences;
        }

        ValidationResult mainCurrencyIssuance = mainCurrencyIssuancePolicy.Validate(
            transaction,
            context.UnspentOutputs);
        if (!mainCurrencyIssuance.IsValid)
        {
            return mainCurrencyIssuance;
        }

        ValidationResult tokenDefinition = await tokenDefinitionPolicy
            .ValidateAsync(transaction, context.UnspentOutputs, context.Store, cancellationToken)
            .ConfigureAwait(false);
        if (!tokenDefinition.IsValid)
        {
            return tokenDefinition;
        }

        ValidationResult tokenPolicy = await tokenCreationPolicy
            .ValidateAsync(transaction, context.UnspentOutputs, context.Store, cancellationToken)
            .ConfigureAwait(false);
        if (!tokenPolicy.IsValid)
        {
            return tokenPolicy;
        }

        ValidationResult signatures = inputSignatureValidator.Validate(transaction);
        if (!signatures.IsValid)
        {
            return signatures;
        }

        return assetConservationValidator.ValidateTransfer(transaction, context.UnspentOutputs);
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
}
