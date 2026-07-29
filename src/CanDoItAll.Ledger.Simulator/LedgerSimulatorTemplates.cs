namespace CanDoItAll.Ledger.Simulator;

public interface ILedgerSimulatorTemplateProvider
{
    IReadOnlyList<LedgerSimulatorTemplateState> GetTemplates();

    LedgerSimulatorTemplateState GetTemplate(string templateId);
}

public interface ILedgerSimulatorTemplateMaterializer
{
    ValueTask<LedgerSimulatorMaterializationPlan> BuildPlanAsync(
        LedgerSimulatorMaterializationRequest request,
        CancellationToken cancellationToken);
}

public sealed class LedgerSimulatorTemplateProvider : ILedgerSimulatorTemplateProvider
{
    private static readonly IReadOnlyList<LedgerSimulatorTemplateState> Templates = CreateTemplates();

    public IReadOnlyList<LedgerSimulatorTemplateState> GetTemplates()
        => Templates;

    public LedgerSimulatorTemplateState GetTemplate(string templateId)
        => Templates.FirstOrDefault(x => string.Equals(x.TemplateId, templateId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Ledger simulator template '{templateId}' was not found.");

    private static IReadOnlyList<LedgerSimulatorTemplateState> CreateTemplates()
        => new[]
        {
            Department("common.project-treasury.v1", "Project treasury", BusinessUnitAddressRole.Treasury, BusinessUnitKind.Project, "Central project cash and funding bucket."),
            Department("common.external-clearing.v1", "External clearing", BusinessUnitAddressRole.ExternalClearing, BusinessUnitKind.ExternalBoundary, "Auditable boundary for cash entering or leaving the internal ledger."),
            Department("common.reserve.v1", "Reserve", BusinessUnitAddressRole.Reserve, BusinessUnitKind.Reserve, "Risk reserve and runway buffer."),
            Department("dept.marketing.v1", "Marketing", BusinessUnitAddressRole.Expense, BusinessUnitKind.Department, "Paid ads, content, events, brand, and agencies."),
            Department("dept.sales.v1", "Sales", BusinessUnitAddressRole.Revenue, BusinessUnitKind.Department, "Sales team, commissions, pipeline, and revenue operations."),
            Department("dept.legal.v1", "Legal", BusinessUnitAddressRole.Expense, BusinessUnitKind.Department, "Legal counsel, contracts, compliance, and governance."),
            Department("dept.finance-admin.v1", "Finance and admin", BusinessUnitAddressRole.Expense, BusinessUnitKind.Department, "Accounting, bookkeeping, bank fees, and administration."),
            Department("dept.hr-payroll.v1", "HR and payroll", BusinessUnitAddressRole.Payroll, BusinessUnitKind.Department, "Payroll pool and people operations."),
            Department("dept.it-cloud.v1", "IT and cloud", BusinessUnitAddressRole.Expense, BusinessUnitKind.Department, "SaaS subscriptions, cloud hosting, and internal IT."),
            Department("dept.rd.v1", "R&D", BusinessUnitAddressRole.Capex, BusinessUnitKind.Department, "Product development and research."),
            Department("dept.operations.v1", "Operations", BusinessUnitAddressRole.Expense, BusinessUnitKind.Department, "Operations and delivery."),
            Department("dept.support.v1", "Support", BusinessUnitAddressRole.Expense, BusinessUnitKind.Department, "Customer support and success."),
            Department("dept.logistics.v1", "Logistics", BusinessUnitAddressRole.Expense, BusinessUnitKind.Department, "Warehousing, shipping, and fulfillment."),
            Department("dept.tax.v1", "Tax", BusinessUnitAddressRole.Tax, BusinessUnitKind.TaxAuthority, "Tax liabilities and authority payments."),
            Model("model.service-project.v1", "Service project", RevenueStreamKind.Service),
            Model("model.product-project.v1", "Product project", RevenueStreamKind.Product),
            Model("model.hybrid-service-product.v1", "Hybrid service and product", RevenueStreamKind.Hybrid),
            Model("model.subscription-saas.v1", "Subscription SaaS", RevenueStreamKind.Subscription),
            Model("model.marketplace-commission.v1", "Marketplace commission", RevenueStreamKind.MarketplaceCommission),
            RuleTemplate(
                "rule.cashpool-sweep.v1",
                "Cashpool sweep",
                LedgerSimulatorRuleProcessKind.CashpoolSweep,
                LedgerSimulatorObjectTypes.RuleState,
                "Moves surplus cash into treasury while preserving an operating buffer.",
                "Business Objects/Rule state",
                ["common.project-treasury.v1", "common.reserve.v1"],
                ["cash", "cashpool", "treasury", "automation", "rule"]),
            RuleTemplate(
                "rule.purchase-approval.v1",
                "Purchase approval",
                LedgerSimulatorRuleProcessKind.PurchaseApproval,
                LedgerSimulatorObjectTypes.PurchaseRequest,
                "Approves BU purchase requests when authority and reserve checks pass.",
                "Rule Packs/Approval policies",
                ["common.reserve.v1"],
                ["approval", "purchase", "expense", "bu", "rule"]),
            RuleTemplate(
                "rule.capex-approval.v1",
                "CapEx approval",
                LedgerSimulatorRuleProcessKind.CapexApproval,
                LedgerSimulatorObjectTypes.CapexRequest,
                "Approves investment requests when amount, cash reserve, and business-case thresholds pass.",
                "Rule Packs/Approval policies",
                ["common.reserve.v1", "rule.purchase-approval.v1"],
                ["approval", "capex", "investment", "management", "rule"]),
            RuleTemplate(
                "financial.planned-expense-request.v1",
                "Planned expense request",
                LedgerSimulatorRuleProcessKind.PurchaseApproval,
                LedgerSimulatorObjectTypes.PlannedFinancialSchedule,
                "Schedules one-off or monthly BU expenses with source evidence and dispatch timing.",
                "Financial Schedules/Planned expenses",
                ["rule.purchase-approval.v1", "rule.capex-approval.v1"],
                ["planned-expense", "financial-schedule", "timeline", "approval", "excel"]),
            RuleTemplate(
                "financial.approval-workflow.v1",
                "Approval workflow",
                LedgerSimulatorRuleProcessKind.PurchaseApproval,
                LedgerSimulatorObjectTypes.PurchaseRequest,
                "Connects BU self-approval, management approval, and downstream payable dispatch.",
                "Rule Packs/Approval policies",
                ["rule.purchase-approval.v1", "rule.capex-approval.v1", "common.reserve.v1"],
                ["workflow", "approval", "dependencies", "bo", "rule"])
        };

    private static LedgerSimulatorTemplateState Department(
        string id,
        string displayName,
        BusinessUnitAddressRole role,
        BusinessUnitKind kind,
        string description)
        => new(
            id,
            "1.0",
            displayName,
            LedgerSimulatorTemplateKind.Department,
            description,
            new[]
            {
                Parameter("path", "Path", "string", true, LedgerSimulatorIdentifiers.NormalizeSegment(displayName), null, "Business unit path."),
                Parameter("displayName", "Display name", "string", true, displayName, null, "Business unit display name.")
            },
            new[]
            {
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.BusinessUnit, role.ToString(), "bu:{projectId}:{path}", new Dictionary<string, string> { ["kind"] = kind.ToString(), ["role"] = role.ToString() }),
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.BusinessUnitAddressBinding, role.ToString(), "buaddr:{projectId}:{path}:{role}:{addressSuffix}", new Dictionary<string, string> { ["isPrimary"] = role == BusinessUnitAddressRole.Treasury ? "true" : "false" })
            },
            Array.Empty<string>(),
            new Dictionary<string, string> { ["role"] = role.ToString(), ["kind"] = kind.ToString() },
            Metadata(
                $"Business Objects/{(kind == BusinessUnitKind.Project ? "Core company" : "Departments")}",
                ["business-unit", "department", role.ToString().ToLowerInvariant(), kind.ToString().ToLowerInvariant()],
                [],
                [LedgerSimulatorObjectTypes.BusinessUnit, LedgerSimulatorObjectTypes.BusinessUnitAddressBinding],
                $"Creates a {role} address binding and BU settings for {displayName}."));

