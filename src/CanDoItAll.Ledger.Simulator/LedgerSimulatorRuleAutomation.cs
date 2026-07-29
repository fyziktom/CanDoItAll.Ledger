using System.Globalization;

namespace CanDoItAll.Ledger.Simulator;

public enum LedgerSimulatorRuleProcessKind
{
    CashpoolSweep = 0,
    ReserveGuard = 1,
    PurchaseApproval = 2,
    PayablePayment = 3,
    ReceivableFollowUp = 4,
    ExternalSettlement = 5,
    CapexApproval = 6,
    InventoryReorder = 7,
    ProductionGate = 8,
    QualityGate = 9,
    MaintenancePayment = 10,
    TreasuryFacility = 11
}

public sealed record RuleAutomationPlanState(
    string ProjectId,
    string ScenarioCode,
    string RuleNamespace,
    IReadOnlyList<string> RuleIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record RuleProcessTemplateState(
    string RuleId,
    string ProcessCode,
    LedgerSimulatorRuleProcessKind Kind,
    string TriggerObjectType,
    string FinanceControlPurpose,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<string> ActionDescriptions,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record RuleExecutionEvidenceState(
    string ProjectId,
    string ScenarioCode,
    string RuleId,
    string ActionKind,
    string Status,
    SimulationObjectReference? TriggerObjectReference,
    TxId? TriggerTransactionId,
    TxId? TxId,
    DateTimeOffset ObservedAtUtc,
    string Message,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static RuleExecutionEvidenceState FromResult(
        string projectId,
        string scenarioCode,
        AccountRuleExecutionResult result,
        DateTimeOffset observedAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            projectId,
            scenarioCode,
            result.RuleId,
            result.ActionKind.ToString(),
            result.Status.ToString(),
            result.TriggerObjectReference,
            result.TriggerTransactionId,
            result.TxId,
            observedAtUtc,
            result.Message,
            metadata ?? new Dictionary<string, string>(StringComparer.Ordinal));
}

public sealed record LedgerSimulatorRuleTemplate(
    AccountRuleDefinition Rule,
    RuleProcessTemplateState Process,
    BusinessUnitId OwnerBusinessUnitId,
    Address OwnerAddress);

public sealed record LedgerSimulatorScenarioRulePack(
    string ProjectId,
    string ScenarioCode,
    string RuleNamespace,
    IReadOnlyList<LedgerSimulatorRuleTemplate> Templates)
{
    public IReadOnlyList<AccountRuleDefinition> Rules => Templates.Select(x => x.Rule).ToArray();

    public RuleAutomationPlanState ToPlanState()
        => new(
            ProjectId,
            ScenarioCode,
            RuleNamespace,
            Rules.Select(x => x.RuleId).ToArray(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ruleCount"] = Rules.Count.ToString(CultureInfo.InvariantCulture),
                ["processCount"] = Templates.Count.ToString(CultureInfo.InvariantCulture)
            });
}

public static class LedgerSimulatorCommonRuleFactory
{
    public const string CommonCashpoolSweepRuleId = "common.cashpool-sweep.v1";
    public const string CommonReserveFloorGuardRuleId = "common.reserve-floor-guard.v1";
    public const string CommonPurchaseApprovalRuleId = "common.purchase-approval.v1";
    public const string CommonPayablePaymentRuleId = "common.payable-payment.v1";
    public const string CommonReceivableFollowUpRuleId = "common.receivable-follow-up.v1";
    public const string CommonExternalSettlementRuleId = "common.external-settlement.v1";
    public const string CommonCapexApprovalRuleId = "common.capex-approval.v1";

    public static AccountRuleDefinition CreateCashpoolSweepRule(
        string ruleId,
        string ruleNamespace,
        Address cashpoolAddress,
        decimal targetCash,
        decimal sweepBuffer,
        int sweepCadenceSeconds)
        => Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.RuleState),
            Variables(
                ("target_cash", targetCash),
                ("sweep_buffer", sweepBuffer),
                ("sweep_cadence_seconds", sweepCadenceSeconds)),
            [
                "account_main > target_cash + sweep_buffer",
                "now_unix - bo_last_sweep_unix >= sweep_cadence_seconds"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: cashpoolAddress.Value,
                    AmountExpression: "account_main - target_cash"),
                new AccountRuleAction(
                    AccountRuleActionKind.UpdateBusinessObject,
                    StateJson: "{\"lastSweepUnix\":{{now_unix}},\"lastSweepRuleId\":{{rule_id}},\"lastSweepExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false, MaxActionsPerRefresh = 2 });

    public static AccountRuleDefinition CreateReserveFloorGuardRule(
        string ruleId,
        string ruleNamespace,
        decimal reserveFloor)
        => Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.RuleState),
            Variables(("reserve_floor", reserveFloor)),
            ["account_main < reserve_floor"],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.UpdateBusinessObject,
                    StateJson: "{\"reserveBreach\":true,\"reserveBreachAtUnix\":{{now_unix}},\"lastRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false, MaxActionsPerRefresh = 1 });

    public static AccountRuleDefinition CreatePurchaseApprovalRule(
        string ruleId,
        string ruleNamespace,
        Address purchasingAddress,
        decimal approvalLimit,
        decimal reserveFloor,
        string triggerObjectType = LedgerSimulatorObjectTypes.PurchaseRequest)
        => Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, triggerObjectType),
            Variables(("approval_limit", approvalLimit), ("reserve_floor", reserveFloor)),
            [
                "bo_amount <= approval_limit",
                "bo_processed == false",
                "account_main >= bo_amount + reserve_floor"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.TransferBusinessObject,
                    ReceiverAddress: purchasingAddress.Value,
                    StateJson: "{\"approved\":true,\"approvedAtUnix\":{{now_unix}},\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    public static AccountRuleDefinition CreatePayablePaymentRule(
        string ruleId,
        string ruleNamespace,
        decimal reserveFloor,
        string triggerObjectType = LedgerSimulatorObjectTypes.Payable)
        => Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, triggerObjectType),
            Variables(("reserve_floor", reserveFloor)),
            [
                "bo_approved == true",
                "bo_due_unix <= now_unix",
                "bo_paid == false",
                "account_main >= bo_amount + reserve_floor"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: "bo_supplier_address",
                    AmountExpression: "bo_amount"),
                new AccountRuleAction(
                    AccountRuleActionKind.UpdateBusinessObject,
                    StateJson: "{\"paid\":true,\"paidAtUnix\":{{now_unix}},\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false, MaxActionsPerRefresh = 2 });

    public static AccountRuleDefinition CreateReceivableFollowUpRule(
        string ruleId,
        string ruleNamespace,
        Address collectionOwnerAddress,
        int graceSeconds,
        string triggerObjectType = LedgerSimulatorObjectTypes.Receivable)
        => Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, triggerObjectType),
            Variables(("grace_seconds", graceSeconds)),
            [
                "bo_settled == false",
                "bo_due_unix + grace_seconds < now_unix"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.TransferBusinessObject,
                    ReceiverAddress: collectionOwnerAddress.Value,
                    StateJson: "{\"followUpRequired\":true,\"followUpAtUnix\":{{now_unix}},\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    public static AccountRuleDefinition CreateExternalSettlementRule(
        string ruleId,
        string ruleNamespace,
        decimal reserveFloor)
        => Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.ExternalSettlement),
            Variables(("reserve_floor", reserveFloor)),
            [
                "bo_ready == true",
                "bo_processed == false",
                "account_main >= bo_amount + reserve_floor"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: "bo_receiver_address",
                    AmountExpression: "bo_amount"),
                new AccountRuleAction(
                    AccountRuleActionKind.UpdateBusinessObject,
                    StateJson: "{\"processed\":true,\"settledAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false, MaxActionsPerRefresh = 2 });

    public static AccountRuleDefinition CreateCapexApprovalRule(
        string ruleId,
        string ruleNamespace,
        Address capexOwnerAddress,
        decimal capexLimit,
        decimal reserveFloor,
        decimal minimumBusinessCaseScore)
        => Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.CapexRequest),
            Variables(
                ("capex_limit", capexLimit),
                ("reserve_floor", reserveFloor),
                ("min_business_case_score", minimumBusinessCaseScore)),
            [
                "bo_amount <= capex_limit",
                "account_main >= bo_amount + reserve_floor",
                "bo_business_case_score >= min_business_case_score"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.TransferBusinessObject,
                    ReceiverAddress: capexOwnerAddress.Value,
                    StateJson: "{\"approved\":true,\"approvedAtUnix\":{{now_unix}},\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    public static RuleProcessTemplateState Process(
        AccountRuleDefinition rule,
        string processCode,
        LedgerSimulatorRuleProcessKind kind,
        string triggerObjectType,
        string financeControlPurpose,
        params string[] actionDescriptions)
        => new(
            rule.RuleId,
            processCode,
            kind,
            triggerObjectType,
            financeControlPurpose,
            rule.Conditions.ToArray(),
            actionDescriptions,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["commonRuleFamily"] = GetCommonRuleFamily(kind)
            });

    internal static AccountRuleDefinition Rule(
        string ruleId,
        string ruleNamespace,
        AccountRuleTrigger trigger,
        IReadOnlyDictionary<string, string> variables,
        IReadOnlyList<string> conditions,
        IReadOnlyList<AccountRuleAction> actions,
        AccountRuleProcessingOptions? processing = null)
        => new(ruleId, ruleNamespace, Enabled: true, trigger, variables, conditions, actions, processing ?? AccountRuleProcessingOptions.Default);

    internal static IReadOnlyDictionary<string, string> Variables(params (string Key, object Value)[] values)
        => values.ToDictionary(
            x => x.Key,
            x => Convert.ToString(x.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            StringComparer.Ordinal);

    private static string GetCommonRuleFamily(LedgerSimulatorRuleProcessKind kind)
        => kind switch
        {
            LedgerSimulatorRuleProcessKind.CashpoolSweep => CommonCashpoolSweepRuleId,
            LedgerSimulatorRuleProcessKind.ReserveGuard => CommonReserveFloorGuardRuleId,
            LedgerSimulatorRuleProcessKind.PurchaseApproval => CommonPurchaseApprovalRuleId,
            LedgerSimulatorRuleProcessKind.PayablePayment => CommonPayablePaymentRuleId,
            LedgerSimulatorRuleProcessKind.ReceivableFollowUp => CommonReceivableFollowUpRuleId,
            LedgerSimulatorRuleProcessKind.ExternalSettlement => CommonExternalSettlementRuleId,
            LedgerSimulatorRuleProcessKind.CapexApproval => CommonCapexApprovalRuleId,
            _ => "common.specialized-control.v1"
        };
}

