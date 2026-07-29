using CanDoItAll.Ledger.Rules.NCalc;
using CanDoItAll.Ledger.Simulator;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class LedgerSimulatorRuleAutomationTests
{
    [Fact]
    public void CommonRuleFactory_CreatesReusableFinanceControlRules()
    {
        Address treasury = Address.FromPublicKey("treasury-rule-owner"u8.ToArray());
        string ruleNamespace = "economy-simulator.test.rules";

        AccountRuleDefinition cashpool = LedgerSimulatorCommonRuleFactory.CreateCashpoolSweepRule(
            "test.cashpool-sweep.v1",
            ruleNamespace,
            treasury,
            targetCash: 10_000m,
            sweepBuffer: 500m,
            sweepCadenceSeconds: 86_400);
        AccountRuleDefinition payable = LedgerSimulatorCommonRuleFactory.CreatePayablePaymentRule(
            "test.payable-payment.v1",
            ruleNamespace,
            reserveFloor: 2_500m);
        AccountRuleDefinition arFollowUp = LedgerSimulatorCommonRuleFactory.CreateReceivableFollowUpRule(
            "test.receivable-follow-up.v1",
            ruleNamespace,
            treasury,
            graceSeconds: 86_400);

        Assert.Contains(LedgerSimulatorObjectTypes.RuleState, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.RuleExecutionEvidence, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Equal(AccountRuleTriggerKind.BusinessObject, cashpool.Trigger!.Kind);
        Assert.Equal(LedgerSimulatorObjectTypes.RuleState, cashpool.Trigger.ObjectType);
        Assert.Contains(cashpool.Actions, action => action.Kind == AccountRuleActionKind.UpdateBusinessObject && action.StateJson!.Contains("lastSweepUnix", StringComparison.Ordinal));
        Assert.Contains(payable.Actions, action => action.Kind == AccountRuleActionKind.Payment && action.ReceiverAddress == "bo_supplier_address");
        Assert.Contains(arFollowUp.Actions, action => action.Kind == AccountRuleActionKind.TransferBusinessObject);
        Assert.All(new[] { cashpool, payable, arFollowUp }, rule =>
        {
            Assert.True(RuleTokenAuthority.TryValidate(rule, out IReadOnlyList<string> errors), string.Join("; ", errors));
        });
    }

    [Fact]
    public void ScenarioRulePacks_CoverExpectedProcesses()
    {
        var catalog = new LedgerScenarioTemplateCatalog();
        Dictionary<string, string[]> expectedRuleIds = new(StringComparer.Ordinal)
        {
            ["S1"] =
            [
                "s1.enterprise-ar-follow-up.v1",
                "s1.implementation-milestone-collection.v1",
                "s1.hiring-equipment-request.v1",
                "s1.cloud-spend-guard.v1",
                "s1.department-spend-approval.v1",
                "s1.cash-reserve-guard.v1",
                "s1.cashpool-sweep.v1"
            ],
            ["S2"] =
            [
                "s2.ingredient-reorder.v1",
                "s2.supplier-payment-window.v1",
                "s2.daily-retail-cash-sweep.v1",
                "s2.wholesale-ar-follow-up.v1",
                "s2.production-exception-routing.v1",
                "s2.wage-rent-liquidity-guard.v1"
            ],
            ["S3"] =
            [
                "s3.customer-ar-follow-up.v1",
                "s3.component-reorder-request.v1",
                "s3.wip-stage-transfer.v1",
                "s3.quality-hold-release.v1",
                "s3.tooling-capex-gate.v1",
                "s3.maintenance-payment.v1",
                "s3.external-settlement.v1",
                "s3.revolver-draw.v1"
            ],
            ["VEF"] =
            [
                "vef.product-revenue-follow-up.v1",
                "vef.department-spend-self-approval.v1",
                "vef.management-expense-approval.v1",
                "vef.capex-investment-approval.v1",
                "vef.cash-reserve-guard.v1",
                "vef.cashpool-sweep.v1"
            ]
        };

        foreach (ScenarioBusinessModelState model in catalog.GetScenarios())
        {
            LedgerSimulatorScenarioRulePack pack = LedgerSimulatorScenarioRulePackCatalog.CreateRulePack(model);
            string[] ruleIds = pack.Rules.Select(rule => rule.RuleId).Order(StringComparer.Ordinal).ToArray();

            Assert.Equal(model.ProjectId, pack.ProjectId);
            Assert.Equal(model.ScenarioCode, pack.ScenarioCode);
            Assert.Equal(expectedRuleIds[model.ScenarioCode].Order(StringComparer.Ordinal).ToArray(), ruleIds);
            Assert.Equal(pack.Rules.Count, pack.Rules.Select(rule => rule.RuleId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(pack.Templates, template =>
            {
                Assert.Contains(template.OwnerBusinessUnitId, model.BusinessUnits.Select(unit => unit.BusinessUnitId));
                Assert.True(RuleTokenAuthority.TryValidate(template.Rule, out IReadOnlyList<string> errors), string.Join("; ", errors));
            });
        }
    }

    [Fact]
    public void ScenarioRulePackExpressions_AreAcceptedByNCalcDriver()
    {
        var evaluator = new NCalcAccountRuleExpressionEvaluator();
        var catalog = new LedgerScenarioTemplateCatalog();
        int evaluatedExpressionCount = 0;

        foreach (ScenarioBusinessModelState model in catalog.GetScenarios())
        {
            LedgerSimulatorScenarioRulePack pack = LedgerSimulatorScenarioRulePackCatalog.CreateRulePack(model);
            foreach (AccountRuleDefinition rule in pack.Rules)
            {
                Dictionary<string, object?> variables = CreateRepresentativeRuleVariables(rule);
                foreach (string condition in rule.Conditions)
                {
                    var result = evaluator.EvaluateBoolean(condition, variables);

                    Assert.True(
                        result.Succeeded,
                        $"Scenario '{model.ScenarioCode}', rule '{rule.RuleId}', condition '{condition}' failed NCalc evaluation: {result.Error}");
                    evaluatedExpressionCount++;
                }

                foreach (AccountRuleAction action in rule.Actions.Where(action => !string.IsNullOrWhiteSpace(action.AmountExpression)))
                {
                    var result = evaluator.EvaluateNumber(action.AmountExpression!, variables);

                    Assert.True(
                        result.Succeeded,
                        $"Scenario '{model.ScenarioCode}', rule '{rule.RuleId}', amount expression '{action.AmountExpression}' failed NCalc evaluation: {result.Error}");
                    evaluatedExpressionCount++;
                }
            }
        }

        Assert.True(evaluatedExpressionCount > 0);
    }

    [Fact]
    public void ScenarioRulePacks_DoNotChangeDeterministicScenarioCompilation()
    {
        var serializer = new LedgerSimulatorJsonSerializer();
        var compiler = new LedgerScenarioEventCompiler(serializer);
        var catalog = new LedgerScenarioTemplateCatalog();

        foreach (ScenarioBusinessModelState model in catalog.GetScenarios())
        {
            LedgerSimulatorScenarioRulePack pack = LedgerSimulatorScenarioRulePackCatalog.CreateRulePack(model);
            LedgerScenarioEventCompileResult compiled = compiler.Compile(
                new LedgerScenarioEventCompileRequest(
                    model,
                    new ScenarioId($"rule-proof-{model.ScenarioCode.ToLowerInvariant()}"),
                    model.PlanningStartDate,
                    model.PlanningEndDate));

            Assert.NotEmpty(pack.Rules);
            Assert.True(compiled.Events.Count > 0);
            Assert.True(compiled.ExpectedTotals["revenue"] > 0m);
            Assert.NotEmpty(compiled.ScheduleHash);
        }
    }

    private static Dictionary<string, object?> CreateRepresentativeRuleVariables(AccountRuleDefinition rule)
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["account_address"] = "simulator-account",
            ["account_main"] = 1_000_000m,
            ["utxo_count"] = 3,
            ["business_object_count"] = 12,
            ["transaction_count"] = 24,
            ["now_unix"] = 2_000_000L,
            ["rule_id"] = rule.RuleId,
            ["rule_namespace"] = rule.RuleNamespace,
            ["rule_execution_id"] = $"{rule.RuleId}:test-execution",
            ["token_test"] = 500m,
            ["trigger_object_id"] = "bo:test",
            ["trigger_object_type"] = rule.Trigger?.ObjectType ?? "economy-simulator.test",
            ["trigger_owner_address"] = "simulator-owner",
            ["trigger_version"] = 1,
            ["trigger_created_unix"] = 1_000_000L,
            ["trigger_age_seconds"] = 1_000_000d,
            ["bo_object_id"] = "bo:test",
            ["bo_object_type"] = rule.Trigger?.ObjectType ?? "economy-simulator.test",
            ["bo_owner_address"] = "simulator-owner",
            ["bo_last_sweep_unix"] = 1_000_000m,
            ["bo_amount"] = 100m,
            ["bo_supplier_address"] = "supplier-address",
            ["bo_receiver_address"] = "receiver-address",
            ["bo_processed"] = false,
            ["bo_approved"] = true,
            ["bo_due_unix"] = 1_000_000m,
            ["bo_paid"] = false,
            ["bo_settled"] = false,
            ["bo_ready"] = true,
            ["bo_business_case_score"] = 1m,
            ["bo_days_cover"] = 1m,
            ["bo_lead_time_days"] = 30m,
            ["bo_waste_rate"] = 1m,
            ["bo_capacity_gap"] = 1m,
            ["bo_stage_complete"] = true,
            ["bo_quality_hold"] = false,
            ["bo_passed"] = true,
            ["bo_failed"] = false,
            ["bo_undrawn_facility"] = 1_000_000m,
            ["bo_covenant_clear"] = true,
            ["trigger_tx_id"] = "tx:test",
            ["trigger_tx_kind"] = "Transfer",
            ["trigger_tx_status"] = "Confirmed",
            ["tx_id"] = "tx:test",
            ["tx_kind"] = "Transfer",
            ["tx_status"] = "Confirmed",
            ["tx_unix"] = 2_000_000L,
            ["tx_input_count"] = 1,
            ["tx_output_count"] = 2,
            ["tx_output_main"] = 200m,
            ["tx_account_output_main"] = 100m,
            ["tx_account_input_count"] = 1,
            ["tx_has_data"] = true,
            ["txdata_amount"] = 100m
        };

        foreach ((string key, string value) in rule.Variables)
        {
            variables[key] = value;
        }

        return variables;
    }
}