    private static LedgerSimulatorTemplateState Model(string id, string displayName, RevenueStreamKind revenueKind)
        => new(
            id,
            "1.0",
            displayName,
            LedgerSimulatorTemplateKind.Composite,
            $"Creates a {displayName.ToLowerInvariant()} model with treasury, external clearing, departments, revenue stream, cost, payroll, tax, asset, and scenario defaults.",
            new[]
            {
                Parameter("projectName", "Project name", "string", true, displayName, null, "Human readable model name."),
                Parameter("baseCurrencyCode", "Base currency", "string", true, "USD", null, "Main reporting currency.")
            },
            new[]
            {
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.Model, "Model", "esm:{projectId}", new Dictionary<string, string>()),
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.RevenueStream, revenueKind.ToString(), "rev:{projectId}:{revenueCode}", new Dictionary<string, string> { ["kind"] = revenueKind.ToString() }),
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.PayrollPlan, "Payroll", "payroll:{projectId}:default", new Dictionary<string, string>()),
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.TaxRuleSet, "Tax", "tax:{projectId}:generic:default:1", new Dictionary<string, string>())
            },
            new[] { "generic-payroll.v1", "generic-tax.v1", "straight-line-depreciation.v1" },
            new Dictionary<string, string> { ["revenueKind"] = revenueKind.ToString(), ["baseCurrencyCode"] = "USD" },
            Metadata(
                "Run Setups/Scenario models",
                ["scenario", "model", "revenue", revenueKind.ToString().ToLowerInvariant(), "payroll", "tax"],
                ["generic-payroll.v1", "generic-tax.v1", "straight-line-depreciation.v1"],
                [LedgerSimulatorObjectTypes.Model, LedgerSimulatorObjectTypes.RevenueStream, LedgerSimulatorObjectTypes.PayrollPlan, LedgerSimulatorObjectTypes.TaxRuleSet],
                $"Creates the core model, revenue stream, payroll, and tax BOs for {displayName}."));

    private static LedgerSimulatorTemplateState RuleTemplate(
        string id,
        string displayName,
        LedgerSimulatorRuleProcessKind processKind,
        string triggerObjectType,
        string description,
        string treePath,
        IReadOnlyList<string> requiredTemplates,
        IReadOnlyList<string> tags)
        => new(
            id,
            "1.0",
            displayName,
            LedgerSimulatorTemplateKind.Custom,
            description,
            new[]
            {
                Parameter("ruleNamespace", "Rule namespace", "string", true, "rules.company", null, "Namespace for generated account rules."),
                Parameter("ownerBusinessUnitId", "Owner BU", "businessUnitId", true, "treasury", null, "Business unit that owns this automation."),
                Parameter("approvalLimit", "Approval limit", "amount", false, "10000", "currency", "Maximum amount this rule may approve.")
            },
            new[]
            {
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.RuleProcessTemplate, processKind.ToString(), $"rule-process:{{projectId}}:{LedgerSimulatorIdentifiers.NormalizeSegment(displayName)}", new Dictionary<string, string> { ["kind"] = processKind.ToString() }),
                new LedgerSimulatorTemplateOutput(LedgerSimulatorObjectTypes.RuleState, triggerObjectType, $"rule-state:{{projectId}}:{LedgerSimulatorIdentifiers.NormalizeSegment(displayName)}", new Dictionary<string, string> { ["triggerObjectType"] = triggerObjectType })
            },
            requiredTemplates,
            new Dictionary<string, string> { ["processKind"] = processKind.ToString(), ["triggerObjectType"] = triggerObjectType },
            Metadata(treePath, tags, requiredTemplates, [triggerObjectType, LedgerSimulatorObjectTypes.RuleState, LedgerSimulatorObjectTypes.RuleProcessTemplate], description));

    private static LedgerSimulatorTemplateParameter Parameter(
        string name,
        string displayName,
        string valueType,
        bool required,
        string? defaultValue,
        string? unitCode,
        string description)
        => new(name, displayName, valueType, required, defaultValue, unitCode, null, description);

    private static Dictionary<string, string> Metadata(
        string treePath,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> requiredTemplates,
        IReadOnlyList<string> requiredObjectTypes,
        string configurationSummary)
        => new(StringComparer.Ordinal)
        {
            ["treePath"] = treePath,
            ["tags"] = string.Join(',', tags),
            ["requiresTemplates"] = string.Join(',', requiredTemplates),
            ["requiresObjectTypes"] = string.Join(',', requiredObjectTypes),
            ["configurationSummary"] = configurationSummary
        };
}