public static class LedgerSimulatorScenarioRulePackCatalog
{
    public static LedgerSimulatorScenarioRulePack CreateRulePack(ScenarioBusinessModelState model)
        => model.ScenarioCode.ToUpperInvariant() switch
        {
            "S1" => CreateS1(model),
            "S2" => CreateS2(model),
            "S3" => CreateS3(model),
            "VEF" => CreateVef(model),
            _ => throw new InvalidOperationException($"Scenario '{model.ScenarioCode}' does not have a rule pack.")
        };

    private static LedgerSimulatorScenarioRulePack CreateS1(ScenarioBusinessModelState model)
    {
        string ns = RuleNamespace(model);
        BusinessUnitState treasury = Unit(model, "treasury");
        BusinessUnitState sales = Unit(model, "sales-marketing");
        BusinessUnitState admin = Unit(model, "finance-hr-admin");
        BusinessUnitState engineering = Unit(model, "product-engineering");
        BusinessUnitState cloud = Unit(model, "it-cloud-operations");
        decimal reserve = model.MinimumCash.Value;

        return Pack(model, ns,
            Template(model, sales, LedgerSimulatorCommonRuleFactory.CreateReceivableFollowUpRule("s1.enterprise-ar-follow-up.v1", ns, admin.PrimaryAddress, Days(7)), "enterprise-ar", LedgerSimulatorRuleProcessKind.ReceivableFollowUp, LedgerSimulatorObjectTypes.Receivable, "Enterprise subscription DSO follow-up."),
            Template(model, sales, LedgerSimulatorCommonRuleFactory.CreateReceivableFollowUpRule("s1.implementation-milestone-collection.v1", ns, admin.PrimaryAddress, Days(3)), "implementation-ar", LedgerSimulatorRuleProcessKind.ReceivableFollowUp, LedgerSimulatorObjectTypes.Receivable, "Implementation and license milestone collection follow-up."),
            Template(model, engineering, LedgerSimulatorCommonRuleFactory.CreatePurchaseApprovalRule("s1.hiring-equipment-request.v1", ns, admin.PrimaryAddress, 5_000m, reserve), "hiring-equipment", LedgerSimulatorRuleProcessKind.PurchaseApproval, LedgerSimulatorObjectTypes.PurchaseRequest, "New-hire equipment approval and routing."),
            Template(model, cloud, LedgerSimulatorCommonRuleFactory.CreatePurchaseApprovalRule("s1.cloud-spend-guard.v1", ns, admin.PrimaryAddress, 25_000m, reserve), "cloud-spend", LedgerSimulatorRuleProcessKind.PurchaseApproval, LedgerSimulatorObjectTypes.PurchaseRequest, "Cloud and tooling budget guardrail."),
            Template(model, admin, LedgerSimulatorCommonRuleFactory.CreatePurchaseApprovalRule("s1.department-spend-approval.v1", ns, admin.PrimaryAddress, 10_000m, reserve), "department-spend", LedgerSimulatorRuleProcessKind.PurchaseApproval, LedgerSimulatorObjectTypes.PurchaseRequest, "Department spend authority and liquidity guard."),
            Template(model, treasury, LedgerSimulatorCommonRuleFactory.CreateReserveFloorGuardRule("s1.cash-reserve-guard.v1", ns, reserve), "reserve-floor", LedgerSimulatorRuleProcessKind.ReserveGuard, LedgerSimulatorObjectTypes.RuleState, "Runway reserve breach evidence."),
            Template(model, treasury, LedgerSimulatorCommonRuleFactory.CreateCashpoolSweepRule("s1.cashpool-sweep.v1", ns, treasury.PrimaryAddress, reserve, 5_000m, Days(1)), "cashpool-sweep", LedgerSimulatorRuleProcessKind.CashpoolSweep, LedgerSimulatorObjectTypes.RuleState, "Concentrate cash above operating target."));
    }

