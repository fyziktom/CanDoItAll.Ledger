using System.Text.RegularExpressions;

namespace CanDoItAll.Economy.Simulator;

public static partial class EconomySimulatorSchema
{
    public const string Version = "1.0";
}

public static class EconomySimulatorObjectTypes
{
    public const string Model = "economy-simulator.model";
    public const string ProjectConfiguration = "economy-simulator.project-configuration";
    public const string ProjectFolder = "economy-simulator.project-folder";
    public const string ProjectBusinessObjectDefinition = "economy-simulator.project-business-object-definition";
    public const string FinancialInputProfile = "economy-simulator.financial-input-profile";
    public const string RunSetup = "economy-simulator.run-setup";
    public const string ExportManifest = "economy-simulator.export-manifest";
    public const string RunObservation = "economy-simulator.run-observation";
    public const string BusinessUnit = "economy-simulator.business-unit";
    public const string BusinessUnitAddressBinding = "economy-simulator.business-unit-address-binding";
    public const string TokenDefinition = "economy-simulator.token-definition";
    public const string Template = "economy-simulator.template";
    public const string RevenueStream = "economy-simulator.revenue-stream";
    public const string PaymentTerm = "economy-simulator.payment-term";
    public const string Counterparty = "economy-simulator.counterparty";
    public const string CustomerContract = "economy-simulator.customer-contract";
    public const string DemandSchedule = "economy-simulator.demand-schedule";
    public const string ProductOffering = "economy-simulator.product-offering";
    public const string ProductSku = "economy-simulator.product-sku";
    public const string Recipe = "economy-simulator.recipe";
    public const string BillOfMaterial = "economy-simulator.bill-of-material";
    public const string InventoryLocation = "economy-simulator.inventory-location";
    public const string ProductionRoute = "economy-simulator.production-route";
    public const string WorkOrder = "economy-simulator.work-order";
    public const string WorkAllocationRule = "economy-simulator.work-allocation-rule";
    public const string WorkDelivery = "economy-simulator.work-delivery";
    public const string ProductFlowPlan = "economy-simulator.product-flow-plan";
    public const string ProductFlowStep = "economy-simulator.product-flow-step";
    public const string WorkforcePlan = "economy-simulator.workforce-plan";
    public const string EquipmentPolicy = "economy-simulator.equipment-policy";
    public const string MaintenancePlan = "economy-simulator.maintenance-plan";
    public const string QualityGate = "economy-simulator.quality-gate";
    public const string TestLabPlan = "economy-simulator.test-lab-plan";
    public const string ServiceOffering = "economy-simulator.service-offering";
    public const string CostPolicy = "economy-simulator.cost-policy";
    public const string PayrollPlan = "economy-simulator.payroll-plan";
    public const string TaxRuleSet = "economy-simulator.tax-rule-set";
    public const string Asset = "economy-simulator.asset";
    public const string DepreciationSchedule = "economy-simulator.depreciation-schedule";
    public const string Investment = "economy-simulator.investment";
    public const string FinancingInstrument = "economy-simulator.financing-instrument";
    public const string Invoice = "economy-simulator.invoice";
    public const string Receivable = "economy-simulator.receivable";
    public const string Payable = "economy-simulator.payable";
    public const string EconomicEvent = "economy-simulator.economic-event";
    public const string CashFlowSchedule = "economy-simulator.cashflow-schedule";
    public const string ExternalSettlement = "economy-simulator.external-settlement";
    public const string RuleAutomationPlan = "economy-simulator.rule-automation-plan";
    public const string RuleProcessTemplate = "economy-simulator.rule-process-template";
    public const string RuleExecutionEvidence = "economy-simulator.rule-execution-evidence";
    public const string RuleState = "economy-simulator.rule-state";
    public const string RuntimeIntervention = "economy-simulator.runtime-intervention";
    public const string PlannedFinancialSchedule = "economy-simulator.planned-financial-schedule";
    public const string PurchaseRequest = "economy-simulator.purchase-request";
    public const string InventoryReorderRequest = "economy-simulator.inventory-reorder-request";
    public const string CapexRequest = "economy-simulator.capex-request";
    public const string ProductionGateExecution = "economy-simulator.production-gate-execution";
    public const string QualityGateExecution = "economy-simulator.quality-gate-execution";
    public const string MaintenanceRequest = "economy-simulator.maintenance-request";
    public const string LiquidityFacilityState = "economy-simulator.liquidity-facility-state";
    public const string ScenarioRun = "economy-simulator.scenario-run";
    public const string SimulationResult = "economy-simulator.simulation-result";
    public const string CalibrationSnapshot = "economy-simulator.calibration-snapshot";
    public const string CalibrationRecommendation = "economy-simulator.calibration-recommendation";
}

