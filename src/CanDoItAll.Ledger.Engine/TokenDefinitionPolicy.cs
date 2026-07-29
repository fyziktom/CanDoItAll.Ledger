using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;

namespace CanDoItAll.Ledger.Engine;

public interface ITokenDefinitionPolicy
{
    Task<ValidationResult> ValidateAsync(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs,
        ILedgerStore store,
        CancellationToken cancellationToken);
}

public sealed class TokenDefinitionPolicy(ITokenDefinitionHasher? providedHasher = null) : ITokenDefinitionPolicy
{
    private const int MaxDisplayNameLength = 160;
    private const int MaxDescriptionLength = 2048;
    private readonly ITokenDefinitionHasher hasher = providedHasher
        ?? new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());

    public async Task<ValidationResult> ValidateAsync(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs,
        ILedgerStore store,
        CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();
        TokenDefinitionEnvelope? envelope = transaction.TokenDefinition;
        TokenId? newlyDefinedId = null;
        HashSet<TokenId> inputTokenIds = inputs.Values
            .Where(input => input.Token is not null)
            .Select(input => input.Token!.TokenId)
            .ToHashSet();
        if (envelope is not null)
        {
            ValidateEnvelope(transaction, envelope, inputs, errors, out newlyDefinedId);
        }

        foreach (TransactionOutput output in transaction.Outputs.Where(item => item.Token is not null))
        {
            TokenTransfer token = output.Token!;
            RegisteredTokenDefinition? registered = null;
            if (newlyDefinedId == token.TokenId && envelope is not null)
            {
                registered = FromEnvelope(envelope, token.TokenId, transaction, output.OutputIndex, hasher);
            }
            else
            {
                registered = await store
                    .GetTokenDefinitionAsync(
                        transaction.LedgerId,
                        transaction.ScenarioId,
                        token.TokenId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (registered is null
                && transaction.Version == TokenDefinitionIdentity.CurrentProtocolVersion
                && transaction.Kind == TransactionKind.Mint
                && !inputTokenIds.Contains(token.TokenId)
                && !PermissionTokenAuthority.IsPermissionToken(token)
                && !RuleTokenAuthority.IsRuleToken(token)
                && !await store.TokenExistsAsync(
                    transaction.LedgerId,
                    transaction.ScenarioId,
                    token.TokenId,
                    cancellationToken).ConfigureAwait(false))
            {
                errors.Add(DefinitionError(
                    $"Protocol-v2 Mint cannot create token {token.TokenId.Value} without its matching immutable definition."));
            }

            if (registered is null || registered.Classification == TokenClassification.LegacyUnspecified)
            {
                continue;
            }

            ValidateClassifiedOutput(output, registered, errors);
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private void ValidateEnvelope(
        LedgerTransaction transaction,
        TokenDefinitionEnvelope envelope,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs,
        List<ValidationError> errors,
        out TokenId? tokenId)
    {
        TokenDefinition definition = envelope.Definition;
        tokenId = null;
        if (transaction.Version != TokenDefinitionIdentity.CurrentProtocolVersion
            || definition.ProtocolVersion != TokenDefinitionIdentity.CurrentProtocolVersion
            || transaction.Kind != TransactionKind.Mint)
        {
            errors.Add(DefinitionError(
                "A token definition is allowed only on a protocol-v2 Mint transaction and must declare protocol version 2."));
        }

        if (!TokenCreationAuthority.IsValidNamespace(definition.Namespace)
            || !string.Equals(definition.Namespace, definition.Namespace.Trim(), StringComparison.Ordinal))
        {
            errors.Add(DefinitionError("Token definition namespace must be canonical non-empty dot-separated text."));
        }

        if (!LedgerValueValidation.IsValidIdentifier(definition.Symbol, 64)
            || definition.Symbol.Contains('.', StringComparison.Ordinal))
        {
            errors.Add(DefinitionError("Token definition symbol must be a canonical identifier of at most 64 characters without dots."));
        }

        if (!Address.IsValid(definition.Issuer.Value))
        {
            errors.Add(DefinitionError("Token definition issuer must be a valid derived address."));
        }

        if (definition.InitialSupply.Value <= 0m
            || definition.DecimalPlaces is < 0 or > 28
            || GetDecimalScale(definition.InitialSupply.Value) > definition.DecimalPlaces)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTokenInitialSupply,
                "Token initial supply must be positive and fit the declared decimal places."));
        }

        if (!Enum.IsDefined(definition.Classification)
            || definition.Classification == TokenClassification.LegacyUnspecified
            || definition.Classification == TokenClassification.BusinessObjectCarrier && definition.DecimalPlaces != 0)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTokenClassification,
                "Protocol-v2 token classification must be Fungible or BusinessObjectCarrier; BO carriers require zero decimal places."));
        }

        ValidateDisplayMetadata(envelope.DisplayMetadata, errors);

        TokenId expectedId = hasher.CalculateTokenId(definition);
        tokenId = expectedId;
        if (!transaction.Outputs.Any(output => output.Token?.TokenId == expectedId))
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTokenDefinitionId,
                $"Protocol-v2 Mint must emit the hash-derived token id {expectedId.Value}."));
        }

        decimal outputSupply;
        try
        {
            outputSupply = transaction.Outputs
                .Where(output => output.Token?.TokenId == expectedId)
                .Aggregate(0m, (sum, output) => checked(sum + output.Token!.Amount.Value));
        }
        catch (OverflowException)
        {
            outputSupply = 0m;
            errors.Add(new ValidationError(ValidationErrorCodes.AmountOverflow, "Protocol-v2 initial supply aggregation overflowed."));
        }

        if (outputSupply != definition.InitialSupply.Value)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTokenInitialSupply,
                $"Protocol-v2 output supply {outputSupply} does not match declared initial supply {definition.InitialSupply.Value}."));
        }

        bool issuerConsumesInput = transaction.Inputs.Any(input => input.Address == definition.Issuer)
            && inputs.Values.Any(input => input.Address == definition.Issuer);
        if (!issuerConsumesInput)
        {
            errors.Add(DefinitionError(
                $"Protocol-v2 issuer {definition.Issuer.Value} must own and sign a consumed authorization input."));
        }
    }

    private static void ValidateDisplayMetadata(TokenDisplayMetadata? metadata, List<ValidationError> errors)
    {
        if (metadata is null)
        {
            return;
        }

        if (!LedgerValueValidation.IsSafeText(metadata.DisplayName, MaxDisplayNameLength, allowEmpty: true)
            || !LedgerValueValidation.IsSafeText(metadata.Description, MaxDescriptionLength, allowEmpty: true)
            || !LedgerValueValidation.IsSafeText(metadata.ExternalRef, LedgerValidationLimits.MaxExternalRefLength, allowEmpty: true))
        {
            errors.Add(DefinitionError("Token display metadata contains unsafe, non-canonical, or oversized text."));
        }
    }

    private static void ValidateClassifiedOutput(
        TransactionOutput output,
        RegisteredTokenDefinition definition,
        List<ValidationError> errors)
    {
        TokenTransfer token = output.Token!;
        if (definition.DecimalPlaces is { } decimals && GetDecimalScale(token.Amount.Value) > decimals)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTokenClassification,
                $"Output {output.OutputIndex} amount exceeds token {token.TokenId.Value} divisibility of {decimals} decimal places."));
        }

        switch (definition.Classification)
        {
            case TokenClassification.Fungible when output.BusinessObject is not null:
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidTokenClassification,
                    $"Fungible token output {output.OutputIndex} cannot carry a BusinessObject."));
                break;
            case TokenClassification.BusinessObjectCarrier when token.Amount.Value == 1m && output.BusinessObject is null:
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidTokenClassification,
                    $"BusinessObject-carrier token output {output.OutputIndex} with amount 1 must carry a unique BusinessObject."));
                break;
            case TokenClassification.BusinessObjectCarrier when token.Amount.Value > 1m && output.BusinessObject is not null:
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidTokenClassification,
                    $"BusinessObject-carrier source output {output.OutputIndex} with amount greater than 1 cannot carry a BusinessObject."));
                break;
            case TokenClassification.BusinessObjectCarrier when token.Amount.Value < 1m:
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidTokenClassification,
                    $"BusinessObject-carrier output {output.OutputIndex} must use an integral source amount or the unique amount 1."));
                break;
        }
    }

    private static RegisteredTokenDefinition FromEnvelope(
        TokenDefinitionEnvelope envelope,
        TokenId tokenId,
        LedgerTransaction transaction,
        int outputIndex,
        ITokenDefinitionHasher hasher)
        => new(
            tokenId,
            envelope.Definition.ProtocolVersion,
            envelope.Definition.Namespace,
            envelope.Definition.Symbol,
            envelope.Definition.Issuer,
            envelope.Definition.InitialSupply,
            envelope.Definition.DecimalPlaces,
            envelope.Definition.Classification,
            hasher.CalculateDefinitionHash(envelope.Definition),
            envelope.DisplayMetadata,
            transaction.TxId ?? new TxId(string.Empty),
            outputIndex,
            transaction.Timestamp);

    private static int GetDecimalScale(decimal value)
        => (decimal.GetBits(value)[3] >> 16) & 0x7F;

    private static ValidationError DefinitionError(string message)
        => new(ValidationErrorCodes.InvalidTokenDefinition, message);
}