    private static LedgerSimulatorScenarioRulePack CreateS2(ScenarioBusinessModelState model)
    {
        string ns = RuleNamespace(model);
        BusinessUnitState treasury = Unit(model, "treasury");
        BusinessUnitState production = Unit(model, "production");
        BusinessUnitState warehouse = Unit(model, "warehouse-procurement");
        BusinessUnitState sales = Unit(model, "sales-management");
        decimal reserve = model.MinimumCash.Value;

        return Pack(model, ns,
            Template(model, warehouse, CreateInventoryReorderRule("s2.ingredient-reorder.v1", ns, warehouse.PrimaryAddress, 7m), "ingredient-reorder", LedgerSimulatorRuleProcessKind.InventoryReorder, LedgerSimulatorObjectTypes.InventoryReorderRequest, "Ingredient reorder routing before stockout."),
            Template(model, warehouse, LedgerSimulatorCommonRuleFactory.CreatePayablePaymentRule("s2.supplier-payment-window.v1", ns, reserve), "supplier-payment", LedgerSimulatorRuleProcessKind.PayablePayment, LedgerSimulatorObjectTypes.Payable, "Supplier payment run with reserve protection."),
            Template(model, treasury, LedgerSimulatorCommonRuleFactory.CreateCashpoolSweepRule("s2.daily-retail-cash-sweep.v1", ns, treasury.PrimaryAddress, reserve, 1_000m, Days(1)), "daily-retail-sweep", LedgerSimulatorRuleProcessKind.CashpoolSweep, LedgerSimulatorObjectTypes.RuleState, "Daily cash discipline for retail takings."),
            Template(model, sales, LedgerSimulatorCommonRuleFactory.CreateReceivableFollowUpRule("s2.wholesale-ar-follow-up.v1", ns, sales.PrimaryAddress, Days(2)), "wholesale-ar", LedgerSimulatorRuleProcessKind.ReceivableFollowUp, LedgerSimulatorObjectTypes.Receivable, "Wholesale AR follow-up for net terms."),
            Template(model, production, CreateProductionExceptionRule("s2.production-exception-routing.v1", ns, production.PrimaryAddress, 0.06m), "production-exception", LedgerSimulatorRuleProcessKind.ProductionGate, LedgerSimulatorObjectTypes.ProductionGateExecution, "Waste and production exception routing."),
            Template(model, treasury, LedgerSimulatorCommonRuleFactory.CreateReserveFloorGuardRule("s2.wage-rent-liquidity-guard.v1", ns, reserve), "wage-rent-liquidity", LedgerSimulatorRuleProcessKind.ReserveGuard, LedgerSimulatorObjectTypes.RuleState, "Payroll, rent, and electricity liquidity guard."));
    }