public interface IEconomySimulatorObjectTypeCatalog
{
    IReadOnlySet<string> ObjectTypes { get; }

    bool Contains(string objectType);
}

public sealed class EconomySimulatorObjectTypeCatalog : IEconomySimulatorObjectTypeCatalog
{
    public static readonly EconomySimulatorObjectTypeCatalog Instance = new();

    public IReadOnlySet<string> ObjectTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        EconomySimulatorObjectTypes.Model,
        EconomySimulatorObjectTypes.ProjectConfiguration,
        EconomySimulatorObjectTypes.ProjectFolder,
        EconomySimulatorObjectTypes.ProjectBusinessObjectDefinition,
        EconomySimulatorObjectTypes.FinancialInputProfile,
        EconomySimulatorObjectTypes.RunSetup,
        EconomySimulatorObjectTypes.ExportManifest,
        EconomySimulatorObjectTypes.RunObservation,
        EconomySimulatorObjectTypes.BusinessUnit,
        EconomySimulatorObjectTypes.BusinessUnitAddressBinding,
        EconomySimulatorObjectTypes.TokenDefinition,
        EconomySimulatorObjectTypes.Template,
        EconomySimulatorObjectTypes.RevenueStream,
        EconomySimulatorObjectTypes.PaymentTerm,
        EconomySimulatorObjectTypes.Counterparty,
        EconomySimulatorObjectTypes.CustomerContract,
        EconomySimulatorObjectTypes.DemandSchedule,
        EconomySimulatorObjectTypes.ProductOffering,
        EconomySimulatorObjectTypes.ProductSku,
        EconomySimulatorObjectTypes.Recipe,
        EconomySimulatorObjectTypes.BillOfMaterial,
        EconomySimulatorObjectTypes.InventoryLocation,
        EconomySimulatorObjectTypes.ProductionRoute,
        EconomySimulatorObjectTypes.WorkOrder,
        EconomySimulatorObjectTypes.WorkAllocationRule,
        EconomySimulatorObjectTypes.WorkDelivery,
        EconomySimulatorObjectTypes.ProductFlowPlan,
        EconomySimulatorObjectTypes.ProductFlowStep,
        EconomySimulatorObjectTypes.WorkforcePlan,
        EconomySimulatorObjectTypes.EquipmentPolicy,
        EconomySimulatorObjectTypes.MaintenancePlan,
        EconomySimulatorObjectTypes.QualityGate,
        EconomySimulatorObjectTypes.TestLabPlan,
        EconomySimulatorObjectTypes.ServiceOffering,
        EconomySimulatorObjectTypes.CostPolicy,
        EconomySimulatorObjectTypes.PayrollPlan,
        EconomySimulatorObjectTypes.TaxRuleSet,
        EconomySimulatorObjectTypes.Asset,
        EconomySimulatorObjectTypes.DepreciationSchedule,
        EconomySimulatorObjectTypes.Investment,
        EconomySimulatorObjectTypes.FinancingInstrument,
        EconomySimulatorObjectTypes.Invoice,
        EconomySimulatorObjectTypes.Receivable,
        EconomySimulatorObjectTypes.Payable,
        EconomySimulatorObjectTypes.EconomicEvent,
        EconomySimulatorObjectTypes.CashFlowSchedule,
        EconomySimulatorObjectTypes.ExternalSettlement,
        EconomySimulatorObjectTypes.RuleAutomationPlan,
        EconomySimulatorObjectTypes.RuleProcessTemplate,
        EconomySimulatorObjectTypes.RuleExecutionEvidence,
        EconomySimulatorObjectTypes.RuleState,
        EconomySimulatorObjectTypes.RuntimeIntervention,
        EconomySimulatorObjectTypes.PlannedFinancialSchedule,
        EconomySimulatorObjectTypes.PurchaseRequest,
        EconomySimulatorObjectTypes.InventoryReorderRequest,
        EconomySimulatorObjectTypes.CapexRequest,
        EconomySimulatorObjectTypes.ProductionGateExecution,
        EconomySimulatorObjectTypes.QualityGateExecution,
        EconomySimulatorObjectTypes.MaintenanceRequest,
        EconomySimulatorObjectTypes.LiquidityFacilityState,
        EconomySimulatorObjectTypes.ScenarioRun,
        EconomySimulatorObjectTypes.SimulationResult,
        EconomySimulatorObjectTypes.CalibrationSnapshot,
        EconomySimulatorObjectTypes.CalibrationRecommendation
    };

    public bool Contains(string objectType)
        => ObjectTypes.Contains(objectType);
}

