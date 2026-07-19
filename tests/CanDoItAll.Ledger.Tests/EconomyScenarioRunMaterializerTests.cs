using CanDoItAll.Economy.Simulator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyScenarioRunMaterializerTests
{
    [Fact]
    public void TryMaterialize_CompilesScenarioProjectionAndHashesDeterministically()
    {
        ScenarioFixture fixture = CreateFixture("S1");
        SimulationScenarioRunMaterializationRequest request = CreateRequest(fixture.Project, currentStep: 3);

        SimulationScenarioRunMaterialization? first = fixture.Materializer.TryMaterialize(request);
        SimulationScenarioRunMaterialization? second = fixture.Materializer.TryMaterialize(request);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEmpty(first!.Events);
        Assert.NotEmpty(first.Result.CashFlow);
        Assert.Equal(first.Events.Count, first.KpiValues["eventCount"]);
        Assert.Equal(first.OutputHash, first.Result.OutputHashes["result"]);
        Assert.Equal(first.OutputHash, second!.OutputHash);
        Assert.Equal(first.Result.OutputHashes, second.Result.OutputHashes);
        Assert.Equal("S1", first.Result.Metadata["scenarioCode"]);
        Assert.Equal("3", first.Result.Metadata["currentStep"]);
        Assert.All(first.Events, economicEvent => Assert.Equal(request.RunId, economicEvent.Metadata["runId"]));
    }

    [Fact]
    public void TryMaterialize_AppliesDemandInterventionToFutureScenarioEvents()
    {
        ScenarioFixture fixture = CreateFixture("S2");
        DateTimeOffset appliedAtUtc = DateTimeOffset.Parse("2026-02-15T10:30:00Z");
        var intervention = new SimulationRunInterventionState(
            "intervention:demand-shock",
            SimulationRunInterventionKind.DemandChange,
            SimulationRunInterventionStatus.Applied,
            "SDF-001-demand",
            "Larger sourdough demand",
            new DateOnly(2026, 3, 1),
            null,
            2m,
            null,
            null,
            "Customer demand shock",
            2,
            appliedAtUtc,
            appliedAtUtc,
            "definition:runtime-demand-shock",
            new Dictionary<string, string>(StringComparer.Ordinal));
        SimulationScenarioRunMaterializationRequest baselineRequest = CreateRequest(fixture.Project, currentStep: 3);
        SimulationScenarioRunMaterializationRequest interventionRequest = baselineRequest with
        {
            Interventions = [intervention]
        };
        SimulationScenarioRunMaterializationRequest priorBaselineRequest = CreateRequest(fixture.Project, currentStep: 2);
        SimulationScenarioRunMaterializationRequest priorInterventionRequest = priorBaselineRequest with
        {
            Interventions = [intervention]
        };

        SimulationScenarioRunMaterialization baseline = fixture.Materializer.TryMaterialize(baselineRequest)!;
        SimulationScenarioRunMaterialization changed = fixture.Materializer.TryMaterialize(interventionRequest)!;
        SimulationScenarioRunMaterialization priorBaseline = fixture.Materializer.TryMaterialize(priorBaselineRequest)!;
        SimulationScenarioRunMaterialization priorChanged = fixture.Materializer.TryMaterialize(priorInterventionRequest)!;

        (string EventId, DateOnly Date, decimal Amount, EconomicEventStatus Status)[] priorBaselineSequence = priorBaseline.Events
            .Where(economicEvent => economicEvent.EconomicDate < intervention.EffectiveDate)
            .Select(economicEvent => (economicEvent.EventId, economicEvent.EconomicDate, economicEvent.Amount.Value, economicEvent.Status))
            .ToArray();
        (string EventId, DateOnly Date, decimal Amount, EconomicEventStatus Status)[] priorChangedSequence = priorChanged.Events
            .Where(economicEvent => economicEvent.EconomicDate < intervention.EffectiveDate)
            .Select(economicEvent => (economicEvent.EventId, economicEvent.EconomicDate, economicEvent.Amount.Value, economicEvent.Status))
            .ToArray();

        Assert.Equal(priorBaselineSequence, priorChangedSequence);
        Assert.True(changed.KpiValues["revenue"] > baseline.KpiValues["revenue"]);
        Assert.Equal(1m, changed.KpiValues["interventionCount"]);
        Assert.NotEqual(baseline.OutputHash, changed.OutputHash);
        Assert.Equal("1", changed.Result.Metadata["interventionCount"]);
        Assert.Same(intervention, Assert.Single(changed.Interventions));
    }

    [Fact]
    public void ProjectValidation_RejectsScenarioProjectWithoutScenarioCode()
    {
        ScenarioFixture fixture = CreateFixture("S1");
        var metadata = new Dictionary<string, string>(fixture.Project.Metadata, StringComparer.Ordinal);
        metadata.Remove("scenarioCode");
        SimulationProjectState invalid = fixture.Project with { Metadata = metadata };

        SimulationProjectValidationResult validation = new SimulationProjectValidationService().Validate(invalid);

        SimulationProjectValidationMessage message = Assert.Single(
            validation.Messages,
            candidate => candidate.Code == "missing-scenario-code");
        Assert.False(validation.IsValid);
        Assert.Equal(SimulationValidationSeverity.Error, message.Severity);
        Assert.Equal("metadata.scenarioCode", message.Path);
    }

    [Fact]
    public void AddEconomySimulator_ComposesMaterializerAndRequiresItAtLifecycleBoundary()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITimeProvider>(new SystemTimeProvider());
        services.AddEconomySimulator();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsType<EconomyScenarioRunMaterializer>(provider.GetRequiredService<ISimulationScenarioRunMaterializer>());
        Assert.IsType<InMemorySimulationRunLifecycleService>(provider.GetRequiredService<ISimulationRunLifecycleService>());

        System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(InMemorySimulationRunLifecycleService).GetConstructors());
        System.Reflection.ParameterInfo materializerParameter = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(ISimulationScenarioRunMaterializer));
        Assert.False(materializerParameter.HasDefaultValue);
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(IEconomyScenarioEventCompiler));
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(IEconomyProjectionService));
        Assert.False(typeof(ISimulationScenarioRunMaterializer).IsNested);
        Assert.False(typeof(SimulationScenarioRunMaterializationRequest).IsNested);
        Assert.False(typeof(SimulationScenarioRunMaterialization).IsNested);
    }

    private static ScenarioFixture CreateFixture(string scenarioCode)
    {
        var serializer = new EconomySimulatorJsonSerializer();
        var catalog = new EconomyScenarioTemplateCatalog();
        SimulationProjectState project = new SimulationScenarioProjectFactory(catalog, serializer)
            .CreateScenarioProjects(DateTimeOffset.Parse("2026-05-15T12:00:00Z"))
            .Single(candidate => candidate.Metadata["scenarioCode"] == scenarioCode);
        var materializer = new EconomyScenarioRunMaterializer(
            catalog,
            new EconomyScenarioEventCompiler(serializer),
            new EconomyProjectionService(),
            serializer);
        return new ScenarioFixture(project, materializer);
    }

    private static SimulationScenarioRunMaterializationRequest CreateRequest(
        SimulationProjectState project,
        int currentStep)
    {
        SimulationRunSetupState setup = project.RunSetups[0];
        DateOnly currentDate = setup.HorizonStartDate.AddMonths(currentStep - 1);
        DateOnly stepEndDate = currentDate.AddMonths(1).AddDays(-1);
        if (stepEndDate > setup.HorizonEndDate)
        {
            stepEndDate = setup.HorizonEndDate;
        }

        return new SimulationScenarioRunMaterializationRequest(
            project,
            setup,
            "run:materializer-characterization",
            SimulationRunLifecycleStatus.Paused,
            DateTimeOffset.Parse("2026-01-01T10:00:00Z"),
            null,
            currentStep,
            currentDate,
            stepEndDate,
            [],
            []);
    }

    private sealed record ScenarioFixture(
        SimulationProjectState Project,
        EconomyScenarioRunMaterializer Materializer);
}
