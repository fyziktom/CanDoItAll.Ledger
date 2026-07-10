using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class MainCurrencyIssuancePolicyOptions
{
    public Address? TreasuryAdminAddress { get; init; }
}

public interface IMainCurrencyIssuancePolicy
{
    ValidationResult Validate(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs);
}

/// <summary>
/// Explicit opt-out for controlled in-memory simulations that model their own main-currency source.
/// Production composition must use <see cref="MainCurrencyIssuancePolicy"/>.
/// </summary>
public sealed class DisabledMainCurrencyIssuancePolicy : IMainCurrencyIssuancePolicy
{
    public static DisabledMainCurrencyIssuancePolicy Instance { get; } = new();

    private DisabledMainCurrencyIssuancePolicy()
    {
    }

    public ValidationResult Validate(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
        => ValidationResult.Success;
}

public sealed class MainCurrencyIssuancePolicy(MainCurrencyIssuancePolicyOptions options)
    : IMainCurrencyIssuancePolicy
{
    public ValidationResult Validate(
        LedgerTransaction transaction,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs)
    {
        if (transaction.Kind != TransactionKind.Mint)
        {
            return ValidationResult.Success;
        }

        if (!TrySum(inputs.Values.Select(output => output.MainAmount.Value), out decimal inputMain)
            || !TrySum(transaction.Outputs.Select(output => output.MainAmount.Value), out decimal outputMain))
        {
            return ValidationResult.Failure(new ValidationError(
                ValidationErrorCodes.AmountOverflow,
                "Main-currency issuance amount aggregation overflowed."));
        }

        if (outputMain <= inputMain)
        {
            return ValidationResult.Success;
        }

        if (options.TreasuryAdminAddress is not Address treasuryAddress)
        {
            return Unauthorized("Main-currency issuance is disabled because no treasury admin address is configured.");
        }

        bool allInputsBelongToTreasury = inputs.Count > 0
            && inputs.Values.All(output => output.Address == treasuryAddress);
        bool consumesPlainTreasuryAuthority = inputs.Values.Any(output =>
            output.Address == treasuryAddress
            && output.MainAmount.Value > 0m
            && output.Token is null
            && output.BusinessObject is null);
        if (!allInputsBelongToTreasury || !consumesPlainTreasuryAuthority)
        {
            return Unauthorized(
                $"Mint may increase main currency only by consuming a plain authority output owned by treasury {treasuryAddress.Value}.");
        }

        return ValidationResult.Success;
    }

    private static bool TrySum(IEnumerable<decimal> amounts, out decimal total)
    {
        total = 0m;
        try
        {
            foreach (decimal amount in amounts)
            {
                total = checked(total + amount);
            }

            return true;
        }
        catch (OverflowException)
        {
            total = 0m;
            return false;
        }
    }

    private static ValidationResult Unauthorized(string message)
        => ValidationResult.Failure(new ValidationError(
            ValidationErrorCodes.UnauthorizedMainCurrencyIssuance,
            message));
}
