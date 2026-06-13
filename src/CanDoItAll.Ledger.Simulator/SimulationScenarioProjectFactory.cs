using System.Globalization;

namespace CanDoItAll.Economy.Simulator;

public interface ISimulationScenarioProjectFactory
{
    IReadOnlyList<SimulationProjectState> CreateScenarioProjects(DateTimeOffset updatedAtUtc);
}

public sealed class SimulationScenarioProjectFactory(
    IEconomyScenarioTemplateCatalog catalog,
    IEconomySimulatorJsonSerializer serializer) : ISimulationScenarioProjectFactory
{
    public IReadOnlyList<SimulationProjectState> CreateScenarioProjects(DateTimeOffset updatedAtUtc)
        => catalog.GetScenarios()
            .Select(model => CreateProject(model, updatedAtUtc))
            .ToArray();

    private SimulationProjectState CreateProject(ScenarioBusinessModelState scenario, DateTimeOffset updatedAtUtc)
    {
        BusinessUnitState treasury = scenario.BusinessUnits.FirstOrDefault(x => x.Path == "treasury") ?? scenario.BusinessUnits[0];
        BusinessUnitState external = CreateExternalClearing(scenario.ProjectId, treasury);
        IReadOnlyList<BusinessUnitState> businessUnits = scenario.BusinessUnits
            .Concat([external])
            .GroupBy(x => x.BusinessUnitId)
            .Select(x => x.First())
            .ToArray();
        var model = new EconomySimulatorModelState(
            scenario.ProjectId,
            scenario.DisplayName,
            EconomySimulatorSchema.Version,
            scenario.BaseCurrencyCode,
            scenario.PlanningStartDate,
            scenario.PlanningEndDate,
            "gregorian",
            null,
            null,
            "straight-line.v1",
            treasury.BusinessUnitId,
            external.BusinessUnitId,
            [treasury.BusinessUnitId],
            BuildAssumptions(scenario),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scenarioCode"] = scenario.ScenarioCode,
                ["scenarioProject"] = "true"
            });
        var summary = new SimulationProjectSummary(
            scenario.ProjectId,
            scenario.DisplayName,
            SimulationProjectStatus.Active,
            scenario.BaseCurrencyCode,
            scenario.PlanningStartDate,
            scenario.PlanningEndDate,
            null,
            0,
            SimulationProjectStorageMode.InMemory,
            updatedAtUtc);
        IReadOnlyList<BusinessUnitAddressBindingState> bindings = businessUnits
            .Select(unit => Binding(scenario.ProjectId, unit, treasury))
            .ToArray();
        IReadOnlyList<SimulationProjectFolderState> folders =
        [
            new("folder:project-config", null, "Project configuration", SimulationProjectFolderKind.ProjectConfiguration, 10, Empty()),
            new("folder:business-units", null, "Business units", SimulationProjectFolderKind.BusinessUnits, 20, Empty()),
            new("folder:scenario-model", null, "Scenario model", SimulationProjectFolderKind.ImportedObjects, 30, Empty()),
            new("folder:financial-inputs", null, "Financial inputs", SimulationProjectFolderKind.FinancialInputs, 40, Empty()),
            new("folder:rule-pack", null, "Rule pack", SimulationProjectFolderKind.RulePack, 50, Empty()),
            new("folder:flow-evidence", null, "Work and product flow evidence", SimulationProjectFolderKind.ImportedObjects, 60, Empty()),
            new("folder:planned-financial-schedules", null, "Planned financial schedules", SimulationProjectFolderKind.FinancialInputs, 70, Empty())
        ];
        EconomySimulatorScenarioRulePack rulePack = EconomySimulatorScenarioRulePackCatalog.CreateRulePack(scenario);
        IReadOnlyList<SimulationBusinessObjectDefinitionState> definitions = BuildDefinitions(scenario, model, businessUnits, rulePack);
        IReadOnlyList<SimulationFinancialInputProfileState> profiles = [BuildFinancialProfile(scenario)];
        int stepCount = MonthsInclusive(scenario.PlanningStartDate, scenario.PlanningEndDate);
        IReadOnlyList<SimulationRunSetupState> setups =
        [
            new(
                $"setup:{scenario.ScenarioCode.ToLowerInvariant()}:monthly",
                new ScenarioId($"{scenario.ScenarioCode.ToLowerInvariant()}-project-run"),
                $"{scenario.ScenarioCode} monthly project run",
                SimulationRunStepPeriod.Month,
                stepCount,
                scenario.PlanningStartDate,
                scenario.PlanningEndDate,
                SimulationUiRunSpeed.Normal,
                scenario.ScenarioCode switch { "S1" => 101, "S2" => 202, "S3" => 303, _ => 1 },
                businessUnits.Select(x => x.BusinessUnitId).ToArray(),
                ["scenario-business-model", "scenario-rule-pack", "flow-token-compiler"],
                true,
                BuildAssumptions(scenario),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["scenarioCode"] = scenario.ScenarioCode,
                    ["source"] = "scenario-template-catalog"
                })
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
            [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scenarioCode"] = scenario.ScenarioCode,
                ["scenarioProject"] = "true",
                ["openingCash"] = scenario.OpeningCash.Value.ToString("0.##", CultureInfo.InvariantCulture),
                ["minimumCash"] = scenario.MinimumCash.Value.ToString("0.##", CultureInfo.InvariantCulture)
            });
    }

    private IReadOnlyList<SimulationBusinessObjectDefinitionState> BuildDefinitions(
        ScenarioBusinessModelState scenario,
        EconomySimulatorModelState model,
        IReadOnlyList<BusinessUnitState> businessUnits,
        EconomySimulatorScenarioRulePack rulePack)
    {
        var definitions = new List<SimulationBusinessObjectDefinitionState>
        {
            Definition("def:project-config", "Project model configuration", SimulationBusinessObjectDefinitionKind.ProjectConfiguration, EconomySimulatorObjectTypes.ProjectConfiguration, "folder:project-config", null, null, serializer.Serialize(model), null, SimulationDefinitionMaterializationStatus.Validated),
            Definition("def:scenario-model", scenario.DisplayName, SimulationBusinessObjectDefinitionKind.ImportedObject, EconomySimulatorObjectTypes.Template, "folder:scenario-model", null, null, serializer.Serialize(scenario), null, SimulationDefinitionMaterializationStatus.Validated),
            Definition("def:rule-automation-plan", $"{scenario.ScenarioCode} rule automation plan", SimulationBusinessObjectDefinitionKind.RuleDefinition, EconomySimulatorObjectTypes.RuleAutomationPlan, "folder:rule-pack", null, null, serializer.Serialize(rulePack.ToPlanState()), null, SimulationDefinitionMaterializationStatus.Validated)
        };

        definitions.AddRange(businessUnits.Select(unit => Definition(
            $"def:bu:{EconomySimulatorIdentifiers.NormalizeSegment(unit.Path)}",
            unit.DisplayName,
            SimulationBusinessObjectDefinitionKind.BusinessUnitDefinition,
            EconomySimulatorObjectTypes.BusinessUnit,
            "folder:business-units",
            unit.BusinessUnitId,
            unit.PrimaryAddress,
            serializer.Serialize(unit),
            null,
            SimulationDefinitionMaterializationStatus.Materialized)));

        definitions.AddRange(rulePack.Templates.Select(template => Definition(
            $"def:rule:{EconomySimulatorIdentifiers.NormalizeSegment(template.Rule.RuleId)}",
            template.Process.ProcessCode,
            SimulationBusinessObjectDefinitionKind.RuleDefinition,
            EconomySimulatorObjectTypes.RuleState,
            "folder:rule-pack",
            template.OwnerBusinessUnitId,
            template.OwnerAddress,
            serializer.Serialize(template.Process),
            template.Rule,
            SimulationDefinitionMaterializationStatus.Validated)));

        definitions.AddRange(scenario.WorkOrders.Select(workOrder => Definition(
            $"def:work-order:{EconomySimulatorIdentifiers.NormalizeSegment(workOrder.WorkOrderId)}",
            workOrder.DisplayName,
            SimulationBusinessObjectDefinitionKind.ImportedObject,
            EconomySimulatorObjectTypes.WorkOrder,
            "folder:flow-evidence",
            workOrder.DeliveryBusinessUnitId,
            businessUnits.First(x => x.BusinessUnitId == workOrder.DeliveryBusinessUnitId).PrimaryAddress,
            serializer.Serialize(workOrder),
            null,
            SimulationDefinitionMaterializationStatus.Validated)));

        definitions.AddRange(scenario.ProductFlowPlans.Select(flow => Definition(
            $"def:product-flow:{EconomySimulatorIdentifiers.NormalizeSegment(flow.ProductFlowPlanId)}",
            flow.DisplayName,
            SimulationBusinessObjectDefinitionKind.ImportedObject,
            EconomySimulatorObjectTypes.ProductFlowPlan,
            "folder:flow-evidence",
            businessUnits.FirstOrDefault(x => flow.Steps.Any(step => step.BusinessUnitId == x.BusinessUnitId))?.BusinessUnitId,
            businessUnits.FirstOrDefault(x => flow.Steps.Any(step => step.BusinessUnitId == x.BusinessUnitId))?.PrimaryAddress,
            serializer.Serialize(flow),
            null,
            SimulationDefinitionMaterializationStatus.Validated)));

        definitions.AddRange(scenario.FinancialSchedules.Select(schedule => Definition(
            $"def:financial-schedule:{EconomySimulatorIdentifiers.NormalizeSegment(schedule.ScheduleId)}",
            schedule.DisplayName,
            SimulationBusinessObjectDefinitionKind.FinancialInput,
            EconomySimulatorObjectTypes.PlannedFinancialSchedule,
            "folder:planned-financial-schedules",
            schedule.BusinessUnitId,
            businessUnits.FirstOrDefault(x => x.BusinessUnitId == schedule.BusinessUnitId)?.PrimaryAddress,
            serializer.Serialize(schedule),
            null,
            schedule.RequiresApproval && schedule.Approval?.Status is ScenarioApprovalStatus.Draft or ScenarioApprovalStatus.Requested
                ? SimulationDefinitionMaterializationStatus.Draft
                : SimulationDefinitionMaterializationStatus.Validated)));

        return definitions;
    }

    private static SimulationFinancialInputProfileState BuildFinancialProfile(ScenarioBusinessModelState scenario)
    {
        decimal collectionDays = scenario.PaymentTerms.Count == 0 ? 0m : decimal.Round(scenario.PaymentTerms.Average(x => (decimal)x.DueDays), 2);
        decimal supplierDays = AverageSupplierDays(scenario);
        decimal monthlyPayroll = scenario.WorkforcePlans
            .SelectMany(x => x.Departments)
            .Sum(x => x.StartingFte * x.MonthlyGrossSalaryPerFte.Value * (1m + x.EmployerBurdenRate));
        BusinessUnitId? revenueUnit = scenario.BusinessUnits.FirstOrDefault(x => x.Kind == BusinessUnitKind.RevenueCenter)?.BusinessUnitId;
        BusinessUnitId? costUnit = scenario.BusinessUnits.FirstOrDefault(x => x.Kind == BusinessUnitKind.CostCenter)?.BusinessUnitId;

        return new SimulationFinancialInputProfileState(
            "profile:scenario-baseline",
            $"{scenario.ScenarioCode} scenario assumptions",
            [
                Input("input:collection-delay", "Average customer payment term", SimulationFinancialInputKind.CollectionDelayDays, SimulationFinancialInputUnit.Days, "days", collectionDays, revenueUnit, "scenario payment terms"),
                Input("input:supplier-terms", "Average supplier payment term", SimulationFinancialInputKind.SupplierPaymentTermDays, SimulationFinancialInputUnit.Days, "days", supplierDays, costUnit, "scenario supplier assumptions"),
                Input("input:wages", "Starting monthly payroll burden", SimulationFinancialInputKind.WageAssumption, SimulationFinancialInputUnit.Amount, $"{scenario.BaseCurrencyCode}/month", decimal.Round(monthlyPayroll, 2), costUnit, "scenario workforce plan"),
                Input("input:price-growth", "Average monthly demand growth proxy", SimulationFinancialInputKind.PriceGrowthRate, SimulationFinancialInputUnit.Percent, "rate", AverageDemandGrowth(scenario), revenueUnit, "scenario demand schedules")
            ],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scenarioCode"] = scenario.ScenarioCode
            });
    }

    private static SimulationFinancialInputState Input(string id, string displayName, SimulationFinancialInputKind kind, SimulationFinancialInputUnit unit, string unitCode, decimal value, BusinessUnitId? businessUnitId, string source)
        => new(id, displayName, kind, unit, unitCode, value, null, null, null, businessUnitId, source, Empty());

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
            ruleDefinition is null ? [] : [$"evidence:{definitionId}"],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = "scenario-project-factory"
            });

    private static BusinessUnitAddressBindingState Binding(string projectId, BusinessUnitState unit, BusinessUnitState treasury)
        => new(
            projectId,
            unit.BusinessUnitId,
            unit.PrimaryAddress,
            RoleFor(unit),
            RoleFor(unit).ToString(),
            true,
            BusinessUnitAddressBindingStatus.Active,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            null,
            unit.BusinessUnitId == treasury.BusinessUnitId ? null : treasury.PrimaryAddress,
            unit.BusinessUnitId == treasury.BusinessUnitId ? null : treasury.BusinessUnitId,
            unit.DisplayName,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scenarioBinding"] = "true"
            });

    private static BusinessUnitAddressRole RoleFor(BusinessUnitState unit)
        => unit.Kind switch
        {
            BusinessUnitKind.RevenueCenter => BusinessUnitAddressRole.Revenue,
            BusinessUnitKind.ExternalBoundary => BusinessUnitAddressRole.ExternalClearing,
            BusinessUnitKind.Project => BusinessUnitAddressRole.Treasury,
            _ => BusinessUnitAddressRole.Expense
        };

    private static BusinessUnitState CreateExternalClearing(string projectId, BusinessUnitState treasury)
    {
        BusinessUnitId id = EconomySimulatorIdentifiers.BusinessUnitId(projectId, "external-clearing");
        Address address = Address.FromPublicKey(System.Text.Encoding.UTF8.GetBytes($"{projectId}:external-clearing"));
        return new BusinessUnitState(
            projectId,
            id,
            "External clearing",
            BusinessUnitKind.ExternalBoundary,
            treasury.BusinessUnitId,
            address,
            "external-clearing",
            null,
            null,
            null,
            treasury.CurrencyCode,
            true,
            true,
            BusinessUnitStatus.Active,
            [address],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["role"] = "external-clearing" },
            Empty());
    }

    private static IReadOnlyDictionary<string, string> BuildAssumptions(ScenarioBusinessModelState scenario)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scenarioCode"] = scenario.ScenarioCode,
            ["openingCash"] = scenario.OpeningCash.Value.ToString("0.##", CultureInfo.InvariantCulture),
            ["minimumCash"] = scenario.MinimumCash.Value.ToString("0.##", CultureInfo.InvariantCulture),
            ["planningStart"] = scenario.PlanningStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["planningEnd"] = scenario.PlanningEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

    private static decimal AverageSupplierDays(ScenarioBusinessModelState scenario)
    {
        decimal[] days = scenario.Ingredients
            .Select(x => (decimal)x.SupplierPaymentTermDays)
            .Concat(scenario.BillsOfMaterial.SelectMany(x => x.Lines.Select(line => (decimal)line.SupplierPaymentTermDays)))
            .Where(x => x > 0m)
            .ToArray();
        return days.Length == 0 ? 0m : decimal.Round(days.Average(), 2);
    }

    private static decimal AverageDemandGrowth(ScenarioBusinessModelState scenario)
    {
        decimal[] rates = scenario.DemandSchedules.Select(x => x.MonthlyGrowthRate).Where(x => x != 0m).ToArray();
        return rates.Length == 0 ? 0m : decimal.Round(rates.Average(), 4);
    }

    private static int MonthsInclusive(DateOnly start, DateOnly end)
        => ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;

    private static IReadOnlyDictionary<string, string> Empty() => new Dictionary<string, string>(StringComparer.Ordinal);
}
