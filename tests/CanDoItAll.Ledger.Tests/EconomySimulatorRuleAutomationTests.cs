using CanDoItAll.Economy.Simulator;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomySimulatorRuleAutomationTests
{
    [Fact]
    public void CommonRuleFactory_CreatesReusableFinanceControlRules()
    {
        Address treasury = Address.FromPublicKey("treasury-rule-owner"u8.ToArray());
        string ruleNamespace = "economy-simulator.test.rules";

        AccountRuleDefinition cashpool = EconomySimulatorCommonRuleFactory.CreateCashpoolSweepRule(
            "test.cashpool-sweep.v1",
            ruleNamespace,
            treasury,
            targetCash: 10_000m,
            sweepBuffer: 500m,
            sweepCadenceSeconds: 86_400);
        AccountRuleDefinition payable = EconomySimulatorCommonRuleFactory.CreatePayablePaymentRule(
            "test.payable-payment.v1",
            ruleNamespace,
            reserveFloor: 2_500m);
        AccountRuleDefinition arFollowUp = EconomySimulatorCommonRuleFactory.CreateReceivableFollowUpRule(
            "test.receivable-follow-up.v1",
            ruleNamespace,
            treasury,
            graceSeconds: 86_400);

        Assert.Contains(EconomySimulatorObjectTypes.RuleState, EconomySimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(EconomySimulatorObjectTypes.RuleExecutionEvidence, EconomySimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Equal(AccountRuleTriggerKind.BusinessObject, cashpool.Trigger!.Kind);
        Assert.Equal(EconomySimulatorObjectTypes.RuleState, cashpool.Trigger.ObjectType);
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
        var catalog = new EconomyScenarioTemplateCatalog();
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
            EconomySimulatorScenarioRulePack pack = EconomySimulatorScenarioRulePackCatalog.CreateRulePack(model);
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
    public void ScenarioRulePacks_DoNotChangeDeterministicScenarioCompilation()
    {
        var serializer = new EconomySimulatorJsonSerializer();
        var compiler = new EconomyScenarioEventCompiler(serializer);
        var catalog = new EconomyScenarioTemplateCatalog();

        foreach (ScenarioBusinessModelState model in catalog.GetScenarios())
        {
            EconomySimulatorScenarioRulePack pack = EconomySimulatorScenarioRulePackCatalog.CreateRulePack(model);
            EconomyScenarioEventCompileResult compiled = compiler.Compile(
                new EconomyScenarioEventCompileRequest(
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
}