    private static LedgerSimulatorScenarioRulePack CreateS3(ScenarioBusinessModelState model)
    {
        string ns = RuleNamespace(model);
        BusinessUnitState treasury = Unit(model, "treasury");
        BusinessUnitState warehouse = Unit(model, "logistics-warehouse");
        BusinessUnitState quality = Unit(model, "quality");
        BusinessUnitState industrialization = Unit(model, "production-engineering");
        BusinessUnitState maintenance = Unit(model, "maintenance");
        BusinessUnitState sales = Unit(model, "sales-account-management");
        decimal reserve = model.MinimumCash.Value;

        return Pack(model, ns,
            Template(model, sales, LedgerSimulatorCommonRuleFactory.CreateReceivableFollowUpRule("s3.customer-ar-follow-up.v1", ns, sales.PrimaryAddress, Days(7)), "customer-ar", LedgerSimulatorRuleProcessKind.ReceivableFollowUp, LedgerSimulatorObjectTypes.Receivable, "Customer-program AR follow-up by payment terms."),
            Template(model, warehouse, CreateInventoryReorderRule("s3.component-reorder-request.v1", ns, warehouse.PrimaryAddress, 20m, minLeadTimeDays: 30m), "component-reorder", LedgerSimulatorRuleProcessKind.InventoryReorder, LedgerSimulatorObjectTypes.InventoryReorderRequest, "Long-lead component reorder routing."),
            Template(model, warehouse, CreateWipStageTransferRule("s3.wip-stage-transfer.v1", ns, quality.PrimaryAddress), "wip-stage", LedgerSimulatorRuleProcessKind.ProductionGate, LedgerSimulatorObjectTypes.ProductionGateExecution, "Trace WIP stage handoff."),
            Template(model, quality, CreateQualityHoldReleaseRule("s3.quality-hold-release.v1", ns, quality.PrimaryAddress), "quality-release", LedgerSimulatorRuleProcessKind.QualityGate, LedgerSimulatorObjectTypes.QualityGateExecution, "Release or route quality holds."),
            Template(model, industrialization, LedgerSimulatorCommonRuleFactory.CreateCapexApprovalRule("s3.tooling-capex-gate.v1", ns, industrialization.PrimaryAddress, 500_000m, reserve, 0.70m), "tooling-capex", LedgerSimulatorRuleProcessKind.CapexApproval, LedgerSimulatorObjectTypes.CapexRequest, "Production-prep tooling CapEx gate."),
            Template(model, maintenance, LedgerSimulatorCommonRuleFactory.CreatePayablePaymentRule("s3.maintenance-payment.v1", ns, reserve, LedgerSimulatorObjectTypes.MaintenanceRequest), "maintenance-payment", LedgerSimulatorRuleProcessKind.MaintenancePayment, LedgerSimulatorObjectTypes.MaintenanceRequest, "Critical maintenance payment decision."),
            Template(model, treasury, LedgerSimulatorCommonRuleFactory.CreateExternalSettlementRule("s3.external-settlement.v1", ns, reserve), "external-settlement", LedgerSimulatorRuleProcessKind.ExternalSettlement, LedgerSimulatorObjectTypes.ExternalSettlement, "Traceable internal/external cash boundary movement."),
            Template(model, treasury, CreateRevolverDrawRule("s3.revolver-draw.v1", ns, model.MinimumCash.Value), "revolver-draw", LedgerSimulatorRuleProcessKind.TreasuryFacility, LedgerSimulatorObjectTypes.LiquidityFacilityState, "Minimum-cash facility draw request with review trace."));
    }

