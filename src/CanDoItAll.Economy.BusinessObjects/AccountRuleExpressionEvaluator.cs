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

public sealed class UnavailableAccountRuleExpressionEvaluator : IAccountRuleExpressionEvaluator
{
    public static UnavailableAccountRuleExpressionEvaluator Instance { get; } = new();

    private UnavailableAccountRuleExpressionEvaluator()
    {
    }

    public AccountRuleBooleanEvaluation EvaluateBoolean(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
        => AccountRuleBooleanEvaluation.Failed(
            "No account-rule expression driver is configured. Register an IAccountRuleExpressionEvaluator implementation.");

    public AccountRuleNumberEvaluation EvaluateNumber(
        string expressionText,
        IReadOnlyDictionary<string, object?> variables)
        => AccountRuleNumberEvaluation.Failed(
            "No account-rule expression driver is configured. Register an IAccountRuleExpressionEvaluator implementation.");
}
