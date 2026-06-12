using System.Globalization;
using NCalc;

namespace CanDoItAll.Economy.BusinessObjects;

public interface IAccountRuleExpressionEvaluator
{
    AccountRuleBooleanEvaluation EvaluateBoolean(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables);

    AccountRuleNumberEvaluation EvaluateNumber(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables);
}

public sealed class NCalcAccountRuleExpressionEvaluator : IAccountRuleExpressionEvaluator
{
    public AccountRuleBooleanEvaluation EvaluateBoolean(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
    {
        AccountRuleEvaluation evaluation = Evaluate(expressionText, variables);
        if (!evaluation.Succeeded)
        {
            return AccountRuleBooleanEvaluation.Failed(evaluation.Error ?? "Expression evaluation failed.");
        }

        if (TryConvertBoolean(evaluation.Value, out bool value))
        {
            return AccountRuleBooleanEvaluation.Success(value);
        }

        return AccountRuleBooleanEvaluation.Failed($"Expression '{expressionText}' did not evaluate to a boolean value.");
    }

    public AccountRuleNumberEvaluation EvaluateNumber(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
    {
        AccountRuleEvaluation evaluation = Evaluate(expressionText, variables);
        if (!evaluation.Succeeded)
        {
            return AccountRuleNumberEvaluation.Failed(evaluation.Error ?? "Expression evaluation failed.");
        }

        if (TryConvertDecimal(evaluation.Value, out decimal value))
        {
            return AccountRuleNumberEvaluation.Success(value);
        }

        return AccountRuleNumberEvaluation.Failed($"Expression '{expressionText}' did not evaluate to a numeric value.");
    }

    private static AccountRuleEvaluation Evaluate(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(expressionText))
        {
            return AccountRuleEvaluation.Failed("Expression text cannot be empty.");
        }

        try
        {
            var expression = new Expression(expressionText);
            foreach ((string key, object? value) in variables)
            {
                expression.Parameters[key] = NormalizeParameterValue(value);
            }

            if (expression.HasErrors())
            {
                return AccountRuleEvaluation.Failed(expression.Error);
            }

            object evaluatedValue = expression.Evaluate();
            return AccountRuleEvaluation.Success(evaluatedValue);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or EvaluationException)
        {
            return AccountRuleEvaluation.Failed(ex.Message);
        }
    }

    private static object? NormalizeParameterValue(object? value)
    {
        if (value is string text && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
        {
            return number;
        }

        return value;
    }

    private static bool TryConvertBoolean(object? value, out bool result)
    {
        switch (value)
        {
            case bool boolean:
                result = boolean;
                return true;
            case string text when bool.TryParse(text, out bool parsed):
                result = parsed;
                return true;
            case IConvertible convertible:
                try
                {
                    result = Convert.ToDecimal(convertible, CultureInfo.InvariantCulture) != 0m;
                    return true;
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    break;
                }
        }

        result = false;
        return false;
    }

    private static bool TryConvertDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal decimalValue:
                result = decimalValue;
                return true;
            case IConvertible convertible:
                try
                {
                    result = Convert.ToDecimal(convertible, CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    break;
                }
        }

        result = 0m;
        return false;
    }
}

public sealed record AccountRuleBooleanEvaluation(bool Succeeded, bool Value, string? Error)
{
    public static AccountRuleBooleanEvaluation Success(bool value) => new(true, value, null);

    public static AccountRuleBooleanEvaluation Failed(string error) => new(false, false, error);
}

public sealed record AccountRuleNumberEvaluation(bool Succeeded, decimal Value, string? Error)
{
    public static AccountRuleNumberEvaluation Success(decimal value) => new(true, value, null);

    public static AccountRuleNumberEvaluation Failed(string error) => new(false, 0m, error);
}

internal sealed record AccountRuleEvaluation(bool Succeeded, object? Value, string? Error)
{
    public static AccountRuleEvaluation Success(object? value) => new(true, value, null);

    public static AccountRuleEvaluation Failed(string? error) => new(false, null, error);
}
