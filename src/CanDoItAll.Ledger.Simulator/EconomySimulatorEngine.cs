using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Simulator;

public sealed class EconomyProjectionService : IEconomyProjectionService
{
    public EconomyProjection BuildProjection(ProjectionRequest request)
    {
        Amount balance = request.OpeningCash;
        Amount minimum = request.OpeningCash;
        var points = new List<CashFlowProjectionPoint>();
        var issues = new List<SimulationIssue>();

        foreach (EconomicEventState eventState in SortEvents(request.Events))
        {
            Amount plannedInflow = Amount.Zero;
            Amount plannedOutflow = Amount.Zero;
            Amount committedInflow = Amount.Zero;
            Amount committedOutflow = Amount.Zero;
            Amount actualInflow = Amount.Zero;
            Amount actualOutflow = Amount.Zero;
            Amount simulatedInflow = Amount.Zero;
            Amount simulatedOutflow = Amount.Zero;

            bool cashImpact = eventState.ImpactKind is EconomicImpactKind.CashOnly or EconomicImpactKind.CashAndAccrual;
            bool outflow = IsOutflow(eventState);
            if (cashImpact)
            {
                switch (eventState.Status)
                {
                    case EconomicEventStatus.Actual:
                    case EconomicEventStatus.Settled:
                    case EconomicEventStatus.Reconciled:
                        (actualInflow, actualOutflow) = Split(eventState.Amount, outflow);
                        break;
                    case EconomicEventStatus.Committed:
                    case EconomicEventStatus.Invoiced:
                    case EconomicEventStatus.Due:
                    case EconomicEventStatus.PartiallySettled:
                        (committedInflow, committedOutflow) = Split(eventState.Amount, outflow);
                        break;
                    case EconomicEventStatus.Simulated:
                        (simulatedInflow, simulatedOutflow) = Split(eventState.Amount, outflow);
                        break;
                    default:
                        (plannedInflow, plannedOutflow) = Split(eventState.Amount, outflow);
                        break;
                }

                balance = Amount.FromDecimal(balance.Value + plannedInflow.Value + committedInflow.Value + actualInflow.Value + simulatedInflow.Value - plannedOutflow.Value - committedOutflow.Value - actualOutflow.Value - simulatedOutflow.Value);
                if (balance.Value < minimum.Value)
                {
                    minimum = balance;
                }

                if (balance.Value < 0m)
                {
                    issues.Add(new SimulationIssue(
                        $"issue:{eventState.EventId}:deficit",
                        "cash-deficit",
                        "error",
                        eventState.EconomicDate,
                        eventState.SourceBusinessUnitId,
                        new SimulationObjectReference(eventState.EventId),
                        null,
                        Amount.FromDecimal(Math.Abs(balance.Value)),
                        "Cash balance falls below zero after this event.",
                        new Dictionary<string, string>()));
                }
            }

            points.Add(new CashFlowProjectionPoint(
                eventState.EconomicDate,
                eventState.SourceBusinessUnitId,
                eventState.CategoryCode,
                plannedInflow,
                plannedOutflow,
                committedInflow,
                committedOutflow,
                actualInflow,
                actualOutflow,
                simulatedInflow,
                simulatedOutflow,
                balance,
                eventState.Confidence));
        }

        decimal plannedRevenue = request.Events
            .Where(x => x.Kind == EconomicEventKind.Revenue && x.Status is EconomicEventStatus.Planned or EconomicEventStatus.Invoiced)
            .Sum(x => x.Amount.Value);
        decimal actualRevenue = request.Events
            .Where(x => x.Kind == EconomicEventKind.Revenue && x.Status is EconomicEventStatus.Settled or EconomicEventStatus.Reconciled)
            .Sum(x => x.Amount.Value);
        decimal outflows = points.Sum(x => x.PlannedOutflow.Value + x.CommittedOutflow.Value + x.ActualOutflow.Value + x.SimulatedOutflow.Value);
        var kpis = new Dictionary<string, decimal>
        {
            ["openingCash"] = request.OpeningCash.Value,
            ["closingCash"] = balance.Value,
            ["minimumCash"] = minimum.Value,
            ["plannedRevenue"] = plannedRevenue,
            ["actualRevenue"] = actualRevenue,
            ["totalCashOutflow"] = outflows,
            ["deficitCount"] = issues.Count
        };

        return new EconomyProjection(points, kpis, issues);
    }

