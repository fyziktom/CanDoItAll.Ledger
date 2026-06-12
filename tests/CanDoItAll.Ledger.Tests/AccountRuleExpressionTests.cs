using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class AccountRuleExpressionTests
{
    [Fact]
    public void RuleTokenAuthority_RoundTripsRuleData()
    {
        AccountRuleDefinition rule = CreateCashpoolRule(Address.FromPublicKey(Hex.Utf8("cashpool-public")));
        TransactionData data = RuleTokenAuthority.CreateData(rule);
        var businessObject = new BusinessObjectRef(
            RuleTokenAuthority.CreateRuleObjectId(rule.RuleId),
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
            RuleTokenAuthority.CreateRuleObjectId(rule.RuleId),
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
