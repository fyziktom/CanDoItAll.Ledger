using System.Globalization;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Rules.NCalc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class AccountRuleExpressionTests
{
    [Fact]
    public void RuleTokenAuthority_RoundTripsRuleData()
    {
        AccountRuleDefinition rule = CreateCashpoolRule(Address.FromPublicKey(Hex.Utf8("cashpool-public")));
        TransactionData data = RuleTokenAuthority.CreateData(rule);
        var businessObject = new BusinessObjectRef(
            RuleTokenAuthority.CreateRuleLineageKey(rule.RuleId),
            1,
            BusinessObjectOperation.Create,
            data.DataHash!.Value);

        bool parsed = RuleTokenAuthority.TryParseData(data, businessObject, out AccountRuleDefinition roundTripped);

        Assert.True(parsed);
        Assert.Equal("cashpool-sweep", roundTripped.RuleId);
        Assert.Equal("rules.company", roundTripped.RuleNamespace);
        Assert.Equal(AccountRuleTriggerKind.AccountSnapshot, roundTripped.Trigger!.Kind);
        Assert.Equal("account_main > limit", Assert.Single(roundTripped.Conditions));
        Assert.Equal(AccountRuleActionKind.Payment, Assert.Single(roundTripped.Actions).Kind);
    }

    [Fact]
    public void RuleTokenAuthority_RejectsInvalidHashBinding()
    {
        AccountRuleDefinition rule = CreateCashpoolRule(Address.FromPublicKey(Hex.Utf8("cashpool-public")));
        TransactionData data = RuleTokenAuthority.CreateData(rule);
        var businessObject = new BusinessObjectRef(
            RuleTokenAuthority.CreateRuleLineageKey(rule.RuleId),
            1,
            BusinessObjectOperation.Create,
            new HashHex(new string('a', 64)));

        bool parsed = RuleTokenAuthority.TryParseData(data, businessObject, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void RuleExpression_EvaluatesArithmeticComparisonsAndMultiConditions()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["account_main"] = 1250m,
            ["limit"] = 1000m,
            ["trigger_price"] = "150",
            ["allowance_limit"] = 500m
        };

        AccountRuleBooleanEvaluation balanceCondition = evaluator.EvaluateBoolean("account_main > limit", variables);
        AccountRuleBooleanEvaluation allowanceCondition = evaluator.EvaluateBoolean("trigger_price <= allowance_limit", variables);
        AccountRuleNumberEvaluation sweepAmount = evaluator.EvaluateNumber("account_main - limit", variables);

        Assert.True(balanceCondition.Succeeded);
        Assert.True(balanceCondition.Value);
        Assert.True(allowanceCondition.Succeeded);
        Assert.True(allowanceCondition.Value);
        Assert.True(sweepAmount.Succeeded);
        Assert.Equal(250m, sweepAmount.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RuleExpression_ReturnsFailureForEmptyExpression(string expressionText)
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();

        AccountRuleBooleanEvaluation result = evaluator.EvaluateBoolean(
            expressionText,
            new Dictionary<string, object?>(StringComparer.Ordinal));

        Assert.False(result.Succeeded);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuleExpression_ReturnsFailureForMalformedExpression()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();

        AccountRuleBooleanEvaluation result = evaluator.EvaluateBoolean(
            "account_main > (",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["account_main"] = 1250m
            });

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void RuleExpression_ReturnsFailureForMissingVariable()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();

        AccountRuleBooleanEvaluation result = evaluator.EvaluateBoolean(
            "account_main > limit",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["account_main"] = 1250m
            });

        Assert.False(result.Succeeded);
        Assert.Contains("limit", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuleExpression_ReturnsFailureForUnknownFunction()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();

        AccountRuleNumberEvaluation result = evaluator.EvaluateNumber(
            "unknown_rule_function(account_main)",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["account_main"] = 1250m
            });

        Assert.False(result.Succeeded);
        Assert.Contains("unknown_rule_function", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuleExpression_EvaluatesMixedNumbersBooleanOrAndNumericStrings()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["now_unix"] = 200L,
            ["bo_last_sweep_unix"] = 100m,
            ["sweep_cadence_seconds"] = "100",
            ["bo_waste_rate"] = 0.10m,
            ["waste_threshold"] = 0.20m,
            ["bo_capacity_gap"] = 1m,
            ["text_amount"] = "40.5"
        };

        AccountRuleBooleanEvaluation cadence = evaluator.EvaluateBoolean(
            "now_unix - bo_last_sweep_unix >= sweep_cadence_seconds",
            variables);
        AccountRuleBooleanEvaluation alternative = evaluator.EvaluateBoolean(
            "bo_waste_rate > waste_threshold or bo_capacity_gap > 0",
            variables);
        AccountRuleNumberEvaluation numericText = evaluator.EvaluateNumber("text_amount + 2", variables);

        Assert.True(cadence.Succeeded);
        Assert.True(cadence.Value);
        Assert.True(alternative.Succeeded);
        Assert.True(alternative.Value);
        Assert.True(numericText.Succeeded);
        Assert.Equal(42.5m, numericText.Value);
    }

    [Fact]
    public void RuleExpression_PreservesSupportedResultTypeConversions()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal);

        AccountRuleBooleanEvaluation numericBoolean = evaluator.EvaluateBoolean("1", variables);
        AccountRuleBooleanEvaluation stringBoolean = evaluator.EvaluateBoolean("'false'", variables);
        AccountRuleNumberEvaluation stringNumber = evaluator.EvaluateNumber("'42.5'", variables);
        AccountRuleNumberEvaluation booleanNumber = evaluator.EvaluateNumber("true", variables);
        AccountRuleBooleanEvaluation invalidBoolean = evaluator.EvaluateBoolean("'not-a-boolean'", variables);
        AccountRuleNumberEvaluation invalidNumber = evaluator.EvaluateNumber("'not-a-number'", variables);

        Assert.True(numericBoolean.Succeeded);
        Assert.True(numericBoolean.Value);
        Assert.True(stringBoolean.Succeeded);
        Assert.False(stringBoolean.Value);
        Assert.True(stringNumber.Succeeded);
        Assert.Equal(42.5m, stringNumber.Value);
        Assert.True(booleanNumber.Succeeded);
        Assert.Equal(1m, booleanNumber.Value);
        Assert.False(invalidBoolean.Succeeded);
        Assert.False(invalidNumber.Succeeded);
    }

    [Fact]
    public void RuleExpression_UsesInvariantCultureForParametersAndLiterals()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            AccountRuleNumberEvaluation result = evaluator.EvaluateNumber(
                "amount + 0.25",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["amount"] = "1.50"
                });

            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(1.75m, result.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void NCalcDriver_IsOuterImplementationBehindBusinessObjectsContract()
    {
        Assert.DoesNotContain(
            typeof(BusinessObjectAccount).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("NCalc", StringComparison.OrdinalIgnoreCase) == true);

        var services = new ServiceCollection();
        services.AddNCalcAccountRuleExpressionDriver();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<NCalcAccountRuleExpressionEvaluator>(
            provider.GetRequiredService<IAccountRuleExpressionEvaluator>());
    }

    private static AccountRuleDefinition CreateCashpoolRule(Address cashpoolAddress)
        => new(
            "cashpool-sweep",
            "rules.company",
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.AccountSnapshot),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["limit"] = "1000"
            },
            new[] { "account_main > limit" },
            new[]
            {
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: cashpoolAddress.Value,
                    AmountExpression: "account_main - limit")
            },
            AccountRuleProcessingOptions.Default);
}