public readonly record struct BusinessUnitId
{
    public BusinessUnitId(string value)
    {
        Value = EconomySimulatorIdentifiers.RequireId(value, nameof(value), "bu:");
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct EconomySimulatorModelId
{
    public EconomySimulatorModelId(string value)
    {
        Value = EconomySimulatorIdentifiers.RequireId(value, nameof(value), "esm:");
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct EconomySimulatorTemplateId
{
    public EconomySimulatorTemplateId(string value)
    {
        Value = EconomySimulatorIdentifiers.RequireId(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct EconomicEventId
{
    public EconomicEventId(string value)
    {
        Value = EconomySimulatorIdentifiers.RequireId(value, nameof(value), "evt:");
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ScenarioRunId
{
    public ScenarioRunId(string value)
    {
        Value = EconomySimulatorIdentifiers.RequireId(value, nameof(value), "run:");
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public static partial class EconomySimulatorIdentifiers
{
    private static readonly Regex IdPattern = CreateIdPattern();

    public static string NormalizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier segment is required.", nameof(value));
        }

        string lowered = value.Trim().ToLowerInvariant();
        string normalized = Regex.Replace(lowered, "[^a-z0-9]+", "-", RegexOptions.CultureInvariant).Trim('-');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Identifier segment must contain a letter or digit.", nameof(value));
        }

        return normalized;
    }

    public static EconomySimulatorModelId ModelId(SimulationBookId bookId, string projectCode, string versionOrDate)
        => new($"esm:{NormalizeSegment(bookId.Value)}:{NormalizeSegment(projectCode)}:{NormalizeSegment(versionOrDate)}");

    public static BusinessUnitId BusinessUnitId(string projectId, string path)
        => new($"bu:{NormalizeSegment(projectId)}:{NormalizePath(path)}");

    public static EconomicEventId EventId(string projectId, string correlationId, int sequence)
        => new($"evt:{NormalizeSegment(projectId)}:{NormalizeSegment(correlationId)}:{sequence:0000}");

    public static ScenarioRunId RunId(string projectId, ScenarioId scenarioId, DateTimeOffset startedAtUtc)
        => new($"run:{NormalizeSegment(projectId)}:{NormalizeSegment(scenarioId.Value)}:{startedAtUtc:yyyyMMddHHmmss}");

    internal static string RequireId(string value, string parameterName, string? requiredPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != value.Trim().Length)
        {
            throw new ArgumentException("Identifier is required and cannot contain leading or trailing whitespace.", parameterName);
        }

        if (requiredPrefix is not null && !value.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Identifier must start with '{requiredPrefix}'.", parameterName);
        }

        if (!IdPattern.IsMatch(value))
        {
            throw new ArgumentException("Identifier may contain lowercase letters, digits, '.', '-', '_', and ':' only.", parameterName);
        }

        return value;
    }

    private static string NormalizePath(string path)
        => string.Join(':', path.Split(['/', '\\', ':'], StringSplitOptions.RemoveEmptyEntries).Select(NormalizeSegment));

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateIdPattern();
}

public static class EconomySimulatorTokenIdFactory
{
    public static TokenId Budget(string projectId, string businessUnitPath, string period, string category)
        => Token("budget", projectId, businessUnitPath, period, category);

    public static TokenId Resource(string projectId, string resourceCode, string period)
        => Token("resource", projectId, resourceCode, period);

    public static TokenId ServiceHour(string projectId, string workOrderId, string period)
        => Token("service-hour", projectId, workOrderId, period);

    public static TokenId Product(string projectId, string sku)
        => Token("product", projectId, sku);

    public static TokenId ProductUnit(string projectId, string sku, string flowPlanId, string period)
        => Token("product-unit", projectId, sku, flowPlanId, period);

    public static TokenId InventoryLot(string projectId, string sku, string lotId)
        => Token("inventory", projectId, sku, lotId);

    public static TokenId Receivable(string projectId, string customerOrSegment, string period)
        => Token("receivable", projectId, customerOrSegment, period);

    public static TokenId Commitment(string projectId, string category, string period)
        => Token("commitment", projectId, category, period);

    public static TokenId Equity(string projectId, string classCode)
        => Token("equity", projectId, classCode);

    public static TokenId Debt(string projectId, string instrumentId)
        => Token("debt", projectId, instrumentId);

    private static TokenId Token(string tokenClass, string projectId, params string[] segments)
        => new($"es:{EconomySimulatorIdentifiers.NormalizeSegment(projectId)}:{EconomySimulatorIdentifiers.NormalizeSegment(tokenClass)}:{string.Join(':', segments.Select(EconomySimulatorIdentifiers.NormalizeSegment))}");
}