public sealed class LedgerSimulatorTemplateMaterializer(
    ILedgerSimulatorTemplateProvider templateProvider,
    ILedgerSimulatorJsonSerializer serializer) : ILedgerSimulatorTemplateMaterializer
{
    public ValueTask<LedgerSimulatorMaterializationPlan> BuildPlanAsync(
        LedgerSimulatorMaterializationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LedgerSimulatorTemplateState template = templateProvider.GetTemplate(request.TemplateId);
        string path = GetParameter(request, template, "path", LedgerSimulatorIdentifiers.NormalizeSegment(template.DisplayName));
        string displayName = GetParameter(request, template, "displayName", template.DisplayName);
        var businessUnitId = LedgerSimulatorIdentifiers.BusinessUnitId(request.ProjectId, path);
        var warnings = new List<string>();
        var drafts = new List<LedgerSimulatorBusinessObjectCommandDraft>();
        var tokenDefinitions = new List<LedgerSimulatorTokenDefinitionState>();

        if (template.Kind == LedgerSimulatorTemplateKind.Department)
        {
            BusinessUnitKind kind = Enum.Parse<BusinessUnitKind>(template.Defaults["kind"]);
            BusinessUnitAddressRole role = Enum.Parse<BusinessUnitAddressRole>(template.Defaults["role"]);
            var unit = new BusinessUnitState(
                request.ProjectId,
                businessUnitId,
                displayName,
                kind,
                null,
                request.PrimaryAddress,
                path,
                role is BusinessUnitAddressRole.Expense or BusinessUnitAddressRole.Payroll or BusinessUnitAddressRole.Tax ? path : null,
                role == BusinessUnitAddressRole.Revenue ? path : null,
                null,
                "USD",
                kind is BusinessUnitKind.ExternalBoundary or BusinessUnitKind.Counterparty,
                role == BusinessUnitAddressRole.SimulationOnly,
                BusinessUnitStatus.Active,
                new[] { request.PrimaryAddress },
                new Dictionary<string, string> { ["templateId"] = template.TemplateId },
                new Dictionary<string, string>());
            drafts.Add(serializer.CreateDraft(LedgerSimulatorObjectTypes.BusinessUnit, unit, request.PrimaryAddress));

            var binding = new BusinessUnitAddressBindingState(
                request.ProjectId,
                businessUnitId,
                request.PrimaryAddress,
                role,
                role.ToString(),
                true,
                BusinessUnitAddressBindingStatus.Active,
                DateTimeOffset.UnixEpoch,
                null,
                null,
                null,
                displayName,
                new Dictionary<string, string> { ["templateId"] = template.TemplateId });
            drafts.Add(serializer.CreateDraft(LedgerSimulatorObjectTypes.BusinessUnitAddressBinding, binding, request.PrimaryAddress));
        }
        else
        {
            warnings.Add("Composite model templates produce a preview plan in P0; callers can materialize department templates individually.");
            tokenDefinitions.Add(new LedgerSimulatorTokenDefinitionState(
                request.ProjectId,
                LedgerSimulatorTokenIdFactory.Budget(request.ProjectId, "treasury", "monthly", "operating"),
                LedgerSimulatorTokenClass.BudgetAllocation,
                "Operating budget",
                "budget-unit",
                LedgerSimulatorIdentifiers.BusinessUnitId(request.ProjectId, "treasury"),
                $"es:{LedgerSimulatorIdentifiers.NormalizeSegment(request.ProjectId)}:budget",
                false,
                true,
                false,
                null,
                "USD",
                null,
                null,
                new Dictionary<string, string> { ["templateId"] = template.TemplateId }));
        }

        return ValueTask.FromResult(new LedgerSimulatorMaterializationPlan(
            request.ProjectId,
            template.TemplateId,
            drafts,
            tokenDefinitions,
            warnings,
            new Dictionary<string, string>
            {
                ["draftCount"] = drafts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["tokenDefinitionCount"] = tokenDefinitions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));
    }

    private static string GetParameter(
        LedgerSimulatorMaterializationRequest request,
        LedgerSimulatorTemplateState template,
        string name,
        string fallback)
        => request.Parameters.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : template.Parameters.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal))?.DefaultValue ?? fallback;

}