    internal static IReadOnlyList<EconomicEventState> SortEvents(IReadOnlyList<EconomicEventState> events)
        => events
            .OrderBy(x => x.EconomicDate)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.SourceTemplateId, StringComparer.Ordinal)
            .ThenBy(x => x.EventId, StringComparer.Ordinal)
            .ToArray();

    internal static bool IsOutflow(EconomicEventState eventState)
        => eventState.Kind is EconomicEventKind.Expense
            or EconomicEventKind.Payroll
            or EconomicEventKind.EmployerContribution
            or EconomicEventKind.EmployeeWithholding
            or EconomicEventKind.Tax
            or EconomicEventKind.Capex
            or EconomicEventKind.DebtRepayment
            or EconomicEventKind.DividendOrDistribution
            or EconomicEventKind.InventoryPurchase
            or EconomicEventKind.Cogs;

    private static (Amount Inflow, Amount Outflow) Split(Amount amount, bool outflow)
        => outflow ? (Amount.Zero, amount) : (amount, Amount.Zero);
}

public sealed class EconomyScenarioRunner(
    IEconomyProjectionService projectionService,
    IEconomySimulatorJsonSerializer serializer,
    ITimeProvider timeProvider) : IEconomyScenarioRunner
{
    public ValueTask<ScenarioRunResult> RunAsync(ScenarioRunRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Context.ScenarioId is null)
        {
            throw new InvalidOperationException("Scenario simulation requires a non-null ScenarioId.");
        }

        ScenarioId scenarioId = request.Context.ScenarioId.Value;

        if (request.HorizonEndDate < request.HorizonStartDate)
        {
            throw new ArgumentException("Scenario horizon end date cannot be before start date.", nameof(request));
        }

        IReadOnlyList<EconomicEventState> scheduledEvents = EconomyProjectionService.SortEvents(request.Events)
            .Where(x => x.EconomicDate >= request.HorizonStartDate && x.EconomicDate <= request.HorizonEndDate)
            .Select(x => x with { Status = x.Status == EconomicEventStatus.Planned ? EconomicEventStatus.Simulated : x.Status })
            .ToArray();
        EconomyProjection projection = projectionService.BuildProjection(new ProjectionRequest(request.ProjectId, request.OpeningCash, scheduledEvents));
        DateTimeOffset startedAt = timeProvider.UtcNow;
        ScenarioRunId runId = EconomySimulatorIdentifiers.RunId(request.ProjectId, scenarioId, startedAt);
        var runState = new ScenarioRunState(
            request.ProjectId,
            scenarioId,
            runId.Value,
            request.Name,
            null,
            null,
            startedAt,
            request.HorizonStartDate,
            request.HorizonEndDate,
            request.RunMode.ToString(),
            request.Seed,
            request.AssumptionOverrides ?? new Dictionary<string, string>(),
            request.IncludedTemplateIds ?? Array.Empty<string>(),
            request.IncludedBusinessUnits ?? Array.Empty<BusinessUnitId>(),
            projection.Issues.Count == 0 ? "completed" : "completed-with-issues",
            $"simres:{EconomySimulatorIdentifiers.NormalizeSegment(request.ProjectId)}:{EconomySimulatorIdentifiers.NormalizeSegment(scenarioId.Value)}:{EconomySimulatorIdentifiers.NormalizeSegment(runId.Value)}",
            new Dictionary<string, string>());

        Amount closingCash = projection.CashFlow.Count == 0 ? request.OpeningCash : projection.CashFlow[^1].BalanceAfter;
        Amount minimumCash = projection.CashFlow.Count == 0
            ? request.OpeningCash
            : Amount.FromDecimal(projection.CashFlow.Min(x => x.BalanceAfter.Value));
        SimulationIssue? firstDeficit = projection.Issues.FirstOrDefault(x => string.Equals(x.Code, "cash-deficit", StringComparison.Ordinal));
        var resultState = new SimulationResultState(
            request.ProjectId,
            scenarioId,
            runId.Value,
            timeProvider.UtcNow,
            request.HorizonStartDate,
            request.HorizonEndDate,
            request.OpeningCash,
            closingCash,
            minimumCash,
            firstDeficit?.Date,
            firstDeficit?.BusinessUnitId,
            firstDeficit?.Amount ?? Amount.Zero,
            Amount.FromDecimal(Math.Max(0m, Math.Abs(minimumCash.Value) * 1.15m)),
            projection.CashFlow,
            projection.Issues,
            projection.Kpis,
            new Dictionary<string, string>
            {
                ["eventsHash"] = serializer.HashJson(scheduledEvents),
                ["projectionHash"] = serializer.HashJson(projection.CashFlow)
            },
            new Dictionary<string, string>());

        var drafts = new[]
        {
            serializer.CreateDraft(EconomySimulatorObjectTypes.ScenarioRun, runState),
            serializer.CreateDraft(EconomySimulatorObjectTypes.SimulationResult, resultState)
        };

        return ValueTask.FromResult(new ScenarioRunResult(runState, resultState, drafts));
    }
}

