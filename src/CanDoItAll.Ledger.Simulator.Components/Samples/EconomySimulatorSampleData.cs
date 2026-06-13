using CanDoItAll.Economy.Simulator;
using CanDoItAll.Economy.Simulator.Components.Models;

namespace CanDoItAll.Economy.Simulator.Components.Samples;

public static class EconomySimulatorSampleData
{
    private static readonly BusinessUnitId Treasury = EconomySimulatorIdentifiers.BusinessUnitId("project-alpha", "treasury");
    private static readonly BusinessUnitId Sales = EconomySimulatorIdentifiers.BusinessUnitId("project-alpha", "sales");
    private static readonly BusinessUnitId Operations = EconomySimulatorIdentifiers.BusinessUnitId("project-alpha", "operations");
    private static readonly BusinessUnitId ExternalClearing = EconomySimulatorIdentifiers.BusinessUnitId("project-alpha", "external-clearing");
    private static readonly Address TreasuryAccount = Address.FromPublicKey("project-alpha-treasury"u8.ToArray());
    private static readonly Address SalesAccount = Address.FromPublicKey("project-alpha-sales"u8.ToArray());
    private static readonly Address OperationsAccount = Address.FromPublicKey("project-alpha-operations"u8.ToArray());
    private static readonly Address ExternalClearingAccount = Address.FromPublicKey("project-alpha-external-clearing"u8.ToArray());
    private const string TreasuryAddress = "cdia1...7f9a21";
    private const string SalesAddress = "cdia1...18be44";
    private const string OperationsAddress = "cdia1...b9310c";

