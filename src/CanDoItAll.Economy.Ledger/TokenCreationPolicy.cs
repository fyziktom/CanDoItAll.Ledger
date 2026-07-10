using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.Ledger;

public sealed class TokenCreationPolicyOptions
{
    public bool Enabled { get; init; } = true;

    public Address TreasuryAdminAddress { get; init; } = TokenCreationPolicyDefaults.TreasuryAdminAddress;
}

public static class TokenCreationPolicyDefaults
{
    public static Address TreasuryAdminAddress { get; } = Address.FromPublicKey(Hex.Utf8("treasury-admin-public"));
}

public interface ITokenCreationPolicy
{
    Task<ValidationResult> ValidateAsync(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs,
        ILedgerStore store,
        CancellationToken cancellationToken);
}

/// <summary>
/// Explicit opt-out for controlled simulations and compatibility fixtures.
/// Production composition must use <see cref="TokenCreationPolicy"/>.
/// </summary>
public sealed class DisabledTokenCreationPolicy : ITokenCreationPolicy
{
    public static DisabledTokenCreationPolicy Instance { get; } = new();

    private DisabledTokenCreationPolicy()
    {
    }

    public Task<ValidationResult> ValidateAsync(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs,
        ILedgerStore store,
        CancellationToken cancellationToken)
        => Task.FromResult(ValidationResult.Success);
}

