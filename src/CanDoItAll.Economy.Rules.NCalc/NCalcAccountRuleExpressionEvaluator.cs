using System.Globalization;
using CanDoItAll.Economy.BusinessObjects;
using NCalc;

namespace CanDoItAll.Economy.Rules.NCalc;

public sealed class NCalcAccountRuleExpressionEvaluator : IAccountRuleExpressionEvaluator
{
    public AccountRuleBooleanEvaluation EvaluateBoolean(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
    {
        DriverEvaluation evaluation = Evaluate(expressionText, variables);
        if (!evaluation.Succeeded)
        {
            return AccountRuleBooleanEvaluation.Failed(evaluation.Error ?? "Expression evaluation failed.");
        }

        return TryConvertBoolean(evaluation.Value, out bool value)
            ? AccountRuleBooleanEvaluation.Success(value)
            : AccountRuleBooleanEvaluation.Failed($"Expression '{expressionText}' did not evaluate to a boolean value.");
    }

    public AccountRuleNumberEvaluation EvaluateNumber(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
    {
        DriverEvaluation evaluation = Evaluate(expressionText, variables);
        if (!evaluation.Succeeded)
        {
            return AccountRuleNumberEvaluation.Failed(evaluation.Error ?? "Expression evaluation failed.");
        }

        return TryConvertDecimal(evaluation.Value, out decimal value)
            ? AccountRuleNumberEvaluation.Success(value)
            : AccountRuleNumberEvaluation.Failed($"Expression '{expressionText}' did not evaluate to a numeric value.");
    }

    private static DriverEvaluation Evaluate(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(expressionText))
        {
            return DriverEvaluation.Failed("Expression text cannot be empty.");
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
                return DriverEvaluation.Failed(expression.Error);
            }

            return DriverEvaluation.Success(expression.Evaluate());
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or EvaluationException)
        {
            return DriverEvaluation.Failed(ex.Message);
        }
    }

    private static object? NormalizeParameterValue(object? value)
        => value is string text
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)
                ? number
                : value;

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

    private sealed record DriverEvaluation(bool Succeeded, object? Value, string? Error)
    {
        public static DriverEvaluation Success(object? value) => new(true, value, null);
        public static DriverEvaluation Failed(string? error) => new(false, null, error);
    }
}