public sealed class ActualTrackingService : IActualTrackingService
{
    public ActualEconomicEntryState ValidateActualEntry(ActualEntryRequest request)
    {
        var messages = new List<string>();
        if (request.Context.ScenarioId is not null)
        {
            messages.Add("Actual operational tracking must use ScenarioId = null.");
        }

        if (request.Event.Status is not (EconomicEventStatus.Settled or EconomicEventStatus.Reconciled or EconomicEventStatus.Committed or EconomicEventStatus.Invoiced))
        {
            messages.Add("Actual entries should be settled, reconciled, committed, or invoiced.");
        }

        if (request.Event.Amount.Value < 0m)
        {
            messages.Add("Actual event amount cannot be negative.");
        }

        return new ActualEconomicEntryState(
            request.ProjectId,
            request.EntryId,
            request.Source,
            request.Event,
            messages.Count == 0 ? "valid" : "invalid",
            messages,
            request.Metadata ?? new Dictionary<string, string>());
    }
}

public sealed class EconomyCalibrationService : IEconomyCalibrationService
{
    public CalibrationRecommendationState RecommendAssumptionUpdate(
        string projectId,
        string metricCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal plannedValue,
        decimal actualValue,
        IReadOnlyList<string> evidenceObjectIds)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException("Calibration period end cannot be before start.");
        }

        decimal delta = actualValue - plannedValue;
        decimal confidence = plannedValue == 0m
            ? 0.5m
            : Math.Clamp(Math.Abs(delta / plannedValue), 0.1m, 0.95m);

        return new CalibrationRecommendationState(
            projectId,
            $"calib:{EconomySimulatorIdentifiers.NormalizeSegment(projectId)}:{EconomySimulatorIdentifiers.NormalizeSegment(metricCode)}:{periodEnd:yyyyMMdd}",
            metricCode,
            periodStart,
            periodEnd,
            plannedValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            actualValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            confidence,
            evidenceObjectIds,
            "needs-review",
            new Dictionary<string, string>
            {
                ["delta"] = delta.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
            });
    }
}

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class EconomySimulatorServiceCollectionExtensions
{
    public static IServiceCollection AddEconomySimulator(this IServiceCollection services)
    {
        services.TryAddSingleton<ITimeProvider, SystemTimeProvider>();
        services.TryAddSingleton<IEconomySimulatorJsonSerializer, EconomySimulatorJsonSerializer>();
        services.TryAddSingleton<IEconomySimulatorObjectTypeCatalog>(EconomySimulatorObjectTypeCatalog.Instance);
        services.TryAddSingleton<IEconomySimulatorTemplateProvider, EconomySimulatorTemplateProvider>();
        services.TryAddScoped<IEconomySimulatorTemplateMaterializer, EconomySimulatorTemplateMaterializer>();
        services.TryAddScoped<IBusinessUnitRegistry, BusinessUnitRegistry>();
        services.TryAddScoped<IBusinessUnitAddressResolver, BusinessUnitAddressResolver>();
        services.TryAddScoped<IEconomicEventGenerator, EconomicEventGenerator>();
        services.TryAddScoped<IPayrollRuleProvider, GenericPayrollRuleProvider>();
        services.TryAddScoped<ITaxRuleProvider, GenericTaxRuleProvider>();
        services.TryAddScoped<IDepreciationRuleProvider, GenericDepreciationRuleProvider>();
        services.TryAddScoped<IEconomicPostingService, EconomicPostingService>();
        services.TryAddScoped<IExternalBoundaryPostingService, ExternalBoundaryPostingService>();
        services.TryAddSingleton<IEconomyProjectionService, EconomyProjectionService>();
        services.TryAddScoped<IEconomyScenarioRunner, EconomyScenarioRunner>();
        services.TryAddSingleton<IEconomyScenarioTemplateCatalog, EconomyScenarioTemplateCatalog>();
        services.TryAddSingleton<IEconomyScenarioEventCompiler, EconomyScenarioEventCompiler>();
        services.TryAddSingleton<ISimulationScenarioRunMaterializer, EconomyScenarioRunMaterializer>();
        services.TryAddSingleton<ISimulationScenarioProjectFactory, SimulationScenarioProjectFactory>();
        services.TryAddScoped<IActualTrackingService, ActualTrackingService>();
        services.TryAddScoped<IEconomyCalibrationService, EconomyCalibrationService>();
        services.TryAddSingleton<ISimulationProjectValidationService, SimulationProjectValidationService>();
        services.TryAddSingleton<ISimulationProjectImportExportService, SimulationProjectImportExportService>();
        services.TryAddSingleton<ISimulationRunObservationService, SimulationRunObservationService>();
        services.TryAddSingleton<ISimulationRunLifecycleService, InMemorySimulationRunLifecycleService>();
        services.AddEconomySimulatorApi();
        return services;
    }
}