    private static LedgerSimulatorScenarioRulePack CreateVef(ScenarioBusinessModelState model)
    {
        string ns = RuleNamespace(model);
        BusinessUnitState treasury = Unit(model, "treasury");
        BusinessUnitState management = Unit(model, "management");
        BusinessUnitState finance = Unit(model, "finance-hr-admin");
        BusinessUnitState sales = Unit(model, "sales");
        BusinessUnitState development = Unit(model, "development");
        decimal reserve = model.MinimumCash.Value;

        return Pack(model, ns,
            Template(model, sales, LedgerSimulatorCommonRuleFactory.CreateReceivableFollowUpRule("vef.product-revenue-follow-up.v1", ns, finance.PrimaryAddress, Days(7)), "product-revenue-ar", LedgerSimulatorRuleProcessKind.ReceivableFollowUp, LedgerSimulatorObjectTypes.Receivable, "Follow up product and service cash receipts from the Excel-plan revenue line."),
            Template(model, development, LedgerSimulatorCommonRuleFactory.CreatePurchaseApprovalRule("vef.department-spend-self-approval.v1", ns, finance.PrimaryAddress, 10_000m, reserve), "department-spend-self-approval", LedgerSimulatorRuleProcessKind.PurchaseApproval, LedgerSimulatorObjectTypes.PurchaseRequest, "Route BU-level planned expenses that fit local authority."),
            Template(model, management, LedgerSimulatorCommonRuleFactory.CreatePurchaseApprovalRule("vef.management-expense-approval.v1", ns, management.PrimaryAddress, 75_000m, reserve), "management-expense-approval", LedgerSimulatorRuleProcessKind.PurchaseApproval, LedgerSimulatorObjectTypes.PurchaseRequest, "Management review for larger planned expenses before dispatch."),
            Template(model, management, LedgerSimulatorCommonRuleFactory.CreateCapexApprovalRule("vef.capex-investment-approval.v1", ns, management.PrimaryAddress, 500_000m, reserve, 0.50m), "capex-investment-approval", LedgerSimulatorRuleProcessKind.CapexApproval, LedgerSimulatorObjectTypes.CapexRequest, "CapEx/investment request approval for scheduled equipment, prototype, and hardware preparation expenses."),
            Template(model, treasury, LedgerSimulatorCommonRuleFactory.CreateReserveFloorGuardRule("vef.cash-reserve-guard.v1", ns, reserve), "reserve-floor", LedgerSimulatorRuleProcessKind.ReserveGuard, LedgerSimulatorObjectTypes.RuleState, "Warn when the reference plan drops below the management reserve floor."),
            Template(model, treasury, LedgerSimulatorCommonRuleFactory.CreateCashpoolSweepRule("vef.cashpool-sweep.v1", ns, treasury.PrimaryAddress, reserve, 5_000m, Days(1)), "cashpool-sweep", LedgerSimulatorRuleProcessKind.CashpoolSweep, LedgerSimulatorObjectTypes.RuleState, "Keep excess cash visible at treasury."));
    }