    public static EconomySimulatorViewModel Create()
    {
        var kpis = new[]
        {
            new EconomyKpiItem("Cash", "$184,200", "success", "Real + committed ledger balance"),
            new EconomyKpiItem("Runway", "7.4 mo", "info", "Based on planned outflows"),
            new EconomyKpiItem("First deficit", "None", "success", "Downside scenario stays funded"),
            new EconomyKpiItem("Gross margin", "68%", "accent", "Product + service blend")
        };

        var cashFlow = new[]
        {
            Point(new DateOnly(2026, 6, 1), Treasury, "opening", 0, 0, 184200),
            Point(new DateOnly(2026, 6, 15), Sales, "invoice", 42000, 0, 226200),
            Point(new DateOnly(2026, 6, 25), Operations, "payroll", 0, 36000, 190200),
            Point(new DateOnly(2026, 7, 5), Operations, "cloud", 0, 6000, 184200),
            Point(new DateOnly(2026, 7, 12), Sales, "collection", 58000, 0, 242200)
        };

        return new EconomySimulatorViewModel(
            "project-alpha",
            "Project Alpha",
            "In-memory ledger + simulator projection cache",
            kpis,
            new[]
            {
                new BusinessUnitNodeItem("treasury", "Project treasury", "Treasury", "Active", Amount.FromInt64(184200)),
                new BusinessUnitNodeItem("sales", "Sales", "Revenue", "Active", Amount.FromInt64(42000)),
                new BusinessUnitNodeItem("operations", "Operations", "Expense", "Active", Amount.FromInt64(0)),
                new BusinessUnitNodeItem("external-clearing", "External clearing", "ExternalClearing", "Active", Amount.FromInt64(0))
            },
            new[]
            {
                new AddressBindingItem("Project treasury", "Treasury", "cdia1...7f9a21", "Active", "2026-01-01 onward"),
                new AddressBindingItem("Sales", "Revenue", "cdia1...18be44", "Active", "2026-01-01 onward"),
                new AddressBindingItem("Operations", "Expense", "cdia1...b9310c", "Rotated", "2026-01-01 to 2026-04-30"),
                new AddressBindingItem("External clearing", "ExternalClearing", "cdia1...c44091", "Active", "2026-01-01 onward")
            },
            new[]
            {
                new TemplateOptionItem("model.hybrid-service-product.v1", "Hybrid service + product", "Composite", "Selected", "Treasury, sales, product, support, payroll, tax"),
                new TemplateOptionItem("dept.marketing.v1", "Marketing department", "Department", "Ready", "Expense BU and address binding"),
                new TemplateOptionItem("dept.it-cloud.v1", "IT and cloud", "Department", "Ready", "Cloud cost policy and recurring events"),
                new TemplateOptionItem("common.external-clearing.v1", "External clearing", "Department", "Required", "Settlement boundary")
            },
            new[]
            {
                new EventTimelineItem(new DateOnly(2026, 6, 15), "evt:project-alpha:invoice:0001", "Revenue", "Invoiced", "enterprise-subscription", Amount.FromInt64(42000), "91%"),
                new EventTimelineItem(new DateOnly(2026, 6, 25), "evt:project-alpha:payroll:0001", "Payroll", "Planned", "payroll-net", Amount.FromInt64(36000), "98%"),
                new EventTimelineItem(new DateOnly(2026, 7, 5), "evt:project-alpha:cloud:0001", "Expense", "Committed", "cloud", Amount.FromInt64(6000), "96%"),
                new EventTimelineItem(new DateOnly(2026, 7, 12), "evt:project-alpha:collection:0001", "Revenue", "Simulated", "cash-collection", Amount.FromInt64(58000), "88%")
            },
            cashFlow,
            CreateGraphs(),
            new[]
            {
                new ScenarioIssueItem("warning", "reserve-threshold", new DateOnly(2026, 8, 20), "Operations", "Reserve falls below two months in downside case."),
                new ScenarioIssueItem("info", "collection-delay", new DateOnly(2026, 7, 12), "Sales", "Collections are 4 days slower than template assumption.")
            },
            new[]
            {
                new ActualEntryItem("actual-001", new DateOnly(2026, 6, 16), "bank import", "Validated", Amount.FromInt64(40100)),
                new ActualEntryItem("actual-002", new DateOnly(2026, 6, 25), "user", "Needs match", Amount.FromInt64(35900))
            },
            new[]
            {
                new ReconciliationItem("match-001", "actual-001", "evt:project-alpha:invoice:0001", "-$1,900", "Review"),
                new ReconciliationItem("match-002", "actual-002", "evt:project-alpha:payroll:0001", "$100", "Matched")
            },
            new[]
            {
                new CalibrationRecommendationState("project-alpha", "calib:project-alpha:collection-delay:20260731", "cashCollectionDelayDays", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 31), "15", "19", 0.72m, new[] { "actual-001" }, "needs-review", new Dictionary<string, string>()),
                new CalibrationRecommendationState("project-alpha", "calib:project-alpha:cloud-runrate:20260731", "cloudMonthlyRunRate", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 31), "5000", "6100", 0.81m, new[] { "evt:project-alpha:cloud:0001" }, "needs-review", new Dictionary<string, string>())
            },
            new[]
            {
                new ObjectDetailItem("Transaction", "evt:project-alpha:collection:0001", "Simulated", "Cash collection for enterprise subscription."),
                new ObjectDetailItem("BusinessObject", "bu:project-alpha:sales", "Active", "Revenue center with one active address binding."),
                new ObjectDetailItem("Token", "es:project-alpha:budget:sales:2026-q3:travel", "Non-cash", "Budget authority token."),
                new ObjectDetailItem("Address", "cdia1...18be44", "Active", "Revenue address for Sales.")
            });
    }

    public static SimulationProjectManagementViewModel CreateProjectManagement(string storageMode = "InMemory")
    {
        SimulationProjectState selectedProject = CreateProjectState(storageMode);
        var serializer = new EconomySimulatorJsonSerializer();
        var validation = new SimulationProjectValidationService();
        var importExport = new SimulationProjectImportExportService(serializer, validation);
        SimulationProjectExportManifest manifest = importExport.CreateManifest(selectedProject, DateTimeOffset.Parse("2026-05-14T12:00:00Z"));
        SimulationProjectImportPreview importPreview = importExport.PreviewImport(importExport.SerializeManifest(manifest));

        var projects = new[]
        {
            selectedProject.Summary,
            new SimulationProjectSummary(
                "project-beta",
                "Project Beta",
                SimulationProjectStatus.NeedsAttention,
                "USD",
                new DateOnly(2026, 4, 1),
                new DateOnly(2027, 3, 31),
                DateTimeOffset.Parse("2026-05-12T08:30:00Z"),
                5,
                selectedProject.Summary.StorageMode,
                DateTimeOffset.Parse("2026-05-13T16:00:00Z")),
            new SimulationProjectSummary(
                "project-gamma",
                "Project Gamma",
                SimulationProjectStatus.Draft,
                "EUR",
                new DateOnly(2026, 7, 1),
                new DateOnly(2026, 12, 31),
                null,
                0,
                SimulationProjectStorageMode.Imported,
                DateTimeOffset.Parse("2026-05-10T09:15:00Z"))
        };

        return new SimulationProjectManagementViewModel(projects, selectedProject, manifest, importPreview);
    }

    public static SimulationRunObservationViewModel CreateRunObservation(string storageMode = "InMemory")
    {
        SimulationProjectState project = CreateProjectState(storageMode);
        SimulationRunSetupState setup = project.RunSetups[0];
        DateTimeOffset started = DateTimeOffset.Parse("2026-05-14T10:30:00Z");
        var run = new ScenarioRunState(
            project.Summary.ProjectId,
            setup.ScenarioId,
            EconomySimulatorIdentifiers.RunId(project.Summary.ProjectId, setup.ScenarioId, started).Value,
            setup.ScenarioName,
            null,
            null,
            started,
            setup.HorizonStartDate,
            setup.HorizonEndDate,
            ScenarioRunMode.DraftAndScenarioPosting.ToString(),
            setup.Seed,
            setup.AssumptionOverrides,
            setup.IncludedTemplateIds,
            setup.IncludedBusinessUnits,
            "Completed",
            "result:project-alpha:baseline",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["uiSpeed"] = setup.UiSpeed.ToString()
            });
        IReadOnlyList<CashFlowProjectionPoint> cashFlow =
        [
            Point(new DateOnly(2026, 6, 1), Treasury, "opening", 0, 0, 184200),
            Point(new DateOnly(2026, 6, 15), Sales, "cash-receipts", 42000, 0, 226200),
            Point(new DateOnly(2026, 6, 25), Operations, "payroll", 0, 36000, 190200),
            Point(new DateOnly(2026, 7, 12), Sales, "cash-receipts", 58000, 0, 242200)
        ];
        IReadOnlyList<SimulationIssue> issues =
        [
            new(
                "issue:reserve-threshold",
                "reserve-threshold",
                "warning",
                new DateOnly(2026, 8, 20),
                Operations,
                null,
                null,
                Amount.FromInt64(26000),
                "Reserve falls below two months in downside case.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["suggestedAction"] = "Delay non-critical cloud expansion."
                })
        ];
        var result = new SimulationResultState(
            project.Summary.ProjectId,
            setup.ScenarioId,
            run.RunId,
            DateTimeOffset.Parse("2026-05-14T10:30:05Z"),
            setup.HorizonStartDate,
            setup.HorizonEndDate,
            Amount.FromInt64(184200),
            Amount.FromInt64(242200),
            Amount.FromInt64(184200),
            null,
            null,
            Amount.Zero,
            Amount.FromInt64(65000),
            cashFlow,
            issues,
            new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["closingCash"] = 242200m,
                ["minimumCash"] = 184200m,
                ["deficitCount"] = 0m
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["result"] = "sample-result-hash-8b1f",
                ["cashflow"] = "sample-cashflow-hash-4a2e"
            },
            new Dictionary<string, string>());
        IReadOnlyList<RuleExecutionEvidenceState> evidence =
        [
            new(
                project.Summary.ProjectId,
                setup.ScenarioId.Value,
                "alpha.cashpool-sweep.v1",
                AccountRuleActionKind.Payment.ToString(),
                AccountRuleExecutionStatus.Executed.ToString(),
                new BusinessObjectId("bo:project-alpha:rule-state:treasury"),
                null,
                new TxId("tx:project-alpha:cashpool:0001"),
                DateTimeOffset.Parse("2026-05-14T10:30:04Z"),
                "Cash above target swept to treasury reserve.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["eventId"] = "evt:project-alpha:graph:cashflow:001",
                    ["categoryCode"] = "cash-receipts"
                }),
            new(
                project.Summary.ProjectId,
                setup.ScenarioId.Value,
                "alpha.reserve-floor-guard.v1",
                AccountRuleActionKind.UpdateBusinessObject.ToString(),
                AccountRuleExecutionStatus.Skipped.ToString(),
                new BusinessObjectId("bo:project-alpha:rule-state:operations"),
                null,
                null,
                DateTimeOffset.Parse("2026-05-14T10:30:04Z"),
                "Reserve floor was not breached.",
                new Dictionary<string, string>(StringComparer.Ordinal))
        ];

        var observation = new SimulationRunObservationService(new EconomySimulatorJsonSerializer())
            .BuildObservation(project, setup, run, result, CreateGraphs(), evidence);

        return new SimulationRunObservationViewModel(project, setup, observation);
    }

    public static SimulationRunConsoleViewModel CreateRunConsole(string storageMode = "InMemory", string? selectedRunId = null)
    {
        SimulationProjectManagementViewModel management = CreateProjectManagement(storageMode);
        var serializer = new EconomySimulatorJsonSerializer();
        var validation = new SimulationProjectValidationService();
        var importExport = new SimulationProjectImportExportService(serializer, validation);
        var observation = new SimulationRunObservationService(serializer);
        var lifecycle = new InMemorySimulationRunLifecycleService(validation, importExport, observation, serializer, new SystemTimeProvider());
        IReadOnlyList<SimulationRunConsoleState> runs = lifecycle.EnsureProjectRuns(management.SelectedProject);
        SimulationRunConsoleState selected = string.IsNullOrWhiteSpace(selectedRunId)
            ? runs[0]
            : runs.FirstOrDefault(run => string.Equals(run.Run.RunId, selectedRunId, StringComparison.Ordinal)) ?? runs[0];

        return new SimulationRunConsoleViewModel(management.Projects, runs, selected, []);
    }

    public static SimulationProjectState CreateProjectState(string storageMode = "InMemory")
    {
        var serializer = new EconomySimulatorJsonSerializer();
        DateTimeOffset updated = DateTimeOffset.Parse("2026-05-14T11:45:00Z");
        var model = new EconomySimulatorModelState(
            "project-alpha",
            "Project Alpha",
            EconomySimulatorSchema.Version,
            "USD",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            "gregorian",
            "generic-sales-tax.v1",
            "generic-payroll.v1",
            "straight-line.v1",
            Treasury,
            ExternalClearing,
            [Treasury],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inflationRate"] = "0.032",
                ["customerAcquisitionRate"] = "0.11",
                ["collectionDelayDays"] = "18"
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["owner"] = "Finance ops",
                ["calendar"] = "gregorian"
            });
        var summary = new SimulationProjectSummary(
            model.ProjectId,
            model.Name,
            SimulationProjectStatus.Active,
            model.BaseCurrencyCode,
            model.PlanningStartDate,
            model.PlanningEndDate,
            DateTimeOffset.Parse("2026-05-14T10:30:00Z"),
            2,
            ParseStorageMode(storageMode),
            updated);
        IReadOnlyList<BusinessUnitState> businessUnits =
        [
            BusinessUnit(Treasury, "Project treasury", BusinessUnitKind.Project, null, TreasuryAccount, "treasury", BusinessUnitStatus.Active),
            BusinessUnit(Sales, "Sales", BusinessUnitKind.RevenueCenter, Treasury, SalesAccount, "sales", BusinessUnitStatus.Active, "revenue"),
            BusinessUnit(Operations, "Operations", BusinessUnitKind.CostCenter, Treasury, OperationsAccount, "operations", BusinessUnitStatus.Active, "expense"),
            BusinessUnit(ExternalClearing, "External clearing", BusinessUnitKind.ExternalBoundary, Treasury, ExternalClearingAccount, "external-clearing", BusinessUnitStatus.Active, "external")
        ];
        IReadOnlyList<BusinessUnitAddressBindingState> bindings =
        [
            Binding(Treasury, TreasuryAccount, BusinessUnitAddressRole.Treasury, true, "Treasury main"),
            Binding(Sales, SalesAccount, BusinessUnitAddressRole.Revenue, true, "Sales revenue"),
            Binding(Operations, OperationsAccount, BusinessUnitAddressRole.Expense, true, "Operations expense"),
            Binding(ExternalClearing, ExternalClearingAccount, BusinessUnitAddressRole.ExternalClearing, true, "External clearing")
        ];
        IReadOnlyList<SimulationProjectFolderState> folders =
        [
            new("folder:project-config", null, "Project configuration", SimulationProjectFolderKind.ProjectConfiguration, 10, new Dictionary<string, string>()),
            new("folder:bu-objects", null, "BU objects", SimulationProjectFolderKind.BusinessUnits, 20, new Dictionary<string, string>()),
            new("folder:financial-inputs", null, "Financial inputs", SimulationProjectFolderKind.FinancialInputs, 30, new Dictionary<string, string>()),
            new("folder:rule-packs", null, "Rule packs", SimulationProjectFolderKind.RulePack, 40, new Dictionary<string, string>()),
            new("folder:trigger-templates", "folder:rule-packs", "Trigger templates", SimulationProjectFolderKind.TriggerTemplate, 50, new Dictionary<string, string>()),
            new("folder:evidence-templates", "folder:rule-packs", "Evidence templates", SimulationProjectFolderKind.EvidenceTemplate, 60, new Dictionary<string, string>())
        ];
        AccountRuleDefinition cashpoolRule = EconomySimulatorCommonRuleFactory.CreateCashpoolSweepRule(
            "alpha.cashpool-sweep.v1",
            "economy-simulator.project-alpha.rules",
            TreasuryAccount,
            100000m,
            5000m,
            86400);
        AccountRuleDefinition reserveRule = EconomySimulatorCommonRuleFactory.CreateReserveFloorGuardRule(
            "alpha.reserve-floor-guard.v1",
            "economy-simulator.project-alpha.rules",
            65000m);
        IReadOnlyList<SimulationBusinessObjectDefinitionState> definitions =
        [
            Definition("def:project-config", "Project model configuration", SimulationBusinessObjectDefinitionKind.ProjectConfiguration, EconomySimulatorObjectTypes.ProjectConfiguration, "folder:project-config", null, null, serializer.Serialize(model), null, SimulationDefinitionMaterializationStatus.Validated),
            Definition("def:sales-bu", "Sales BU object", SimulationBusinessObjectDefinitionKind.BusinessUnitDefinition, EconomySimulatorObjectTypes.BusinessUnit, "folder:bu-objects", Sales, SalesAccount, serializer.Serialize(businessUnits[1]), null, SimulationDefinitionMaterializationStatus.Materialized),
            Definition("def:cashpool-rule", "Cashpool sweep rule", SimulationBusinessObjectDefinitionKind.RuleDefinition, EconomySimulatorObjectTypes.RuleState, "folder:rule-packs", Treasury, TreasuryAccount, serializer.Serialize(new { ruleId = cashpoolRule.RuleId, purpose = "Concentrate cash above reserve target." }), cashpoolRule, SimulationDefinitionMaterializationStatus.Validated),
            Definition("def:reserve-rule", "Reserve floor guard", SimulationBusinessObjectDefinitionKind.RuleDefinition, EconomySimulatorObjectTypes.RuleState, "folder:rule-packs", Treasury, TreasuryAccount, serializer.Serialize(new { ruleId = reserveRule.RuleId, purpose = "Create evidence when liquidity floor is breached." }), reserveRule, SimulationDefinitionMaterializationStatus.Validated),
            Definition("def:payable-trigger", "Payable trigger template", SimulationBusinessObjectDefinitionKind.TriggerTemplate, EconomySimulatorObjectTypes.Payable, "folder:trigger-templates", Operations, OperationsAccount, serializer.Serialize(new { dueDays = 30, processed = false, amount = 12000 }), null, SimulationDefinitionMaterializationStatus.Draft),
            Definition("def:rule-evidence", "Rule evidence template", SimulationBusinessObjectDefinitionKind.EvidenceTemplate, EconomySimulatorObjectTypes.RuleExecutionEvidence, "folder:evidence-templates", Treasury, TreasuryAccount, serializer.Serialize(new { status = "executed", actionKind = "Payment" }), null, SimulationDefinitionMaterializationStatus.Draft)
        ];
        IReadOnlyList<SimulationFinancialInputProfileState> profiles =
        [
            new(
                "profile:baseline",
                "Baseline assumptions",
                [
                    Input("input:tax-rate", "Sales tax rate", SimulationFinancialInputKind.TaxRate, SimulationFinancialInputUnit.Percent, "rate", 0.0825m, null, "generic-sales-tax.v1"),
                    Input("input:inflation", "Inflation", SimulationFinancialInputKind.InflationRate, SimulationFinancialInputUnit.Percent, "rate", 0.032m, null, "finance forecast"),
                    Input("input:acquisition", "Customer acquisition", SimulationFinancialInputKind.CustomerAcquisitionRate, SimulationFinancialInputUnit.Percent, "rate", 0.11m, Sales, "growth model"),
                    Input("input:collection-delay", "Collection delay", SimulationFinancialInputKind.CollectionDelayDays, SimulationFinancialInputUnit.Days, "days", 18m, Sales, "AR history"),
                    Input("input:supplier-terms", "Supplier payment terms", SimulationFinancialInputKind.SupplierPaymentTermDays, SimulationFinancialInputUnit.Days, "days", 30m, Operations, "vendor terms"),
                    Input("input:wages", "Monthly wage assumption", SimulationFinancialInputKind.WageAssumption, SimulationFinancialInputUnit.Amount, "USD/month", 96000m, Operations, "payroll plan")
                ],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "sample"
                })
        ];
        IReadOnlyList<SimulationRunSetupState> setups =
        [
            new(
                "setup:baseline-monthly",
                new ScenarioId("baseline-monthly"),
                "Baseline monthly run",
                SimulationRunStepPeriod.Month,
                12,
                model.PlanningStartDate,
                model.PlanningEndDate,
                SimulationUiRunSpeed.Normal,
                42,
                businessUnits.Select(x => x.BusinessUnitId).ToArray(),
                ["model.hybrid-service-product.v1", "dept.marketing.v1", "common.external-clearing.v1"],
                true,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["inflationRate"] = "0.032",
                    ["collectionDelayDays"] = "18"
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["speedNote"] = "UI speed is intentionally excluded from deterministic run hashes."
                })
        ];
        IReadOnlyList<SimulationProjectValidationMessage> validationMessages =
        [
            new("reserve-threshold", SimulationValidationSeverity.Warning, "Reserve falls below two months in downside case.", "runSetups.setup:baseline-monthly", "setup:baseline-monthly"),
            new("rule-evidence-linked", SimulationValidationSeverity.Info, "Rule evidence templates link back to BO/rule definitions.", "businessObjectDefinitions", "def:rule-evidence")
        ];

        return new SimulationProjectState(
            summary,
            model,
            businessUnits,
            bindings,
            folders,
            definitions,
            profiles,
            setups,
            validationMessages,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["layoutProposal"] = "artifacts/economy/simulator/layout-proposals"
            });
    }

    private static CashFlowProjectionPoint Point(DateOnly date, BusinessUnitId bu, string category, long inflow, long outflow, long balance)
        => new(
            date,
            bu,
            category,
            Amount.FromInt64(inflow),
            Amount.FromInt64(outflow),
            Amount.Zero,
            Amount.Zero,
            Amount.Zero,
            Amount.Zero,
            Amount.Zero,
            Amount.Zero,
            Amount.FromInt64(balance),
            0.9m);

    private static BusinessUnitState BusinessUnit(
        BusinessUnitId businessUnitId,
        string displayName,
        BusinessUnitKind kind,
        BusinessUnitId? parentBusinessUnitId,
        Address primaryAddress,
        string path,
        BusinessUnitStatus status,
        string? role = null)
        => new(
            "project-alpha",
            businessUnitId,
            displayName,
            kind,
            parentBusinessUnitId,
            primaryAddress,
            path,
            kind == BusinessUnitKind.CostCenter ? path : null,
            kind == BusinessUnitKind.RevenueCenter ? path : null,
            null,
            "USD",
            kind == BusinessUnitKind.ExternalBoundary,
            false,
            status,
            [primaryAddress],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = role ?? kind.ToString()
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["owner"] = displayName
            });

    private static BusinessUnitAddressBindingState Binding(
        BusinessUnitId businessUnitId,
        Address address,
        BusinessUnitAddressRole role,
        bool isPrimary,
        string displayName)
        => new(
            "project-alpha",
            businessUnitId,
            address,
            role,
            role.ToString(),
            isPrimary,
            BusinessUnitAddressBindingStatus.Active,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            null,
            businessUnitId == Treasury ? null : TreasuryAccount,
            businessUnitId == Treasury ? null : Treasury,
            displayName,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["accountRole"] = role.ToString()
            });

    private static SimulationBusinessObjectDefinitionState Definition(
        string definitionId,
        string displayName,
        SimulationBusinessObjectDefinitionKind kind,
        string objectType,
        string folderId,
        BusinessUnitId? ownerBusinessUnitId,
        Address? ownerAddress,
        string stateJson,
        AccountRuleDefinition? ruleDefinition,
        SimulationDefinitionMaterializationStatus status)
        => new(
            definitionId,
            displayName,
            kind,
            objectType,
            EconomySimulatorSchema.Version,
            folderId,
            ownerBusinessUnitId,
            ownerAddress,
            stateJson,
            ruleDefinition,
            status,
            ruleDefinition is null ? [] : ["evidence:sample-run:cashpool"],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = "sample"
            });

    private static SimulationFinancialInputState Input(
        string inputId,
        string displayName,
        SimulationFinancialInputKind kind,
        SimulationFinancialInputUnit unit,
        string unitCode,
        decimal value,
        BusinessUnitId? businessUnitId,
        string source)
        => new(
            inputId,
            displayName,
            kind,
            unit,
            unitCode,
            value,
            null,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            businessUnitId,
            source,
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static SimulationProjectStorageMode ParseStorageMode(string storageMode)
        => storageMode.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            ? SimulationProjectStorageMode.PostgreSql
            : SimulationProjectStorageMode.InMemory;

    private static IReadOnlyList<EconomyGraphData> CreateGraphs()
    {
        var cashFlowPoints = new[]
        {
            GraphPoint(new DateOnly(2026, 6, 15), Sales.Value, SalesAddress, "cash-receipts", FinancialStatementSection.CashReceipts, committedInflow: 42000m, balanceAfter: 226200m),
            GraphPoint(new DateOnly(2026, 6, 25), Operations.Value, OperationsAddress, "payroll-expense", FinancialStatementSection.Payroll, plannedOutflow: 36000m, balanceAfter: 190200m),
            GraphPoint(new DateOnly(2026, 7, 5), Operations.Value, OperationsAddress, "opex-cloud", FinancialStatementSection.Opex, plannedOutflow: 6000m, balanceAfter: 184200m),
            GraphPoint(new DateOnly(2026, 7, 12), Sales.Value, SalesAddress, "cash-receipts", FinancialStatementSection.CashReceipts, simulatedInflow: 58000m, balanceAfter: 242200m)
        };
        var revenuePoints = new[]
        {
            GraphPoint(new DateOnly(2026, 6, 1), Sales.Value, SalesAddress, "revenue-subscription", FinancialStatementSection.Revenue, nonCashIncome: 42000m),
            GraphPoint(new DateOnly(2026, 7, 1), Sales.Value, SalesAddress, "revenue-subscription", FinancialStatementSection.Revenue, nonCashIncome: 58000m),
            GraphPoint(new DateOnly(2026, 7, 1), Sales.Value, SalesAddress, "revenue-implementation", FinancialStatementSection.Revenue, nonCashIncome: 16000m)
        };
        var expensePoints = new[]
        {
            GraphPoint(new DateOnly(2026, 6, 25), Operations.Value, OperationsAddress, "payroll-expense", FinancialStatementSection.Payroll, plannedOutflow: 36000m),
            GraphPoint(new DateOnly(2026, 7, 5), Operations.Value, OperationsAddress, "opex-cloud", FinancialStatementSection.Opex, plannedOutflow: 6000m),
            GraphPoint(new DateOnly(2026, 7, 10), Treasury.Value, TreasuryAddress, "capex-equipment", FinancialStatementSection.Capex, plannedOutflow: 8400m)
        };

        return
        [
            Graph(ScenarioGraphMetric.CashFlow, cashFlowPoints),
            Graph(ScenarioGraphMetric.Revenue, revenuePoints),
            Graph(ScenarioGraphMetric.Expense, expensePoints)
        ];
    }

    private static EconomyGraphPoint GraphPoint(
        DateOnly periodStart,
        string businessUnitId,
        string accountAddress,
        string category,
        FinancialStatementSection section,
        decimal plannedInflow = 0m,
        decimal plannedOutflow = 0m,
        decimal committedInflow = 0m,
        decimal simulatedInflow = 0m,
        decimal nonCashIncome = 0m,
        decimal balanceAfter = 0m)
        => new(
            periodStart,
            businessUnitId,
            accountAddress,
            category,
            section.ToString(),
            plannedInflow,
            plannedOutflow,
            committedInflow,
            0m,
            0m,
            0m,
            simulatedInflow,
            0m,
            nonCashIncome,
            0m,
            balanceAfter);

    private static EconomyGraphData Graph(ScenarioGraphMetric metric, IReadOnlyList<EconomyGraphPoint> points)
    {
        var filter = new EconomyGraphQueryFilter("project-alpha", "downside-q3", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 31), metric, null, [], [], 500);
        var series = points
            .GroupBy(point => point.CategoryCode, StringComparer.Ordinal)
            .Select(group => new EconomyGraphSeries(group.Key, group.Key, group.ToArray()))
            .ToArray();
        var rows = points
            .Select((point, index) => new EconomyGraphDrilldownRow(
                point.PeriodStart,
                $"evt:project-alpha:graph:{metric.ToString().ToLowerInvariant()}:{index + 1:000}",
                point.BusinessUnitId,
                point.AccountAddress,
                point.CategoryCode,
                point.FinancialStatementSection,
                "Planned",
                GraphAmount(metric, point),
                point.CategoryCode,
                point.CategoryCode.Replace('-', ' ')))
            .ToArray();
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["inflow"] = points.Sum(point => point.PlannedInflow + point.CommittedInflow + point.ActualInflow + point.SimulatedInflow),
            ["outflow"] = points.Sum(point => point.PlannedOutflow + point.CommittedOutflow + point.ActualOutflow + point.SimulatedOutflow),
            ["nonCashIncome"] = points.Sum(point => point.NonCashIncome),
            ["nonCashExpense"] = points.Sum(point => point.NonCashExpense),
            ["netCashFlow"] = points.Sum(point => point.PlannedInflow + point.CommittedInflow + point.ActualInflow + point.SimulatedInflow - point.PlannedOutflow - point.CommittedOutflow - point.ActualOutflow - point.SimulatedOutflow),
            ["rowCount"] = points.Count
        };

        return new EconomyGraphData(filter, series, rows, totals);
    }

    private static decimal GraphAmount(ScenarioGraphMetric metric, EconomyGraphPoint point)
        => metric switch
        {
            ScenarioGraphMetric.Revenue => point.PlannedInflow + point.CommittedInflow + point.ActualInflow + point.SimulatedInflow + point.NonCashIncome,
            ScenarioGraphMetric.Expense => point.PlannedOutflow + point.CommittedOutflow + point.ActualOutflow + point.SimulatedOutflow + point.NonCashExpense,
            _ => point.PlannedInflow + point.CommittedInflow + point.ActualInflow + point.SimulatedInflow - point.PlannedOutflow - point.CommittedOutflow - point.ActualOutflow - point.SimulatedOutflow
        };
}