public sealed class TokenCreationPolicy(
    TokenCreationPolicyOptions options,
    ITokenDefinitionHasher? providedTokenDefinitionHasher = null) : ITokenCreationPolicy
{
    private const int MaxOriginTraceDepth = 64;
    private readonly ITokenDefinitionHasher tokenDefinitionHasher = providedTokenDefinitionHasher
        ?? new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());

    public async Task<ValidationResult> ValidateAsync(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs,
        ILedgerStore store,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return ValidationResult.Success;
        }

        var errors = new List<ValidationError>();
        IReadOnlyList<UnspentOutput> inputOutputs = inputs.Values.ToArray();
        bool hasTreasuryInput = inputOutputs.Any(x => x.Address == options.TreasuryAdminAddress);
        bool hasTreasuryPermissionInput = inputOutputs.Any(x =>
            x.Address == options.TreasuryAdminAddress
            && PermissionTokenAuthority.IsPermissionToken(x.Token));

        IReadOnlyList<ResolvedPermissionInput> permissionInputs = await ResolvePermissionInputsAsync(
            transaction,
            inputOutputs,
            store,
            cancellationToken).ConfigureAwait(false);

        ValidateMainMint(transaction, hasTreasuryInput, errors);
        await ValidateMintedTokensAsync(
            transaction,
            permissionInputs,
            store,
            errors,
            cancellationToken).ConfigureAwait(false);
        ValidateRuleTokenOutputs(transaction, permissionInputs, errors);
        ValidatePermissionOutputs(
            transaction,
            permissionInputs,
            hasTreasuryPermissionInput,
            errors);

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static void ValidateMainMint(
        LedgerTransaction transaction,
        bool hasTreasuryInput,
        List<ValidationError> errors)
    {
        bool mintsMainAmount = transaction.Kind == TransactionKind.Mint
            && transaction.Outputs.Any(x => x.MainAmount.Value > 0m);
        if (mintsMainAmount && !hasTreasuryInput)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.UnauthorizedTokenCreation,
                "Minting main ledger amount requires a signed treasury input."));
        }
    }

    private async Task ValidateMintedTokensAsync(
        LedgerTransaction transaction,
        IReadOnlyList<ResolvedPermissionInput> permissionInputs,
        ILedgerStore store,
        List<ValidationError> errors,
        CancellationToken cancellationToken)
    {
        if (transaction.Kind != TransactionKind.Mint)
        {
            return;
        }

        var createdTokenIds = new HashSet<TokenId>();
        var permissionUses = new Dictionary<OutPointKey, PermissionUse>();

        foreach (TransactionOutput output in transaction.Outputs.Where(x => x.Token is not null))
        {
            TokenTransfer token = output.Token!;
            TokenDefinition? definition = transaction.TokenDefinition?.Definition;
            bool isDefinedV2Token = definition is not null
                && tokenDefinitionHasher.CalculateTokenId(definition) == token.TokenId;
            if (!isDefinedV2Token && !TokenCreationAuthority.IsValidNamespace(token.TokenId.Value.Trim()))
            {
                errors.Add(new ValidationError(ValidationErrorCodes.InvalidTokenId, $"Token id is invalid: {token.TokenId.Value}."));
                continue;
            }

            if (PermissionTokenAuthority.IsPermissionToken(token))
            {
                if (IsPreservedPermissionOutput(output, permissionInputs))
                {
                    continue;
                }

                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnauthorizedTokenCreation,
                    "Permission token supply can only be created by genesis treasury output."));
                continue;
            }

            if (RuleTokenAuthority.IsRuleToken(token))
            {
                continue;
            }

            bool tokenExists = await store
                .TokenExistsAsync(transaction.LedgerId, transaction.ScenarioId, token.TokenId, cancellationToken)
                .ConfigureAwait(false);
            if (tokenExists)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.DuplicateTokenType,
                    $"Token type already exists: {token.TokenId.Value}."));
                continue;
            }

            ResolvedPermissionInput? permission = permissionInputs.FirstOrDefault(input =>
                input.OriginatesFromTreasury
                && string.Equals(input.Grant.Action, PermissionTokenAuthority.MintNewTokenTypeAction, StringComparison.Ordinal)
                && (!isDefinedV2Token || input.Output.Address == definition!.Issuer)
                && PermissionTokenAuthority.IsTokenInNamespace(
                    isDefinedV2Token
                        ? new TokenId($"{definition!.Namespace}.{definition.Symbol}")
                        : token.TokenId,
                    input.Grant.Namespace));
            if (permission is null)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnauthorizedTokenCreation,
                    $"Creating token {token.TokenId.Value} requires a valid admin-origin permission input."));
                continue;
            }

            if (createdTokenIds.Add(token.TokenId))
            {
                TrackPermissionUse(permissionUses, permission);
            }

            if (permission.Grant.Repeatable && !ReturnsSamePermission(transaction, permission.Output))
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnauthorizedTokenCreation,
                    $"Repeatable permission {permission.Output.OutPoint} must be returned to {permission.Output.Address.Value}."));
            }
        }

        ValidateNonRepeatablePermissionReturns(transaction, permissionUses.Values, errors);
    }

    private static void ValidateRuleTokenOutputs(
        LedgerTransaction transaction,
        IReadOnlyList<ResolvedPermissionInput> permissionInputs,
        List<ValidationError> errors)
    {
        TransactionOutput[] ruleOutputs = transaction.Outputs
            .Where(output => RuleTokenAuthority.IsRuleToken(output.Token))
            .OrderBy(output => output.OutputIndex)
            .ToArray();
        if (ruleOutputs.Length == 0)
        {
            return;
        }

        if (transaction.Kind != TransactionKind.Mint)
        {
            errors.Add(RuleError("Rule tokens can only be created by a mint transaction using a rule-creation permission."));
        }

        if (ruleOutputs.Length != 1)
        {
            errors.Add(RuleError("A rule-token transaction must create exactly one rule token output."));
            return;
        }

        TransactionOutput ruleOutput = ruleOutputs[0];
        if (ruleOutput.OutputIndex != 0)
        {
            errors.Add(RuleError("Rule token output must be output 0."));
        }

        if (ruleOutput.Token?.Amount.Value != 1m)
        {
            errors.Add(RuleError("Rule token output amount must be exactly 1."));
        }

        if (ruleOutput.BusinessObject is null)
        {
            errors.Add(RuleError("Rule token output must carry a BusinessObject reference."));
            return;
        }

        if (!RuleTokenAuthority.TryParseData(transaction.Data, ruleOutput.BusinessObject, out AccountRuleDefinition rule))
        {
            errors.Add(RuleError("Rule token output must include valid rule data bound to output 0."));
            return;
        }

        ResolvedPermissionInput? source = permissionInputs.FirstOrDefault(input =>
            input.OriginatesFromTreasury
            && string.Equals(input.Grant.Action, RuleTokenAuthority.CreateRuleTokenAction, StringComparison.Ordinal)
            && PermissionTokenAuthority.IsTokenInNamespace(new TokenId(rule.RuleNamespace), input.Grant.Namespace)
            && input.Output.Token?.Amount.Value >= 1m);
        if (source is null)
        {
            errors.Add(RuleError($"Creating rule {rule.RuleId} requires a valid admin-origin rule-creation permission source."));
            return;
        }

        ValidateRulePermissionReturn(transaction, source, errors);
    }

    private static void ValidateRulePermissionReturn(
        LedgerTransaction transaction,
        ResolvedPermissionInput permission,
        List<ValidationError> errors)
    {
        decimal inputAmount = permission.Output.Token?.Amount.Value ?? 0m;
        decimal expectedReturn = inputAmount - 1m;
        TransactionOutput? outputOne = transaction.Outputs.FirstOrDefault(output => output.OutputIndex == 1);

        if (expectedReturn > 0m)
        {
            if (outputOne is null
                || !IsSamePermissionIdentity(outputOne, permission.Output)
                || outputOne.Token?.Amount.Value != expectedReturn)
            {
                errors.Add(RuleError(
                    $"Rule creation must return permission change at output 1 with amount {expectedReturn}."));
            }

            return;
        }

        decimal returnedAmount = SumReturnedSamePermissionAmount(transaction, permission.Output);
        if (returnedAmount > 0m)
        {
            errors.Add(RuleError("Rule creation with permission amount 1 must consume the permission source."));
        }
    }

    private void ValidatePermissionOutputs(
        LedgerTransaction transaction,
        IReadOnlyList<ResolvedPermissionInput> permissionInputs,
        bool hasTreasuryPermissionInput,
        List<ValidationError> errors)
    {
        if (transaction.Kind == TransactionKind.Genesis)
        {
            return;
        }

        foreach (TransactionOutput output in transaction.Outputs.Where(x => PermissionTokenAuthority.IsPermissionToken(x.Token)))
        {
            if (output.BusinessObject is null)
            {
                if (!hasTreasuryPermissionInput || output.Address != options.TreasuryAdminAddress)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCodes.UnauthorizedTokenAuthorityGrant,
                        "Permission token outputs without permission BO data are reserved for treasury admin change."));
                }

                continue;
            }

            if (IsPreservedPermissionOutput(output, permissionInputs))
            {
                continue;
            }

            if (!PermissionTokenAuthority.TryParseData(transaction.Data, output.BusinessObject, out PermissionGrant outputGrant))
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnauthorizedTokenAuthorityGrant,
                    $"Permission output {output.OutputIndex} must include valid permission data."));
                continue;
            }

            if (hasTreasuryPermissionInput)
            {
                continue;
            }

            ResolvedPermissionInput? source = permissionInputs.FirstOrDefault(input =>
                input.OriginatesFromTreasury
                && input.Output.Token?.Amount.Value > output.Token!.Amount.Value
                && CanDelegate(input.Grant, outputGrant));
            if (source is null)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnauthorizedTokenAuthorityGrant,
                    $"Permission output for namespace {outputGrant.Namespace} requires an admin-origin permission source with a matching or parent namespace."));
            }
        }
    }

    private async Task<IReadOnlyList<ResolvedPermissionInput>> ResolvePermissionInputsAsync(
        LedgerTransaction transaction,
        IReadOnlyList<UnspentOutput> inputOutputs,
        ILedgerStore store,
        CancellationToken cancellationToken)
    {
        var permissions = new List<ResolvedPermissionInput>();
        foreach (UnspentOutput output in inputOutputs.Where(output =>
            PermissionTokenAuthority.IsPermissionToken(output.Token)
            && output.BusinessObject is not null))
        {
            PermissionResolution? resolution = await ResolvePermissionAtOutPointAsync(
                transaction.LedgerId,
                transaction.ScenarioId,
                output.OutPoint,
                output.BusinessObject!.DataHash,
                store,
                new HashSet<string>(StringComparer.Ordinal),
                cancellationToken).ConfigureAwait(false);
            if (resolution is not null)
            {
                permissions.Add(new ResolvedPermissionInput(output, resolution.Grant, resolution.OriginatesFromTreasury));
            }
        }

        return permissions;
    }

    private async Task<PermissionResolution?> ResolvePermissionAtOutPointAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        OutPoint outPoint,
        HashHex dataHash,
        ILedgerStore store,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (visited.Count > MaxOriginTraceDepth || !visited.Add($"{outPoint.TxId.Value}:{outPoint.OutputIndex}:{dataHash.Value}"))
        {
            return null;
        }

        LedgerTransaction? transaction = await store
            .GetTransactionAsync(ledgerId, scenarioId, outPoint.TxId, cancellationToken)
            .ConfigureAwait(false);
        TransactionOutput? output = transaction?.Outputs.FirstOrDefault(x => x.OutputIndex == outPoint.OutputIndex);
        if (transaction is null
            || output is null
            || !PermissionTokenAuthority.IsPermissionToken(output.Token)
            || output.BusinessObject?.DataHash != dataHash)
        {
            return null;
        }

        if (PermissionTokenAuthority.TryParseData(transaction.Data, output.BusinessObject, out PermissionGrant grant))
        {
            bool origin = await HasTreasuryPermissionOriginAsync(
                ledgerId,
                scenarioId,
                transaction,
                store,
                visited,
                cancellationToken).ConfigureAwait(false);
            return new PermissionResolution(grant, origin);
        }

        foreach (TransactionInput input in transaction.Inputs)
        {
            PreviousOutput? previous = await GetPreviousOutputAsync(
                ledgerId,
                scenarioId,
                input.PreviousOutPoint,
                store,
                cancellationToken).ConfigureAwait(false);
            if (previous?.Output.BusinessObject?.DataHash != dataHash
                || !PermissionTokenAuthority.IsPermissionToken(previous.Output.Token))
            {
                continue;
            }

            PermissionResolution? inherited = await ResolvePermissionAtOutPointAsync(
                ledgerId,
                scenarioId,
                input.PreviousOutPoint,
                dataHash,
                store,
                visited,
                cancellationToken).ConfigureAwait(false);
            if (inherited is not null)
            {
                return inherited;
            }
        }

        return null;
    }

    private async Task<bool> HasTreasuryPermissionOriginAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        LedgerTransaction transaction,
        ILedgerStore store,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        foreach (TransactionInput input in transaction.Inputs)
        {
            PreviousOutput? previous = await GetPreviousOutputAsync(
                ledgerId,
                scenarioId,
                input.PreviousOutPoint,
                store,
                cancellationToken).ConfigureAwait(false);
            if (previous is null || !PermissionTokenAuthority.IsPermissionToken(previous.Output.Token))
            {
                continue;
            }

            if (input.Address == options.TreasuryAdminAddress)
            {
                return true;
            }

            if (previous.Output.BusinessObject is null)
            {
                continue;
            }

            PermissionResolution? inherited = await ResolvePermissionAtOutPointAsync(
                ledgerId,
                scenarioId,
                input.PreviousOutPoint,
                previous.Output.BusinessObject.DataHash,
                store,
                visited,
                cancellationToken).ConfigureAwait(false);
            if (inherited?.OriginatesFromTreasury == true)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<PreviousOutput?> GetPreviousOutputAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        OutPoint outPoint,
        ILedgerStore store,
        CancellationToken cancellationToken)
    {
        LedgerTransaction? transaction = await store
            .GetTransactionAsync(ledgerId, scenarioId, outPoint.TxId, cancellationToken)
            .ConfigureAwait(false);
        TransactionOutput? output = transaction?.Outputs.FirstOrDefault(x => x.OutputIndex == outPoint.OutputIndex);
        return transaction is null || output is null
            ? null
            : new PreviousOutput(transaction, output);
    }

    private static void TrackPermissionUse(
        Dictionary<OutPointKey, PermissionUse> permissionUses,
        ResolvedPermissionInput permission)
    {
        if (!permissionUses.TryGetValue(permission.Output.OutPointKey, out PermissionUse? use))
        {
            use = new PermissionUse(permission);
            permissionUses.Add(permission.Output.OutPointKey, use);
        }

        use.CreatedTokenTypeCount++;
    }

    private static void ValidateNonRepeatablePermissionReturns(
        LedgerTransaction transaction,
        IEnumerable<PermissionUse> permissionUses,
        List<ValidationError> errors)
    {
        foreach (PermissionUse use in permissionUses)
        {
            if (use.Permission.Grant.Repeatable)
            {
                continue;
            }

            decimal inputAmount = use.Permission.Output.Token?.Amount.Value ?? 0m;
            if (use.CreatedTokenTypeCount > inputAmount)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnauthorizedTokenCreation,
                    $"Non-repeatable permission {use.Permission.Output.OutPoint} only authorizes {inputAmount} created token type(s), requested {use.CreatedTokenTypeCount}."));
            }

            decimal maximumReturnedAmount = Math.Max(0m, inputAmount - use.CreatedTokenTypeCount);
            decimal returnedAmount = SumReturnedSamePermissionAmount(transaction, use.Permission.Output);
            if (returnedAmount > maximumReturnedAmount)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.UnauthorizedTokenCreation,
                    $"Non-repeatable permission {use.Permission.Output.OutPoint} must be consumed for {use.CreatedTokenTypeCount} created token type(s). Returned={returnedAmount}, MaximumAllowed={maximumReturnedAmount}."));
            }
        }
    }

    private static bool ReturnsSamePermission(LedgerTransaction transaction, UnspentOutput permission)
        => transaction.Outputs.Any(output =>
            IsSamePermissionIdentity(output, permission)
            && output.Token?.Amount == permission.Token?.Amount);

    private static decimal SumReturnedSamePermissionAmount(LedgerTransaction transaction, UnspentOutput permission)
        => transaction.Outputs
            .Where(output => IsSamePermissionIdentity(output, permission))
            .Aggregate(0m, (sum, output) => sum + output.Token!.Amount.Value);

    private static bool IsPreservedPermissionOutput(
        TransactionOutput output,
        IReadOnlyList<ResolvedPermissionInput> permissionInputs)
        => permissionInputs.Any(input =>
            input.OriginatesFromTreasury
            && IsSamePermissionIdentity(output, input.Output)
            && output.Token!.Amount.Value <= input.Output.Token!.Amount.Value);

    private static bool IsSamePermissionIdentity(TransactionOutput output, UnspentOutput permission)
    {
        if (output.Token is null
            || permission.Token is null
            || output.BusinessObject is null
            || permission.BusinessObject is null)
        {
            return false;
        }

        return output.Address == permission.Address
            && output.Token.TokenId == PermissionTokenAuthority.PermissionTokenId
            && permission.Token.TokenId == PermissionTokenAuthority.PermissionTokenId
            && output.BusinessObject.ObjectId == permission.BusinessObject.ObjectId
            && output.BusinessObject.DataHash == permission.BusinessObject.DataHash;
    }

    private static bool CanDelegate(PermissionGrant source, PermissionGrant delegated)
        => string.Equals(source.Action, delegated.Action, StringComparison.Ordinal)
            && PermissionTokenAuthority.IsTokenInNamespace(new TokenId(delegated.Namespace), source.Namespace)
            && (!delegated.Repeatable || source.Repeatable);

    private static ValidationError RuleError(string message)
        => new(ValidationErrorCodes.UnauthorizedRuleTokenCreation, message);

    private sealed record ResolvedPermissionInput(
        UnspentOutput Output,
        PermissionGrant Grant,
        bool OriginatesFromTreasury);

    private sealed class PermissionUse(ResolvedPermissionInput permission)
    {
        public ResolvedPermissionInput Permission { get; } = permission;

        public int CreatedTokenTypeCount { get; set; }
    }

    private sealed record PermissionResolution(
        PermissionGrant Grant,
        bool OriginatesFromTreasury);

    private sealed record PreviousOutput(
        LedgerTransaction Transaction,
        TransactionOutput Output);
}
