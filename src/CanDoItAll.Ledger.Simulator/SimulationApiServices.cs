using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Ledger.Simulator;

public interface ISimulationProjectWorkspaceService
{
    IReadOnlyList<SimulationProjectSummary> ListProjects();

    SimulationProjectState? GetProject(string projectId);

    SimulationProjectState UpsertProject(SimulationProjectState project, DateTimeOffset? updatedAtUtc = null);
}

public sealed class InMemorySimulationProjectWorkspaceService(
    ITimeProvider timeProvider,
    ISimulationScenarioProjectFactory? scenarioProjectFactory = null) : ISimulationProjectWorkspaceService
{
    private readonly object sync = new();
    private readonly Dictionary<string, SimulationProjectState> projects = new(StringComparer.Ordinal);

    public IReadOnlyList<SimulationProjectSummary> ListProjects()
    {
        lock (sync)
        {
            EnsureScenarioProjects();
            return projects.Values
                .Select(project => project.Summary)
                .OrderBy(project => project.DisplayName, StringComparer.Ordinal)
                .ThenBy(project => project.ProjectId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public SimulationProjectState? GetProject(string projectId)
    {
        lock (sync)
        {
            EnsureScenarioProjects();
            return projects.TryGetValue(projectId, out SimulationProjectState? project) ? project : null;
        }
    }

    public SimulationProjectState UpsertProject(SimulationProjectState project, DateTimeOffset? updatedAtUtc = null)
    {
        SimulationProjectState updated = project with
        {
            Summary = project.Summary with { UpdatedAtUtc = updatedAtUtc ?? timeProvider.UtcNow }
        };

        lock (sync)
        {
            projects[updated.Summary.ProjectId] = updated;
        }

        return updated;
    }

    private void EnsureScenarioProjects()
    {
        if (scenarioProjectFactory is null)
        {
            return;
        }

        foreach (SimulationProjectState scenarioProject in scenarioProjectFactory.CreateScenarioProjects(timeProvider.UtcNow))
        {
            projects.TryAdd(scenarioProject.Summary.ProjectId, scenarioProject);
        }
    }
}

public interface ISimulationFunctionCatalogService
{
    SimulatorFunctionCatalog GetCatalog();
}

public sealed class SimulationFunctionCatalogService : ISimulationFunctionCatalogService
{
    private static readonly SimulatorFunctionCatalog Catalog = new(CreateFunctions(), CreateEndpoints(), CreateCfoCoverage());

    public SimulatorFunctionCatalog GetCatalog() => Catalog;

    private static IReadOnlyList<SimulatorFunctionMapItem> CreateFunctions()
        =>
        [
            Function("Project workspace", "SimulationProjectValidationService", "Validate", "Validate project, BU, BO, financial input, and run setup references.", "SimulationProjectValidationResult", "projects", "Implemented", "SimulationProjectServices.cs", "Data quality, governance", "Catches broken references and invalid inputs before runs."),
            Function("Project workspace", "SimulationProjectImportExportService", "CreateManifest", "Create deterministic project export manifest.", "SimulationProjectExportManifest", "projects", "Implemented", "SimulationProjectServices.cs", "Audit trail", "Hashes model, BUs, rules, inputs, and run setups."),
            Function("Project workspace", "SimulationProjectImportExportService", "PreviewImport", "Read and validate imported project manifest JSON.", "SimulationProjectImportPreview", "projects", "Implemented", "SimulationProjectServices.cs", "Data quality", "Returns schema warnings and validation errors."),
            Function("Project workspace", "SimulationProjectImportExportService", "BuildDeterministicRunSetupHash", "Hash semantic run setup inputs.", "Hash string", "projects", "Implemented", "SimulationProjectServices.cs", "Controls", "Ignores UI speed but includes scenario semantics."),
            Function("Scenario templates", "LedgerScenarioTemplateCatalog", "GetScenarios", "List S1/S2/S3 baseline scenarios and the reference assembly-native financial plan.", "ScenarioBusinessModelState", "scenario-templates", "Implemented", "LedgerScenarioTemplateCatalog.cs", "Planning and unit economics", "SaaS/services, bakery, hardware engineering, and reference assembly scenarios."),
            Function("Scenario templates", "LedgerScenarioEventCompiler", "Compile", "Compile business model assumptions, dated workbook schedules, and expected totals into economic events.", "LedgerScenarioEventCompileResult", "scenarios", "Implemented", "LedgerScenarioEventCompiler.cs", "Profitability, working capital", "Creates revenue, cash receipt, COGS, payroll, CapEx, approval-gated financial schedule, depreciation, and opex events."),
            Function("Scenario execution", "LedgerProjectionService", "BuildProjection", "Project cash flow, KPIs, and deficit issues from economic events.", "LedgerProjection", "analysis", "Implemented", "LedgerSimulatorEngine.cs", "Liquidity, profitability", "Splits planned, committed, actual, and simulated cash flows."),
            Function("Scenario execution", "LedgerScenarioRunner", "RunAsync", "Run a scenario request and produce run/result BO drafts.", "ScenarioRunResult", "scenarios", "Implemented", "LedgerSimulatorEngine.cs", "Scenario control", "Requires non-null scenario id and validates horizon."),
            Function("Actuals and calibration", "ActualTrackingService", "ValidateActualEntry", "Validate actual operational events before calibration.", "ActualEconomicEntryState", "analysis", "Implemented", "LedgerSimulatorEngine.cs", "Actual-vs-plan", "Rejects scenario-tagged actuals and invalid statuses."),
            Function("Actuals and calibration", "LedgerCalibrationService", "RecommendAssumptionUpdate", "Recommend assumption updates from planned vs actual metrics.", "CalibrationRecommendationState", "analysis", "Implemented", "LedgerSimulatorEngine.cs", "Forecast governance", "Produces confidence and evidence links."),
            Function("Run lifecycle", "InMemorySimulationRunLifecycleService", "StartRun", "Start a project run from a setup in automatic or manual stepping mode.", "SimulationRunCommandResult", "runs", "Implemented", "SimulationRunLifecycleServices.cs", "Operational control", "Automatic runs materialize to the configured horizon; manual runs wait at step 1."),
            Function("Run lifecycle", "InMemorySimulationRunLifecycleService", "StepRun", "Select, move previous, or move next on a run timeline.", "SimulationRunCommandResult", "runs", "Implemented", "SimulationRunLifecycleServices.cs", "Operational control", "Backward movement loads existing snapshots; forward movement materializes missing steps."),
            Function("Run lifecycle", "InMemorySimulationRunLifecycleService", "ApplyIntervention", "Apply a BO-backed manual-run intervention and invalidate future snapshots.", "SimulationRunInterventionCommandResult", "runs", "Implemented", "SimulationRunLifecycleServices.cs", "Operational control, controls", "Demand shocks and capacity-expansion requests are stored as runtime intervention BOs before future manual steps are materialized."),
            Function("Run lifecycle", "InMemorySimulationRunLifecycleService", "ApplyCommand", "Pause, resume, stop, or restart a run.", "SimulationRunCommandResult", "runs", "Implemented", "SimulationRunLifecycleServices.cs", "Operational control", "Invalid commands return visible validation errors."),
            Function("Run lifecycle", "InMemorySimulationRunLifecycleService", "ListRuns/GetRun", "List run registry records and selected run state.", "SimulationRunConsoleState", "runs", "Implemented", "SimulationRunLifecycleServices.cs", "Reporting", "Preserves failed and warning run visibility."),
            Function("Run observation", "SimulationRunObservationService", "BuildObservation", "Build run observation state from result, graphs, account snapshots, and rule evidence.", "SimulationRunObservationState", "runs", "Implemented", "SimulationProjectServices.cs", "Reporting, controls", "Includes output hashes, artifacts, graph rows, issues, BOs, and rule evidence."),
            Function("Rule automation", "LedgerSimulatorCommonRuleFactory", "Create*Rule", "Create common CFO-control account rules.", "AccountRuleDefinition", "metadata", "Implemented", "LedgerSimulatorRuleAutomation.cs", "Controls and governance", "Cashpool, reserve, purchase, payable, receivable, settlement, and CapEx rules."),
            Function("Rule automation", "LedgerSimulatorScenarioRulePackCatalog", "CreateRulePack", "Create scenario-specific rule process packs for each configured template.", "LedgerSimulatorScenarioRulePack", "scenario-templates", "Implemented", "LedgerSimulatorRuleAutomation.cs", "Controls and risk", "Maps rules to BUs, trigger object types, approval limits, and control purposes."),
            Function("Finance analysis", "SimulationFinanceAnalysisService", "Build", "Derive CFO analysis snapshot from a run console state.", "SimulationFinanceAnalysisSnapshot", "runs", "New API support", "SimulationApiServices.cs", "CFO analysis", "Packages liquidity, cash flow, controls, risk, KPI, source-map, and data-quality lenses."),
            Function("Finance analysis", "SimulationPeriodSummaryService", "Build", "Build quarter or year management summary from run graph outputs.", "SimulationPeriodSummarySnapshot", "runs", "New API support", "SimulationApiServices.cs", "Management reporting", "Aggregates cash flow, income, expenses, capital headroom, profit, and distribution slices."),
            Function("API metadata", "SimulationFunctionCatalogService", "GetCatalog", "Expose function/API/CFO coverage metadata.", "SimulatorFunctionCatalog", "metadata", "New API support", "SimulationApiServices.cs", "Governance", "Used by API clients and workbook mapping.")
        ];

    private static IReadOnlyList<SimulatorApiEndpointMapItem> CreateEndpoints()
        =>
        [
            Endpoint("Metadata", "GET", "/api/simulator/metadata/function-map", "Return function, endpoint, and CFO coverage catalog.", "ISimulationFunctionCatalogService", "SimulatorFunctionCatalog", "Governance", "Implemented"),
            Endpoint("Metadata", "GET", "/api/simulator/metadata/endpoints", "Return endpoint map only.", "ISimulationFunctionCatalogService", "SimulatorApiEndpointMapItem[]", "Governance", "Implemented"),
            Endpoint("Metadata", "GET", "/api/simulator/metadata/cfo-coverage", "Return CFO coverage checklist.", "ISimulationFunctionCatalogService", "SimulatorCfoCoverageItem[]", "Data quality", "Implemented"),
            Endpoint("Templates", "GET", "/api/simulator/templates", "List simulator BO, rule, schedule, and model templates with tags and dependencies.", "ILedgerSimulatorTemplateProvider", "LedgerSimulatorTemplateState[]", "Settings catalogue", "Implemented"),
            Endpoint("Templates", "GET", "/api/simulator/templates/{templateId}", "Read one simulator template including required templates, BOs, rules, parameters, and tags.", "ILedgerSimulatorTemplateProvider", "LedgerSimulatorTemplateState", "Settings catalogue", "Implemented"),
            Endpoint("Projects", "GET", "/api/simulator/projects", "List simulator project summaries.", "ISimulationProjectWorkspaceService", "SimulationProjectSummary[]", "Planning context", "Implemented"),
            Endpoint("Projects", "GET", "/api/simulator/projects/{projectId}", "Read a project state.", "ISimulationProjectWorkspaceService", "SimulationProjectState", "BU accountability", "Implemented"),
            Endpoint("Projects", "PUT", "/api/simulator/projects/{projectId}", "Upsert a project state after validation.", "ISimulationProjectWorkspaceService, ISimulationProjectValidationService", "SimulationProjectUpsertResponse", "Data quality", "Implemented"),
            Endpoint("Projects", "POST", "/api/simulator/projects/validate", "Validate a project without storing it.", "ISimulationProjectValidationService", "SimulationProjectValidationResult", "Data quality", "Implemented"),
            Endpoint("Projects", "GET", "/api/simulator/projects/{projectId}/export", "Export a project manifest.", "ISimulationProjectImportExportService", "SimulationProjectExportManifest", "Audit trail", "Implemented"),
            Endpoint("Projects", "POST", "/api/simulator/projects/import/preview", "Preview imported project JSON.", "ISimulationProjectImportExportService", "SimulationProjectImportPreview", "Data quality", "Implemented"),
            Endpoint("Projects", "POST", "/api/simulator/projects/import", "Import and store a valid project manifest.", "ISimulationProjectImportExportService, ISimulationProjectWorkspaceService", "SimulationProjectImportApiResponse", "Data quality", "Implemented"),
            Endpoint("Projects", "POST", "/api/simulator/projects/{projectId}/runs", "Start a run for a stored project/setup.", "ISimulationRunLifecycleService", "SimulationRunStartApiResponse", "Operational control", "Implemented"),
            Endpoint("Scenario templates", "GET", "/api/simulator/scenario-templates", "List baseline scenario template summaries.", "ILedgerScenarioTemplateCatalog", "SimulationScenarioTemplateSummary[]", "Planning and unit economics", "Implemented"),
            Endpoint("Scenario templates", "GET", "/api/simulator/scenario-templates/{scenarioCode}", "Read a scenario business model.", "ILedgerScenarioTemplateCatalog", "ScenarioBusinessModelState", "Planning and unit economics", "Implemented"),
            Endpoint("Scenario templates", "GET", "/api/simulator/scenario-templates/{scenarioCode}/rule-pack", "Read scenario rule pack.", "LedgerSimulatorScenarioRulePackCatalog", "LedgerSimulatorScenarioRulePack", "Controls", "Implemented"),
            Endpoint("Scenario templates", "GET", "/api/simulator/scenario-templates/{scenarioCode}/financial-schedules", "Read planned financial schedules, simulator periods, approvals, and timeline amounts for a scenario.", "ILedgerScenarioTemplateCatalog", "ScenarioFinancialScheduleState[]", "Planning and governance", "Implemented"),
            Endpoint("Scenarios", "POST", "/api/simulator/scenarios/compile", "Compile a named scenario into economic events.", "ILedgerScenarioEventCompiler", "LedgerScenarioEventCompileResult", "Profitability, working capital", "Implemented"),
            Endpoint("Scenarios", "POST", "/api/simulator/scenarios/run", "Run an explicit scenario request.", "ILedgerScenarioRunner", "ScenarioRunResult", "Liquidity", "Implemented"),
            Endpoint("Runs", "GET", "/api/simulator/runs", "List run registry records.", "ISimulationRunLifecycleService", "SimulationRunRegistryRecord[]", "Reporting", "Implemented"),
            Endpoint("Runs", "GET", "/api/simulator/runs/{runId}", "Read run console state.", "ISimulationRunLifecycleService", "SimulationRunConsoleState", "Reporting", "Implemented"),
            Endpoint("Runs", "POST", "/api/simulator/runs/{runId}/commands", "Apply lifecycle command.", "ISimulationRunLifecycleService", "SimulationRunCommandApiResponse", "Operational control", "Implemented"),
            Endpoint("Runs", "POST", "/api/simulator/runs/{runId}/steps", "Select, move previous, or move next on the run timeline.", "ISimulationRunLifecycleService", "SimulationRunStepApiResponse", "Operational control", "Implemented"),
            Endpoint("Runs", "POST", "/api/simulator/runs/{runId}/interventions", "Apply a manual-run demand, financial input, or CapEx/capacity intervention.", "ISimulationRunLifecycleService", "SimulationRunInterventionApiResponse", "Operational control", "Implemented"),
            Endpoint("Runs", "GET", "/api/simulator/runs/{runId}/observation", "Read run observation model.", "ISimulationRunLifecycleService", "SimulationRunObservationState", "Reporting", "Implemented"),
            Endpoint("Runs", "GET", "/api/simulator/runs/{runId}/analysis/finance", "Read CFO finance analysis snapshot.", "ISimulationFinanceAnalysisService", "SimulationFinanceAnalysisSnapshot", "CFO review", "Implemented"),
            Endpoint("Runs", "GET", "/api/simulator/runs/{runId}/analysis/period-summary", "Read quarter/year management summary.", "ISimulationPeriodSummaryService", "SimulationPeriodSummarySnapshot", "Management reporting", "Implemented"),
            Endpoint("Runs", "GET", "/api/simulator/runs/{runId}/analysis/source-map", "Read KPI/source map coverage.", "ISimulationRunLifecycleService", "SimulationOutputSourceRef[]", "Data lineage", "Implemented"),
            Endpoint("Runs", "GET", "/api/simulator/runs/{runId}/graphs", "Read run graph data.", "ISimulationRunLifecycleService", "LedgerGraphData[]", "Reporting", "Implemented"),
            Endpoint("Analysis", "POST", "/api/simulator/analysis/projection", "Build cash-flow projection from events.", "ILedgerProjectionService", "LedgerProjection", "Liquidity", "Implemented"),
            Endpoint("Analysis", "POST", "/api/simulator/analysis/actuals/validate", "Validate actual economic entry.", "IActualTrackingService", "ActualEconomicEntryState", "Actual-vs-plan", "Implemented"),
            Endpoint("Analysis", "POST", "/api/simulator/analysis/calibration/recommend", "Recommend assumption update.", "ILedgerCalibrationService", "CalibrationRecommendationState", "Forecast governance", "Implemented")
        ];

    private static IReadOnlyList<SimulatorCfoCoverageItem> CreateCfoCoverage()
        =>
        [
            Coverage("Liquidity and runway", "Opening/current/minimum cash, deficit date, reserve floor, run status.", "Project model, account snapshots, issues, run registry.", "/runs/{runId}/analysis/finance", "Implemented", "Treasury and insolvency decisions need professional review in real companies."),
            Coverage("Profitability and cash flow", "Revenue, expense, COGS, payroll, CapEx, depreciation, net cash flow.", "Scenario compiler, projections, graph totals.", "/analysis/projection; /runs/{runId}/graphs; /runs/{runId}/analysis/period-summary", "Implemented", "Accounting policy and revenue recognition are generic simulator assumptions."),
            Coverage("Working capital", "Collection delays, supplier terms, AR/AP follow-up, cash receipts.", "Financial inputs, scenario event dates, rule processes.", "/projects; /scenario-templates; /runs/{runId}/analysis/finance", "Implemented", "Entity-specific payment terms and credit risk need user data."),
            Coverage("Capital allocation", "CapEx plans, approval rules, tooling/equipment assumptions, and reference-plan investment/expense requests.", "Scenario templates, planned financial schedules, and CapEx rule packs.", "/scenario-templates/{scenarioCode}; /scenario-templates/{scenarioCode}/financial-schedules; /scenario-templates/{scenarioCode}/rule-pack", "Implemented", "NPV/IRR hurdle-rate models are not part of this API scope."),
            Coverage("Treasury, debt, and cash pooling", "Cash pool, reserve guard, external settlement, liquidity facility rules.", "Rule automation and output sources.", "/metadata/function-map; /scenario-templates/{scenarioCode}/rule-pack", "Partial", "Debt covenants and cross-border cash pooling require legal/tax review."),
            Coverage("Group and BU accountability", "Business units, account bindings, source owners, BU snapshots.", "SimulationProjectState and run observation.", "/projects/{projectId}; /runs/{runId}/observation", "Implemented", "Legal entity boundaries are not a substitute for statutory consolidation."),
            Coverage("Controls and governance", "Rule evidence, approval-gated planned schedule BOs, failed/skipped/processed actions, validation messages, runtime intervention BOs.", "Rule evidence, project validation, source map, manual run interventions, and schedule approvals.", "/scenario-templates/{scenarioCode}/financial-schedules; /runs/{runId}/analysis/finance; /runs/{runId}/interventions", "Implemented", "Controls reduce risk but do not replace audit or approvals policy."),
            Coverage("Risk and externalities", "Run issues, failed runs, warning counts, external clearing warnings.", "Run issues/events/top issues.", "/runs/{runId}; /runs/{runId}/analysis/finance", "Implemented", "Environmental, regulatory, and insurance costs are model inputs until provider rules are added."),
            Coverage("Management reporting", "KPI snapshots, graph totals, output hashes, artifacts, quarter/year summaries.", "Run console state, observation, and graph-derived period summary.", "/runs/{runId}/observation; /runs/{runId}/graphs; /runs/{runId}/analysis/period-summary", "Implemented", "Board packs still need narrative context and management judgement."),
            Coverage("Data quality", "Storage mode, sample/in-memory flags, validation messages, source-map coverage.", "Project summary, validation, artifacts, output sources.", "/runs/{runId}/analysis/finance; /metadata/cfo-coverage", "Implemented", "Sample data must not be treated as production forecast evidence.")
        ];

    private static SimulatorFunctionMapItem Function(
        string group,
        string sourceType,
        string functionName,
        string operation,
        string primaryData,
        string apiGroup,
        string implementationStatus,
        string sourceFile,
        string cfoLens,
        string notes)
        => new(group, sourceType, functionName, operation, primaryData, apiGroup, implementationStatus, sourceFile, cfoLens, notes);

    private static SimulatorApiEndpointMapItem Endpoint(
        string group,
        string method,
        string route,
        string purpose,
        string backingService,
        string primaryResponse,
        string cfoLens,
        string implementationStatus)
        => new(group, method, route, purpose, backingService, primaryResponse, cfoLens, implementationStatus);

    private static SimulatorCfoCoverageItem Coverage(
        string lens,
        string requiredInformation,
        string simulatorSource,
        string apiExposure,
        string status,
        string professionalReviewNote)
        => new(lens, requiredInformation, simulatorSource, apiExposure, status, professionalReviewNote);
}

public interface ISimulationFinanceAnalysisService
{
    SimulationFinanceAnalysisSnapshot Build(SimulationRunConsoleState run);
}

public sealed class SimulationFinanceAnalysisService(ITimeProvider timeProvider) : ISimulationFinanceAnalysisService
{
    public SimulationFinanceAnalysisSnapshot Build(SimulationRunConsoleState run)
    {
        decimal totalCurrentCash = run.Observation.AccountSnapshots.Sum(snapshot => snapshot.Balance.Value);
        decimal minimumCashFloor = ResolveMinimumCashFloor(run.Project);
        decimal revenue = SumGraphTotals(run, ScenarioGraphMetric.Revenue, "inflow") + SumGraphTotals(run, ScenarioGraphMetric.Revenue, "nonCashIncome");
        decimal expense = SumGraphTotals(run, ScenarioGraphMetric.Expense, "outflow") + SumGraphTotals(run, ScenarioGraphMetric.Expense, "nonCashExpense");
        decimal netCashFlow = SumGraphTotals(run, ScenarioGraphMetric.CashFlow, "netCashFlow");
        int kpiSourceMatches = run.Kpis.Count(kpi => run.OutputSources.Any(source => string.Equals(source.OutputCode, kpi.Code, StringComparison.Ordinal)));
        decimal sourceCoverage = run.Kpis.Count == 0 ? 0m : decimal.Round(kpiSourceMatches * 100m / run.Kpis.Count, 2);
        var summary = new SimulationFinanceSummary(
            totalCurrentCash,
            minimumCashFloor,
            totalCurrentCash - minimumCashFloor,
            revenue,
            expense,
            netCashFlow,
            run.Run.WarningCount,
            run.Run.ErrorCount,
            run.Kpis.Count,
            sourceCoverage);
        var dataQuality = new SimulationFinanceDataQuality(
            run.Project.Summary.StorageMode.ToString(),
            run.Project.Summary.StorageMode is SimulationProjectStorageMode.InMemory or SimulationProjectStorageMode.Sample,
            run.Project.ValidationMessages.Count,
            run.Observation.OutputHashes.Count,
            run.Observation.Artifacts.Count,
            BuildLimitationNotes(run));

        return new SimulationFinanceAnalysisSnapshot(
            run.Project.Summary.ProjectId,
            run.Run.RunId,
            timeProvider.UtcNow,
            Classify(run),
            summary,
            dataQuality,
            BuildCfoLenses(run, summary),
            [
                "Generic tax, payroll, legal, statutory accounting, and transfer-pricing assumptions require professional review before operational use.",
                "In-memory or sample-backed simulator data is useful for scenario analysis but is not production evidence unless the host replaces it with durable source systems."
            ]);
    }

    private static decimal SumGraphTotals(SimulationRunConsoleState run, ScenarioGraphMetric metric, string key)
        => run.Observation.Graphs
            .Where(graph => graph.Filter.Metric == metric)
            .Sum(graph => graph.Totals.TryGetValue(key, out decimal value) ? value : 0m);

    private static decimal ResolveMinimumCashFloor(SimulationProjectState project)
    {
        foreach (AccountRuleDefinition rule in project.BusinessObjectDefinitions
            .Where(definition => definition.RuleDefinition is not null)
            .Select(definition => definition.RuleDefinition!))
        {
            if (TryReadDecimal(rule, "reserve_floor", out decimal reserveFloor))
            {
                return reserveFloor;
            }

            if (TryReadDecimal(rule, "target_cash", out decimal targetCash))
            {
                return targetCash;
            }
        }

        return 0m;
    }

    private static bool TryReadDecimal(AccountRuleDefinition rule, string key, out decimal value)
    {
        if (rule.Variables.TryGetValue(key, out string? text)
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0m;
        return false;
    }

    private static string Classify(SimulationRunConsoleState run)
        => run.Run.Status switch
        {
            SimulationRunLifecycleStatus.Failed => "management concern; failed simulation run",
            SimulationRunLifecycleStatus.CompletedWithWarnings => "management concern; completed with warnings",
            SimulationRunLifecycleStatus.Running => "normal planning; active simulation run",
            _ => "normal planning; completed simulation run"
        };

    private static IReadOnlyList<string> BuildLimitationNotes(SimulationRunConsoleState run)
    {
        var notes = new List<string>();
        if (run.Project.Summary.StorageMode is SimulationProjectStorageMode.InMemory or SimulationProjectStorageMode.Sample)
        {
            notes.Add("Project state is in-memory/sample-backed unless the host registers durable workspace storage.");
        }

        if (run.Project.ValidationMessages.Count > 0)
        {
            notes.Add("Project includes validation messages that should be cleared before management reliance.");
        }

        notes.Add("Provider-specific tax, payroll, insurance, and legal rules are represented as generic simulator assumptions.");
        return notes;
    }

    private static IReadOnlyList<SimulationFinanceLensState> BuildCfoLenses(
        SimulationRunConsoleState run,
        SimulationFinanceSummary summary)
    {
        string collectionDelay = FormatInput(run, SimulationFinancialInputKind.CollectionDelayDays);
        string supplierTerms = FormatInput(run, SimulationFinancialInputKind.SupplierPaymentTermDays);
        int ruleCount = run.Project.BusinessObjectDefinitions.Count(definition => definition.RuleDefinition is not null);
        int interventionCount = run.Project.BusinessObjectDefinitions.Count(definition => definition.ObjectType == LedgerSimulatorObjectTypes.RuntimeIntervention);
        int failedActionCount = run.Observation.AccountSnapshots.Sum(snapshot => snapshot.FailedActionCount);
        int skippedActionCount = run.Observation.AccountSnapshots.Sum(snapshot => snapshot.SkippedActionCount);

        return
        [
            Lens("Liquidity and runway", summary.CashHeadroom >= 0m ? "Covered" : "Needs review", $"Cash headroom {summary.CashHeadroom.ToString("#,0.##", CultureInfo.InvariantCulture)} against floor {summary.MinimumCashFloor.ToString("#,0.##", CultureInfo.InvariantCulture)}.", ["totalCurrentCash", "minimumCashFloor", "cashHeadroom", "warningCount", "errorCount"], []),
            Lens("Profitability and cash flow", "Covered", $"Revenue {summary.Revenue.ToString("#,0.##", CultureInfo.InvariantCulture)}, expense {summary.Expense.ToString("#,0.##", CultureInfo.InvariantCulture)}, net cash flow {summary.NetCashFlow.ToString("#,0.##", CultureInfo.InvariantCulture)}.", ["revenue", "expense", "netCashFlow", "graphs"], []),
            Lens("Working capital", string.IsNullOrWhiteSpace(collectionDelay) && string.IsNullOrWhiteSpace(supplierTerms) ? "Partial" : "Covered", $"Collection delay {collectionDelay}; supplier terms {supplierTerms}.", ["financialInputs.collectionDelayDays", "financialInputs.supplierPaymentTermDays"], string.IsNullOrWhiteSpace(collectionDelay) || string.IsNullOrWhiteSpace(supplierTerms) ? ["Missing one or more working-capital assumptions."] : []),
            Lens("Capital allocation", "Covered", $"Rule definitions {ruleCount}; CapEx-related outputs are available through scenario templates and rule packs.", ["capexPlans", "capexApprovalRules", "businessObjectDefinitions"], []),
            Lens("Treasury and debt", "Partial", "Cashpool, reserve, external settlement, and liquidity facility rule families are modelled; covenant/debt analytics are not implemented.", ["cashpoolSweepRules", "reserveGuardRules", "externalSettlementRules"], ["Debt covenant, lender, and cash-pooling legal analysis remain outside this scope."]),
            Lens("Group and BU accountability", "Covered", $"{run.Project.BusinessUnits.Count} BUs and {run.Observation.AccountSnapshots.Count} account snapshots are available.", ["businessUnits", "accountSnapshots", "outputSources"], []),
            Lens("Controls and governance", failedActionCount == 0 ? "Covered" : "Needs review", $"{ruleCount} rules; {interventionCount} runtime intervention BOs; failed actions {failedActionCount}; skipped actions {skippedActionCount}.", ["ruleEvidence", "runtimeInterventions", "processedTriggerCount", "failedActionCount", "skippedActionCount"], failedActionCount > 0 ? ["Failed rule actions require review before relying on the run."] : []),
            Lens("Risk and externalities", run.Run.ErrorCount == 0 ? "Covered" : "Needs review", $"Warnings {run.Run.WarningCount}; errors {run.Run.ErrorCount}; top issues {run.TopIssues.Count}.", ["issues", "topIssues", "events"], run.Run.ErrorCount > 0 ? ["Failed runs should not be used for management decisions without remediation."] : []),
            Lens("Management reporting", summary.SourceMapCoveragePercent >= 80m ? "Covered" : "Partial", $"KPI count {summary.KpiCount}; source-map coverage {summary.SourceMapCoveragePercent.ToString("0.##", CultureInfo.InvariantCulture)}%.", ["kpis", "outputSources", "artifacts", "outputHashes"], summary.SourceMapCoveragePercent < 80m ? ["Some KPI outputs do not have source-map coverage."] : []),
            Lens("Data quality", "Covered", $"Storage mode {run.Project.Summary.StorageMode}; project validation messages {run.Project.ValidationMessages.Count}; artifacts {run.Observation.Artifacts.Count}.", ["storageMode", "validationMessages", "artifacts", "outputHashes"], [])
        ];
    }

    private static string FormatInput(SimulationRunConsoleState run, SimulationFinancialInputKind kind)
    {
        SimulationFinancialInputState? input = run.Project.FinancialInputProfiles
            .SelectMany(profile => profile.Inputs)
            .FirstOrDefault(candidate => candidate.Kind == kind);
        if (input is null)
        {
            return string.Empty;
        }

        return input.NumericValue?.ToString("0.####", CultureInfo.InvariantCulture)
            ?? input.TextValue
            ?? string.Empty;
    }

    private static SimulationFinanceLensState Lens(
        string lens,
        string status,
        string evidence,
        IReadOnlyList<string> dataFields,
        IReadOnlyList<string> gaps)
        => new(lens, status, evidence, dataFields, gaps);
}

public interface ISimulationPeriodSummaryService
{
    SimulationPeriodSummarySnapshot Build(SimulationRunConsoleState run, SimulationPeriodSummaryGranularity granularity);
}

public sealed class SimulationPeriodSummaryService : ISimulationPeriodSummaryService
{
    public SimulationPeriodSummarySnapshot Build(SimulationRunConsoleState run, SimulationPeriodSummaryGranularity granularity)
        => SimulationPeriodSummaryCalculator.Build(run, granularity);
}

public static class SimulationPeriodSummaryCalculator
{
    public static SimulationPeriodSummarySnapshot Build(
        SimulationRunConsoleState run,
        SimulationPeriodSummaryGranularity granularity)
    {
        IReadOnlyList<LedgerGraphPoint> cashPoints = PointsFor(run, ScenarioGraphMetric.CashFlow);
        IReadOnlyList<LedgerGraphPoint> revenuePoints = PointsFor(run, ScenarioGraphMetric.Revenue);
        IReadOnlyList<LedgerGraphPoint> expensePoints = PointsFor(run, ScenarioGraphMetric.Expense);
        decimal minimumCashFloor = ResolveMinimumCashFloor(run);
        decimal openingCash = ResolveOpeningCash(run, cashPoints);
        IReadOnlyList<SimulationPeriodSummaryPeriod> periods = BuildPeriods(
            run,
            granularity,
            cashPoints,
            revenuePoints,
            expensePoints,
            openingCash,
            minimumCashFloor);
        SimulationPeriodSummaryPeriod? lastPeriod = periods.LastOrDefault();
        DateOnly periodStart = periods.Count == 0 ? run.Setup.HorizonStartDate : periods[0].PeriodStart;
        DateOnly periodEnd = periods.Count == 0 ? run.Run.CurrentDate : periods[^1].PeriodEnd;

        return new SimulationPeriodSummarySnapshot(
            run.Project.Summary.ProjectId,
            run.Run.RunId,
            granularity,
            periodStart,
            periodEnd,
            run.Project.Summary.BaseCurrencyCode,
            minimumCashFloor,
            periods.Sum(period => period.CashFlow),
            periods.Sum(period => period.Income),
            periods.Sum(period => period.Expenses),
            periods.Sum(period => period.Profit),
            lastPeriod?.OwnCapital ?? Math.Max(0m, openingCash),
            lastPeriod?.ExternalCapital ?? Math.Max(0m, minimumCashFloor - openingCash),
            periods,
            BuildDistribution(revenuePoints, income: true),
            BuildDistribution(expensePoints, income: false));
    }

    private static IReadOnlyList<SimulationPeriodSummaryPeriod> BuildPeriods(
        SimulationRunConsoleState run,
        SimulationPeriodSummaryGranularity granularity,
        IReadOnlyList<LedgerGraphPoint> cashPoints,
        IReadOnlyList<LedgerGraphPoint> revenuePoints,
        IReadOnlyList<LedgerGraphPoint> expensePoints,
        decimal openingCash,
        decimal minimumCashFloor)
    {
        DateOnly summaryEnd = ResolveSummaryEnd(run, cashPoints, revenuePoints, expensePoints);
        IReadOnlyList<DateOnly> periodStarts = cashPoints
            .Concat(revenuePoints)
            .Concat(expensePoints)
            .Select(point => PeriodStartFor(point.PeriodStart, granularity))
            .DefaultIfEmpty(PeriodStartFor(run.Setup.HorizonStartDate, granularity))
            .Distinct()
            .Order()
            .ToArray();
        var periods = new List<SimulationPeriodSummaryPeriod>(periodStarts.Count);
        decimal cumulativeCashFlow = 0m;

        foreach (DateOnly periodStart in periodStarts)
        {
            DateOnly periodEnd = PeriodEndFor(periodStart, granularity, summaryEnd);
            IReadOnlyList<LedgerGraphPoint> periodCash = PointsInPeriod(cashPoints, periodStart, periodEnd);
            IReadOnlyList<LedgerGraphPoint> periodRevenue = PointsInPeriod(revenuePoints, periodStart, periodEnd);
            IReadOnlyList<LedgerGraphPoint> periodExpense = PointsInPeriod(expensePoints, periodStart, periodEnd);
            decimal cashInflow = periodCash.Sum(Inflow);
            decimal cashOutflow = periodCash.Sum(Outflow);
            decimal cashFlow = cashInflow - cashOutflow;
            decimal income = IncomeValue(periodRevenue);
            decimal expenses = ExpenseValue(periodExpense);
            decimal profit = income - expenses;
            cumulativeCashFlow += cashFlow;
            decimal endingCash = openingCash + cumulativeCashFlow;

            periods.Add(new SimulationPeriodSummaryPeriod(
                PeriodCode(periodStart, granularity),
                PeriodDisplayName(periodStart, granularity),
                periodStart,
                periodEnd,
                cashInflow,
                cashOutflow,
                cashFlow,
                income,
                expenses,
                profit,
                endingCash,
                Math.Max(0m, endingCash),
                Math.Max(0m, minimumCashFloor - endingCash)));
        }

        return periods;
    }

    private static IReadOnlyList<SimulationPeriodSummaryDistributionSlice> BuildDistribution(
        IReadOnlyList<LedgerGraphPoint> points,
        bool income)
    {
        decimal totalNonCashIncome = points.Sum(point => point.NonCashIncome);
        IReadOnlyList<(string CategoryCode, decimal Amount)> amounts = points
            .GroupBy(point => point.CategoryCode, StringComparer.Ordinal)
            .Select(group => (
                CategoryCode: group.Key,
                Amount: income
                    ? totalNonCashIncome > 0m
                        ? group.Sum(point => point.NonCashIncome)
                        : group.Sum(Inflow)
                    : group.Sum(point => Outflow(point) + point.NonCashExpense)))
            .Where(item => item.Amount > 0m)
            .OrderByDescending(item => item.Amount)
            .ThenBy(item => item.CategoryCode, StringComparer.Ordinal)
            .ToArray();
        decimal total = amounts.Sum(item => item.Amount);
        if (total <= 0m)
        {
            return [];
        }

        return amounts
            .Select(item => new SimulationPeriodSummaryDistributionSlice(
                item.CategoryCode,
                DisplayCategory(item.CategoryCode),
                item.Amount,
                decimal.Round(item.Amount * 100m / total, 2)))
            .ToArray();
    }

    private static IReadOnlyList<LedgerGraphPoint> PointsFor(SimulationRunConsoleState run, ScenarioGraphMetric metric)
        => run.Observation.Graphs
            .Where(graph => graph.Filter.Metric == metric)
            .SelectMany(graph => graph.Series.SelectMany(series => series.Points))
            .OrderBy(point => point.PeriodStart)
            .ThenBy(point => point.BusinessUnitId, StringComparer.Ordinal)
            .ThenBy(point => point.CategoryCode, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<LedgerGraphPoint> PointsInPeriod(
        IReadOnlyList<LedgerGraphPoint> points,
        DateOnly periodStart,
        DateOnly periodEnd)
        => points
            .Where(point => point.PeriodStart >= periodStart && point.PeriodStart <= periodEnd)
            .ToArray();

    private static DateOnly ResolveSummaryEnd(
        SimulationRunConsoleState run,
        IReadOnlyList<LedgerGraphPoint> cashPoints,
        IReadOnlyList<LedgerGraphPoint> revenuePoints,
        IReadOnlyList<LedgerGraphPoint> expensePoints)
    {
        DateOnly latestPointDate = cashPoints
            .Concat(revenuePoints)
            .Concat(expensePoints)
            .Select(point => point.PeriodStart)
            .DefaultIfEmpty(run.Run.CurrentDate)
            .Max();
        return latestPointDate > run.Run.CurrentDate ? latestPointDate : run.Run.CurrentDate;
    }

    private static decimal ResolveOpeningCash(SimulationRunConsoleState run, IReadOnlyList<LedgerGraphPoint> cashPoints)
    {
        if (TryReadDecimal(run.Project.Metadata, "openingCash", out decimal openingCash))
        {
            return openingCash;
        }

        decimal currentCash = run.Observation.AccountSnapshots.Sum(snapshot => snapshot.Balance.Value);
        return currentCash - cashPoints.Sum(point => Inflow(point) - Outflow(point));
    }

    private static decimal ResolveMinimumCashFloor(SimulationRunConsoleState run)
    {
        if (TryReadDecimal(run.Project.Metadata, "minimumCash", out decimal minimumCash))
        {
            return minimumCash;
        }

        foreach (AccountRuleDefinition rule in run.Project.BusinessObjectDefinitions
            .Where(definition => definition.RuleDefinition is not null)
            .Select(definition => definition.RuleDefinition!))
        {
            if (TryReadDecimal(rule.Variables, "reserve_floor", out decimal reserveFloor))
            {
                return reserveFloor;
            }

            if (TryReadDecimal(rule.Variables, "target_cash", out decimal targetCash))
            {
                return targetCash;
            }
        }

        return 0m;
    }

    private static bool TryReadDecimal(IReadOnlyDictionary<string, string> values, string key, out decimal value)
    {
        if (values.TryGetValue(key, out string? text)
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0m;
        return false;
    }

    private static decimal IncomeValue(IReadOnlyList<LedgerGraphPoint> points)
    {
        decimal nonCashIncome = points.Sum(point => point.NonCashIncome);
        return nonCashIncome > 0m ? nonCashIncome : points.Sum(Inflow);
    }

    private static decimal ExpenseValue(IReadOnlyList<LedgerGraphPoint> points)
        => points.Sum(point => Outflow(point) + point.NonCashExpense);

    private static decimal Inflow(LedgerGraphPoint point)
        => point.PlannedInflow + point.CommittedInflow + point.ActualInflow + point.SimulatedInflow;

    private static decimal Outflow(LedgerGraphPoint point)
        => point.PlannedOutflow + point.CommittedOutflow + point.ActualOutflow + point.SimulatedOutflow;

    private static DateOnly PeriodStartFor(DateOnly date, SimulationPeriodSummaryGranularity granularity)
        => granularity == SimulationPeriodSummaryGranularity.Year
            ? new DateOnly(date.Year, 1, 1)
            : new DateOnly(date.Year, ((date.Month - 1) / 3 * 3) + 1, 1);

    private static DateOnly PeriodEndFor(
        DateOnly periodStart,
        SimulationPeriodSummaryGranularity granularity,
        DateOnly summaryEnd)
    {
        DateOnly naturalEnd = granularity == SimulationPeriodSummaryGranularity.Year
            ? new DateOnly(periodStart.Year, 12, 31)
            : periodStart.AddMonths(3).AddDays(-1);
        return naturalEnd <= summaryEnd ? naturalEnd : summaryEnd;
    }

    private static string PeriodCode(DateOnly periodStart, SimulationPeriodSummaryGranularity granularity)
        => granularity == SimulationPeriodSummaryGranularity.Year
            ? periodStart.Year.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{periodStart.Year}-Q{((periodStart.Month - 1) / 3) + 1}");

    private static string PeriodDisplayName(DateOnly periodStart, SimulationPeriodSummaryGranularity granularity)
        => granularity == SimulationPeriodSummaryGranularity.Year
            ? periodStart.Year.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"Q{((periodStart.Month - 1) / 3) + 1} {periodStart.Year}");

    private static string DisplayCategory(string categoryCode)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(categoryCode.Replace('-', ' '));
}

public static class SimulationApiServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerSimulatorApi(this IServiceCollection services)
    {
        services.TryAddSingleton<ISimulationProjectWorkspaceService, InMemorySimulationProjectWorkspaceService>();
        services.TryAddSingleton<ISimulationFunctionCatalogService, SimulationFunctionCatalogService>();
        services.TryAddSingleton<ISimulationFinanceAnalysisService, SimulationFinanceAnalysisService>();
        services.TryAddSingleton<ISimulationPeriodSummaryService, SimulationPeriodSummaryService>();
        return services;
    }
}
