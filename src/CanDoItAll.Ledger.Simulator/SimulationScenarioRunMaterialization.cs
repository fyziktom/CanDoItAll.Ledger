using System.Globalization;

namespace CanDoItAll.Economy.Simulator;

public interface ISimulationScenarioRunMaterializer
{
    SimulationScenarioRunMaterialization? TryMaterialize(
        SimulationScenarioRunMaterializationRequest request);
}

public sealed record SimulationScenarioRunMaterializationRequest(
    SimulationProjectState Project,
    SimulationRunSetupState Setup,
    string RunId,
    SimulationRunLifecycleStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int CurrentStep,
    DateOnly CurrentDate,
    DateOnly StepEndDate,
    IReadOnlyList<SimulationRunInterventionState> Interventions,
    IReadOnlyList<SimulationProjectValidationMessage> CommandMessages);

public sealed record SimulationScenarioRunMaterialization(
    IReadOnlyList<EconomicEventState> Events,
    SimulationResultState Result,
    IReadOnlyDictionary<string, decimal> KpiValues,
    string OutputHash,
    DateOnly StepEndDate,
    IReadOnlyList<SimulationRunInterventionState> Interventions);

public sealed class EconomyScenarioRunMaterializer(
    IEconomyScenarioTemplateCatalog scenarioCatalog,
    IEconomyScenarioEventCompiler scenarioCompiler,
    IEconomyProjectionService projectionService,
    IEconomySimulatorJsonSerializer serializer) : ISimulationScenarioRunMaterializer
{
    public SimulationScenarioRunMaterialization? TryMaterialize(
        SimulationScenarioRunMaterializationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsScenarioProject(request.Project))
        {
            return null;
        }

        if (!request.Project.Metadata.TryGetValue("scenarioCode", out string? scenarioCode)
            || string.IsNullOrWhiteSpace(scenarioCode))
        {
            throw new InvalidOperationException(
                $"Scenario project '{request.Project.Summary.ProjectId}' requires non-empty 'scenarioCode' metadata.");
        }

        ScenarioBusinessModelState scenario = ApplyScenarioInterventions(
            scenarioCatalog.GetScenario(scenarioCode),
            request.Interventions);
        EconomyScenarioEventCompileResult compiled = scenarioCompiler.Compile(new EconomyScenarioEventCompileRequest(
            scenario,
            request.Setup.ScenarioId,
            request.Setup.HorizonStartDate,
            request.StepEndDate));
        IReadOnlyList<EconomicEventState> scheduledEvents = EconomyProjectionService.SortEvents(compiled.Events)
            .Where(x => x.EconomicDate >= request.Setup.HorizonStartDate && x.EconomicDate <= request.StepEndDate)
            .Select(x =>
            {
                var metadata = new Dictionary<string, string>(x.Metadata, StringComparer.Ordinal)
                {
                    ["runId"] = request.RunId
                };
                return x with
                {
                    Status = x.Status == EconomicEventStatus.Planned ? EconomicEventStatus.Simulated : x.Status,
                    Metadata = metadata
                };
            })
            .ToArray();
        EconomyProjection projection = projectionService.BuildProjection(new ProjectionRequest(
            request.Project.Summary.ProjectId,
            scenario.OpeningCash,
            scheduledEvents));
        IReadOnlyList<SimulationIssue> issues = projection.Issues
            .Concat(request.CommandMessages.Select(message => new SimulationIssue(
                $"issue:{request.Setup.SetupId}:{message.Code}",
                message.Code,
                message.Severity.ToString(),
                request.CurrentDate,
                null,
                null,
                null,
                null,
                message.Message,
                new Dictionary<string, string>(StringComparer.Ordinal))))
            .ToArray();
        Amount closingCash = projection.CashFlow.Count == 0 ? scenario.OpeningCash : projection.CashFlow[^1].BalanceAfter;
        Amount minimumCash = projection.CashFlow.Count == 0
            ? scenario.OpeningCash
            : Amount.FromDecimal(projection.CashFlow.Min(x => x.BalanceAfter.Value));
        SimulationIssue? firstDeficit = issues.FirstOrDefault(x => x.Code.Contains("deficit", StringComparison.OrdinalIgnoreCase));
        IReadOnlyDictionary<string, decimal> kpis = BuildScenarioKpiValues(
            scenario,
            scheduledEvents,
            projection,
            issues,
            request.Interventions);
        string eventsHash = serializer.HashJson(scheduledEvents);
        string interventionHash = serializer.HashJson(request.Interventions.Select(intervention => new
        {
            intervention.InterventionId,
            intervention.Kind,
            intervention.TargetId,
            intervention.EffectiveDate,
            intervention.QuantityDelta,
            intervention.Multiplier,
            intervention.Amount,
            intervention.DepreciationMonths,
            intervention.BusinessObjectDefinitionId
        }));
        var result = new SimulationResultState(
            request.Project.Summary.ProjectId,
            request.Setup.ScenarioId,
            request.RunId,
            request.FinishedAtUtc ?? request.StartedAtUtc.AddMinutes(Math.Max(1, request.CurrentStep)),
            request.Setup.HorizonStartDate,
            request.Setup.HorizonEndDate,
            scenario.OpeningCash,
            closingCash,
            minimumCash,
            firstDeficit?.Date,
            firstDeficit?.BusinessUnitId,
            firstDeficit?.Amount ?? Amount.Zero,
            Amount.FromDecimal(Math.Max(0m, Math.Abs(minimumCash.Value - scenario.MinimumCash.Value) * 1.15m)),
            projection.CashFlow,
            issues,
            kpis,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["result"] = serializer.HashJson(new
                {
                    runId = request.RunId,
                    status = request.Status,
                    normalizedStep = request.CurrentStep,
                    eventsHash,
                    projectionHash = serializer.HashJson(projection.CashFlow),
                    interventionHash
                }),
                ["events"] = eventsHash,
                ["interventions"] = interventionHash,
                ["cashflow"] = serializer.HashJson(projection.CashFlow),
                ["schedule"] = compiled.ScheduleHash,
                ["runLog"] = serializer.HashJson(new
                {
                    runId = request.RunId,
                    status = request.Status,
                    normalizedStep = request.CurrentStep,
                    scenarioCode
                })
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scenarioCode"] = scenarioCode,
                ["currentStep"] = request.CurrentStep.ToString(CultureInfo.InvariantCulture),
                ["currentDate"] = request.CurrentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["stepEndDate"] = request.StepEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["eventCount"] = scheduledEvents.Count.ToString(CultureInfo.InvariantCulture),
                ["interventionCount"] = request.Interventions.Count.ToString(CultureInfo.InvariantCulture)
            });

        return new SimulationScenarioRunMaterialization(
            scheduledEvents,
            result,
            kpis,
            result.OutputHashes["result"],
            request.StepEndDate,
            request.Interventions);
    }

    private static bool IsScenarioProject(SimulationProjectState project)
        => project.Metadata.TryGetValue("scenarioProject", out string? value)
            && bool.TryParse(value, out bool parsed)
            && parsed;

    private static ScenarioBusinessModelState ApplyScenarioInterventions(
        ScenarioBusinessModelState scenario,
        IReadOnlyList<SimulationRunInterventionState> interventions)
    {
        SimulationRunInterventionState[] activeInterventions = interventions
            .Where(intervention => intervention.Status == SimulationRunInterventionStatus.Applied)
            .OrderBy(intervention => intervention.AppliedAtUtc)
            .ToArray();
        if (activeInterventions.Length == 0)
        {
            return scenario;
        }

        IReadOnlyList<DemandScheduleState> demandSchedules = scenario.DemandSchedules
            .Select(schedule => ApplyDemandInterventions(schedule, activeInterventions))
            .ToArray();
        IReadOnlyList<CapexPlanState> capexPlans =
        [
            ..scenario.CapexPlans,
            ..activeInterventions
                .Where(intervention => intervention.Kind == SimulationRunInterventionKind.CapacityExpansion)
                .Select(intervention => CreateCapacityExpansionCapexPlan(scenario, intervention))
        ];
        var metadata = new Dictionary<string, string>(scenario.Metadata, StringComparer.Ordinal)
        {
            ["runtimeInterventionCount"] = activeInterventions.Length.ToString(CultureInfo.InvariantCulture),
            ["runtimeInterventionIds"] = string.Join(';', activeInterventions.Select(intervention => intervention.InterventionId))
        };

        return scenario with
        {
            DemandSchedules = demandSchedules,
            CapexPlans = capexPlans,
            Metadata = metadata
        };
    }

    private static DemandScheduleState ApplyDemandInterventions(
        DemandScheduleState schedule,
        IReadOnlyList<SimulationRunInterventionState> interventions)
    {
        SimulationRunInterventionState[] related = interventions
            .Where(intervention => intervention.Kind == SimulationRunInterventionKind.DemandChange)
            .Where(intervention => string.Equals(intervention.TargetId, schedule.DemandScheduleId, StringComparison.Ordinal))
            .OrderBy(intervention => intervention.EffectiveDate)
            .ToArray();
        if (related.Length == 0)
        {
            return schedule;
        }

        IReadOnlyList<DemandChangeState> demandChanges =
        [
            ..schedule.DemandChanges,
            ..related.Select(intervention => new DemandChangeState(
                intervention.EffectiveDate,
                intervention.QuantityDelta ?? 0m,
                intervention.Multiplier ?? 1m,
                intervention.Reason,
                new Dictionary<string, string>(intervention.Metadata, StringComparer.Ordinal)
                {
                    ["interventionId"] = intervention.InterventionId,
                    ["businessObjectDefinitionId"] = intervention.BusinessObjectDefinitionId
                }))
        ];
        return schedule with
        {
            DemandChanges = demandChanges,
            Metadata = new Dictionary<string, string>(schedule.Metadata, StringComparer.Ordinal)
            {
                ["runtimeInterventionCount"] = related.Length.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static CapexPlanState CreateCapacityExpansionCapexPlan(
        ScenarioBusinessModelState scenario,
        SimulationRunInterventionState intervention)
    {
        BusinessUnitState businessUnit = scenario.BusinessUnits.FirstOrDefault(unit => string.Equals(unit.BusinessUnitId.Value, intervention.TargetId, StringComparison.Ordinal))
            ?? scenario.BusinessUnits.FirstOrDefault(unit => string.Equals(unit.Path, intervention.TargetId, StringComparison.OrdinalIgnoreCase))
            ?? scenario.BusinessUnits.First(unit => unit.Kind == BusinessUnitKind.CostCenter);
        string capexId = $"runtime-capacity:{EconomySimulatorIdentifiers.NormalizeSegment(intervention.InterventionId)}";
        return new CapexPlanState(
            scenario.ProjectId,
            capexId,
            businessUnit.BusinessUnitId,
            intervention.DisplayName,
            Amount.FromDecimal(intervention.Amount ?? 0m),
            intervention.EffectiveDate,
            intervention.DepreciationMonths ?? 60,
            new Dictionary<string, string>(intervention.Metadata, StringComparer.Ordinal)
            {
                ["interventionId"] = intervention.InterventionId,
                ["businessObjectDefinitionId"] = intervention.BusinessObjectDefinitionId,
                ["capitalAllocationProcess"] = "runtime-capacity-expansion",
                ["liquidityReview"] = "required"
            });
    }

    private static IReadOnlyDictionary<string, decimal> BuildScenarioKpiValues(
        ScenarioBusinessModelState scenario,
        IReadOnlyList<EconomicEventState> events,
        EconomyProjection projection,
        IReadOnlyList<SimulationIssue> issues,
        IReadOnlyList<SimulationRunInterventionState> interventions)
    {
        decimal revenue = events
            .Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.AccrualOnly)
            .Sum(x => x.Amount.Value);
        decimal cashReceipts = events
            .Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.CashOnly)
            .Sum(x => x.Amount.Value);
        decimal profitAndLossExpense = events
            .Where(x => x.ImpactKind is EconomicImpactKind.CashAndAccrual or EconomicImpactKind.NonCashPnl or EconomicImpactKind.AccrualOnly)
            .Where(EconomyProjectionService.IsOutflow)
            .Where(x => x.Metadata.TryGetValue(EconomyScenarioMetadataKeys.FinancialStatementSection, out string? section)
                && section is nameof(FinancialStatementSection.Cogs) or nameof(FinancialStatementSection.Opex) or nameof(FinancialStatementSection.Payroll) or nameof(FinancialStatementSection.Depreciation))
            .Sum(x => x.Amount.Value);
        decimal cashOutflow = projection.CashFlow.Sum(x => x.PlannedOutflow.Value + x.CommittedOutflow.Value + x.ActualOutflow.Value + x.SimulatedOutflow.Value);
        decimal cashInflow = projection.CashFlow.Sum(x => x.PlannedInflow.Value + x.CommittedInflow.Value + x.ActualInflow.Value + x.SimulatedInflow.Value);
        decimal closingCash = projection.CashFlow.Count == 0 ? scenario.OpeningCash.Value : projection.CashFlow[^1].BalanceAfter.Value;
        decimal minimumCash = projection.CashFlow.Count == 0 ? scenario.OpeningCash.Value : projection.CashFlow.Min(x => x.BalanceAfter.Value);

        return new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["cashBalance"] = closingCash,
            ["minimumCash"] = minimumCash,
            ["minimumCashFloor"] = scenario.MinimumCash.Value,
            ["cashHeadroom"] = closingCash - scenario.MinimumCash.Value,
            ["revenue"] = revenue,
            ["cashReceipts"] = cashReceipts,
            ["expenses"] = profitAndLossExpense,
            ["cashInflow"] = cashInflow,
            ["cashOutflow"] = cashOutflow,
            ["netCashFlow"] = cashInflow - cashOutflow,
            ["profit"] = revenue - profitAndLossExpense,
            ["serviceHours"] = events.Where(x => x.TokenEffect?.TokenId.Value.Contains(":service-hour:", StringComparison.Ordinal) == true).Sum(x => x.TokenEffect!.Amount.Value),
            ["productFlowUnits"] = events.Where(x => x.TokenEffect?.TokenId.Value.Contains(":product-unit:", StringComparison.Ordinal) == true).Sum(x => x.TokenEffect!.Amount.Value),
            ["interventionCount"] = interventions.Count,
            ["eventCount"] = events.Count,
            ["deficitCount"] = issues.Count(x => x.Code.Contains("deficit", StringComparison.OrdinalIgnoreCase)),
            ["warningCount"] = issues.Count(x => x.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
        };
    }
}