    private static AccountRuleDefinition CreateInventoryReorderRule(
        string ruleId,
        string ruleNamespace,
        Address procurementAddress,
        decimal safetyStockDays,
        decimal minLeadTimeDays = 0m)
        => LedgerSimulatorCommonRuleFactory.Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.InventoryReorderRequest),
            LedgerSimulatorCommonRuleFactory.Variables(("safety_stock_days", safetyStockDays), ("min_lead_time_days", minLeadTimeDays)),
            [
                "bo_days_cover < safety_stock_days",
                "bo_lead_time_days >= min_lead_time_days",
                "bo_processed == false"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.TransferBusinessObject,
                    ReceiverAddress: procurementAddress.Value,
                    StateJson: "{\"reorderRouted\":true,\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    private static AccountRuleDefinition CreateProductionExceptionRule(
        string ruleId,
        string ruleNamespace,
        Address productionReviewAddress,
        decimal wasteThreshold)
        => LedgerSimulatorCommonRuleFactory.Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.ProductionGateExecution),
            LedgerSimulatorCommonRuleFactory.Variables(("waste_threshold", wasteThreshold)),
            ["bo_waste_rate > waste_threshold or bo_capacity_gap > 0", "bo_processed == false"],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.TransferBusinessObject,
                    ReceiverAddress: productionReviewAddress.Value,
                    StateJson: "{\"exceptionRouted\":true,\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    private static AccountRuleDefinition CreateWipStageTransferRule(
        string ruleId,
        string ruleNamespace,
        Address nextStageAddress)
        => LedgerSimulatorCommonRuleFactory.Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.ProductionGateExecution),
            LedgerSimulatorCommonRuleFactory.Variables(),
            ["bo_stage_complete == true", "bo_quality_hold == false", "bo_processed == false"],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.TransferBusinessObject,
                    ReceiverAddress: nextStageAddress.Value,
                    StateJson: "{\"wipTransferred\":true,\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    private static AccountRuleDefinition CreateQualityHoldReleaseRule(
        string ruleId,
        string ruleNamespace,
        Address qualityReviewAddress)
        => LedgerSimulatorCommonRuleFactory.Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.QualityGateExecution),
            LedgerSimulatorCommonRuleFactory.Variables(),
            ["bo_passed == true or bo_failed == true", "bo_processed == false"],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.TransferBusinessObject,
                    ReceiverAddress: qualityReviewAddress.Value,
                    StateJson: "{\"qualityDecisionRecorded\":true,\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    private static AccountRuleDefinition CreateRevolverDrawRule(
        string ruleId,
        string ruleNamespace,
        decimal minimumCash)
        => LedgerSimulatorCommonRuleFactory.Rule(
            ruleId,
            ruleNamespace,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, LedgerSimulatorObjectTypes.LiquidityFacilityState),
            LedgerSimulatorCommonRuleFactory.Variables(("minimum_cash", minimumCash)),
            [
                "account_main < minimum_cash",
                "bo_undrawn_facility >= minimum_cash - account_main",
                "bo_covenant_clear == true"
            ],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.UpdateBusinessObject,
                    StateJson: "{\"drawRequested\":true,\"drawAmount\":{{minimum_cash}},\"drawRequestedAtUnix\":{{now_unix}},\"lastRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            ],
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    private static LedgerSimulatorScenarioRulePack Pack(
        ScenarioBusinessModelState model,
        string ruleNamespace,
        params LedgerSimulatorRuleTemplate[] templates)
        => new(model.ProjectId, model.ScenarioCode, ruleNamespace, templates);

    private static LedgerSimulatorRuleTemplate Template(
        ScenarioBusinessModelState model,
        BusinessUnitState owner,
        AccountRuleDefinition rule,
        string processCode,
        LedgerSimulatorRuleProcessKind kind,
        string triggerObjectType,
        string purpose)
        => new(
            rule,
            LedgerSimulatorCommonRuleFactory.Process(rule, processCode, kind, triggerObjectType, purpose, DescribeActions(rule)),
            owner.BusinessUnitId,
            owner.PrimaryAddress);

    private static string[] DescribeActions(AccountRuleDefinition rule)
        => rule.Actions.Select(action => action.Kind.ToString()).ToArray();

    private static string RuleNamespace(ScenarioBusinessModelState model)
        => $"economy-simulator.{model.ProjectId}.rules";

    private static int Days(int days) => checked(days * 24 * 60 * 60);

    private static BusinessUnitState Unit(ScenarioBusinessModelState model, string path)
        => model.BusinessUnits.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Scenario {model.ScenarioCode} does not contain BU path '{path}'.");
}
