using CanDoItAll.Economy.Simulator;
using CanDoItAll.Economy.Simulator.Components.Samples;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class SimulationProjectManagementTests
{
    [Fact]
    public void ProjectManifest_RoundtripsDeterministicallyWithBoRulesAndFinancialInputs()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        var serializer = new EconomySimulatorJsonSerializer();
        var service = new SimulationProjectImportExportService(serializer, new SimulationProjectValidationService());
        DateTimeOffset exportedAt = DateTimeOffset.Parse("2026-05-14T12:00:00Z");

        SimulationProjectExportManifest first = service.CreateManifest(project, exportedAt);
        SimulationProjectExportManifest second = service.CreateManifest(project, exportedAt);
        string firstJson = service.SerializeManifest(first);
        string secondJson = service.SerializeManifest(second);
        SimulationProjectImportPreview preview = service.PreviewImport(firstJson);

        Assert.Equal(firstJson, secondJson);
        Assert.True(preview.CanImport, string.Join("; ", preview.Validation.Messages.Select(message => $"{message.Code}:{message.Message}")));
        Assert.NotNull(preview.Manifest);
        Assert.Contains(preview.Manifest!.Project.BusinessObjectDefinitions, x => x.RuleDefinition is not null);
        Assert.NotEmpty(preview.Manifest.Project.FinancialInputProfiles.Single().Inputs);
        Assert.Contains("businessObjectDefinitions", preview.Manifest.ContentHashes.Keys);
        Assert.Contains("financialInputProfiles", preview.Manifest.ContentHashes.Keys);
    }

    [Fact]
    public void ProjectValidation_CatchesBrokenReferencesAndInvalidFinancialInputs()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        BusinessUnitState brokenBusinessUnit = project.BusinessUnits[1] with
        {
            ParentBusinessUnitId = EconomySimulatorIdentifiers.BusinessUnitId(project.Summary.ProjectId, "missing-parent")
        };
        SimulationBusinessObjectDefinitionState brokenDefinition = project.BusinessObjectDefinitions[0] with
        {
            DefinitionId = project.BusinessObjectDefinitions[1].DefinitionId,
            FolderId = "folder:missing",
            ObjectType = "economy-simulator.unknown-object"
        };
        SimulationFinancialInputProfileState brokenProfile = project.FinancialInputProfiles[0] with
        {
            Inputs =
            [
                project.FinancialInputProfiles[0].Inputs[0] with
                {
                    NumericValue = 1.5m,
                    ValidFrom = new DateOnly(2026, 12, 31),
                    ValidTo = new DateOnly(2026, 1, 1)
                }
            ]
        };
        SimulationRunSetupState brokenSetup = project.RunSetups[0] with
        {
            StepCount = 0,
            IncludedBusinessUnits = [EconomySimulatorIdentifiers.BusinessUnitId(project.Summary.ProjectId, "missing-bu")]
        };
        SimulationProjectState invalid = project with
        {
            BusinessUnits = [project.BusinessUnits[0], brokenBusinessUnit],
            BusinessObjectDefinitions = [project.BusinessObjectDefinitions[1], brokenDefinition],
            FinancialInputProfiles = [brokenProfile],
            RunSetups = [brokenSetup]
        };

        SimulationProjectValidationResult result = new SimulationProjectValidationService().Validate(invalid);
        string[] codes = result.Messages.Select(x => x.Code).ToArray();

        Assert.False(result.IsValid);
        Assert.Contains("invalid-business-unit-parent", codes);
        Assert.Contains("duplicate-bo-definition-id", codes);
        Assert.Contains("invalid-bo-folder", codes);
        Assert.Contains("unknown-bo-object-type", codes);
        Assert.Contains("invalid-financial-input-window", codes);
        Assert.Contains("invalid-financial-rate", codes);
        Assert.Contains("invalid-run-step-count", codes);
        Assert.Contains("invalid-run-included-business-unit", codes);
    }

    [Fact]
    public void RunSetupHash_IgnoresUiSpeedButIncludesSemanticSetup()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        var service = new SimulationProjectImportExportService(new EconomySimulatorJsonSerializer(), new SimulationProjectValidationService());
        SimulationRunSetupState normalSpeed = project.RunSetups[0] with { UiSpeed = SimulationUiRunSpeed.Normal };
        SimulationRunSetupState maxSpeed = project.RunSetups[0] with { UiSpeed = SimulationUiRunSpeed.Max };
        SimulationRunSetupState differentSeed = project.RunSetups[0] with { Seed = project.RunSetups[0].Seed + 1 };

        string normalHash = service.BuildDeterministicRunSetupHash(project, normalSpeed);
        string maxHash = service.BuildDeterministicRunSetupHash(project, maxSpeed);
        string seedHash = service.BuildDeterministicRunSetupHash(project, differentSeed);

        Assert.Equal(normalHash, maxHash);
        Assert.NotEqual(normalHash, seedHash);
    }

    [Fact]
    public void ProjectRuleDefinitions_ReuseAccountRuleDefinitionFormat()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        IReadOnlyList<SimulationBusinessObjectDefinitionState> ruleDefinitions = project.BusinessObjectDefinitions
            .Where(x => x.Kind == SimulationBusinessObjectDefinitionKind.RuleDefinition)
            .ToArray();

        Assert.NotEmpty(ruleDefinitions);
        Assert.All(ruleDefinitions, definition =>
        {
            Assert.NotNull(definition.RuleDefinition);
            Assert.True(RuleTokenAuthority.TryValidate(definition.RuleDefinition!, out IReadOnlyList<string> errors), string.Join("; ", errors));
            Assert.Equal(EconomySimulatorObjectTypes.RuleState, definition.ObjectType);
        });
    }

    [Fact]
    public void RunLifecycle_AutomaticStartCompletesAtConfiguredEnd()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();
        DateTimeOffset started = DateTimeOffset.Parse("2026-05-16T10:15:00Z");

        SimulationRunCommandResult result = service.StartRun(project, project.RunSetups[0].SetupId, started, SimulationRunMode.Automatic);
        SimulationRunConsoleState? run = result.RunId is null ? null : service.GetRun(result.RunId);

        Assert.True(result.Succeeded, string.Join("; ", result.Messages.Select(message => message.Message)));
        Assert.NotNull(run);
        Assert.Equal(SimulationRunLifecycleStatus.Completed, run!.Run.Status);
        Assert.Equal(SimulationRunMode.Automatic, run.Run.RunMode);
        Assert.Equal(run.Run.TotalSteps, run.Run.CurrentStep);
        Assert.Equal(run.Run.TotalSteps, run.Run.SelectedStep);
        Assert.Equal(run.Run.TotalSteps, run.Run.AvailableStep);
        Assert.True(run.Run.SelectedStepHasData);
        Assert.Equal(run.Run.TotalSteps, run.StepSnapshots.Count);
        Assert.Equal(project.Summary.ProjectId, run.Run.ProjectId);
        Assert.NotEmpty(run.Events);
        Assert.NotEmpty(run.Pipeline);
        Assert.NotEmpty(run.ResourceUsage.Metrics);
        Assert.Contains(run.Kpis, kpi => string.Equals(kpi.Code, "realGdp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunLifecycle_LiveAutomaticStartRegistersBeforeCompletionAndMaterializes()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        SimulationRunSetupState setup = project.RunSetups[0] with
        {
            StepCount = 30,
            HorizonEndDate = project.RunSetups[0].HorizonStartDate.AddMonths(29)
        };
        project = project with { RunSetups = [setup] };
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();

        SimulationRunCommandResult result = await service.StartRunAsync(project, setup.SetupId, DateTimeOffset.Parse("2026-05-16T10:15:00Z"), SimulationRunMode.Automatic);
        SimulationRunConsoleState initial = service.GetRun(result.RunId!)!;

        Assert.True(result.Succeeded, string.Join("; ", result.Messages.Select(message => message.Message)));
        Assert.Equal(SimulationRunLifecycleStatus.Running, initial.Run.Status);
        Assert.Equal(SimulationRunMode.Automatic, initial.Run.RunMode);
        Assert.Equal(1, initial.Run.AvailableStep);
        Assert.Contains(service.ListRuns(project.Summary.ProjectId), run => run.RunId == result.RunId);

        SimulationRunConsoleState current = initial;
        for (int attempt = 0; attempt < 30 && current.Run.Status != SimulationRunLifecycleStatus.Completed; attempt++)
        {
            await Task.Delay(200);
            current = service.GetRun(result.RunId!)!;
        }

        Assert.Equal(SimulationRunLifecycleStatus.Completed, current.Run.Status);
        Assert.Equal(setup.StepCount, current.Run.AvailableStep);
        Assert.Equal(setup.StepCount, current.Run.SelectedStep);
    }

    [Fact]
    public void RunLifecycle_ManualStartWaitsAtFirstStep()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();
        DateTimeOffset started = DateTimeOffset.Parse("2026-05-16T10:15:00Z");

        SimulationRunCommandResult result = service.StartRun(project, project.RunSetups[0].SetupId, started, SimulationRunMode.Manual);
        SimulationRunConsoleState? run = result.RunId is null ? null : service.GetRun(result.RunId);

        Assert.True(result.Succeeded, string.Join("; ", result.Messages.Select(message => message.Message)));
        Assert.NotNull(run);
        Assert.Equal(SimulationRunLifecycleStatus.Paused, run!.Run.Status);
        Assert.Equal(SimulationRunMode.Manual, run.Run.RunMode);
        Assert.Equal(1, run.Run.CurrentStep);
        Assert.Equal(1, run.Run.SelectedStep);
        Assert.Equal(1, run.Run.AvailableStep);
        Assert.True(run.Run.SelectedStepHasData);
        Assert.Single(run.StepSnapshots);
    }

    [Fact]
    public void RunLifecycle_ManualForwardCreatesMissingAndBackwardLoadsHistory()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();
        DateTimeOffset started = DateTimeOffset.Parse("2026-05-16T10:15:00Z");
        SimulationRunCommandResult start = service.StartRun(project, project.RunSetups[0].SetupId, started, SimulationRunMode.Manual);
        SimulationRunConsoleState initial = service.GetRun(start.RunId!)!;
        string stepOneHash = initial.Run.OutputHash;

        SimulationRunCommandResult next = service.StepRun(initial.Run.RunId, SimulationRunStepRequestKind.Next);
        SimulationRunConsoleState stepTwo = service.GetRun(next.RunId!)!;
        string stepTwoHash = stepTwo.Run.OutputHash;

        Assert.True(next.Succeeded, string.Join("; ", next.Messages.Select(message => message.Message)));
        Assert.Equal(2, stepTwo.Run.SelectedStep);
        Assert.Equal(2, stepTwo.Run.AvailableStep);
        Assert.Equal(2, stepTwo.StepSnapshots.Count);

        SimulationRunCommandResult previous = service.StepRun(initial.Run.RunId, SimulationRunStepRequestKind.Previous);
        SimulationRunConsoleState backAtOne = service.GetRun(previous.RunId!)!;

        Assert.True(previous.Succeeded, string.Join("; ", previous.Messages.Select(message => message.Message)));
        Assert.Equal(1, backAtOne.Run.SelectedStep);
        Assert.Equal(2, backAtOne.Run.AvailableStep);
        Assert.Equal(stepOneHash, backAtOne.Run.OutputHash);

        SimulationRunCommandResult nextAgain = service.StepRun(initial.Run.RunId, SimulationRunStepRequestKind.Next);
        SimulationRunConsoleState backAtTwo = service.GetRun(nextAgain.RunId!)!;

        Assert.True(nextAgain.Succeeded, string.Join("; ", nextAgain.Messages.Select(message => message.Message)));
        Assert.Equal(2, backAtTwo.Run.SelectedStep);
        Assert.Equal(2, backAtTwo.Run.AvailableStep);
        Assert.Equal(stepTwoHash, backAtTwo.Run.OutputHash);
    }

    [Fact]
    public void RunLifecycle_RejectsStepOutsideRunRange()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();
        SimulationRunCommandResult start = service.StartRun(project, project.RunSetups[0].SetupId, DateTimeOffset.Parse("2026-05-16T10:15:00Z"), SimulationRunMode.Manual);

        SimulationRunCommandResult result = service.StepRun(start.RunId!, SimulationRunStepRequestKind.Select, project.RunSetups[0].StepCount + 1);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, message => message.Code == "invalid-run-step");
    }

    [Fact]
    public void RunLifecycle_ManualInterventionsRecomputeFutureScenarioSnapshots()
    {
        using ServiceProvider provider = BuildScenarioProvider();
        ISimulationProjectWorkspaceService workspace = provider.GetRequiredService<ISimulationProjectWorkspaceService>();
        ISimulationRunLifecycleService lifecycle = provider.GetRequiredService<ISimulationRunLifecycleService>();
        SimulationProjectState project = workspace.GetProject("s2-bakery")!;
        SimulationRunSetupState setup = project.RunSetups[0];

        SimulationRunCommandResult baselineStart = lifecycle.StartRun(project, setup.SetupId, DateTimeOffset.Parse("2026-05-16T10:15:00Z"), SimulationRunMode.Manual);
        lifecycle.StepRun(baselineStart.RunId!, SimulationRunStepRequestKind.Next);
        lifecycle.StepRun(baselineStart.RunId!, SimulationRunStepRequestKind.Next);
        SimulationRunConsoleState baselineStepThree = lifecycle.GetRun(baselineStart.RunId!)!;
        lifecycle.StepRun(baselineStart.RunId!, SimulationRunStepRequestKind.Next);
        SimulationRunConsoleState baselineStepFour = lifecycle.GetRun(baselineStart.RunId!)!;

        SimulationRunCommandResult interventionStart = lifecycle.StartRun(project, setup.SetupId, DateTimeOffset.Parse("2026-05-16T10:25:00Z"), SimulationRunMode.Manual);
        lifecycle.StepRun(interventionStart.RunId!, SimulationRunStepRequestKind.Next);
        SimulationRunConsoleState stepTwo = lifecycle.GetRun(interventionStart.RunId!)!;
        string stepTwoHash = stepTwo.Run.OutputHash;

        SimulationRunInterventionCommandResult demand = lifecycle.ApplyIntervention(
            interventionStart.RunId!,
            new SimulationRunInterventionRequest(
                SimulationRunInterventionKind.DemandChange,
                "SDF-001-demand",
                new DateOnly(2026, 3, 1),
                Multiplier: 2m,
                DisplayName: "Larger sourdough demand",
                Reason: "Customer demand shock"));
        SimulationRunConsoleState afterDemand = lifecycle.GetRun(interventionStart.RunId!)!;

        Assert.True(demand.Command.Succeeded, string.Join("; ", demand.Command.Messages.Select(message => message.Message)));
        Assert.NotNull(demand.Intervention);
        Assert.Equal(2, afterDemand.Run.SelectedStep);
        Assert.Equal(2, afterDemand.Run.AvailableStep);
        Assert.NotEqual(stepTwoHash, afterDemand.Run.OutputHash);
        Assert.Contains(afterDemand.Project.BusinessObjectDefinitions, definition => definition.ObjectType == EconomySimulatorObjectTypes.RuntimeIntervention);

        lifecycle.StepRun(interventionStart.RunId!, SimulationRunStepRequestKind.Next);
        SimulationRunConsoleState demandStepThree = lifecycle.GetRun(interventionStart.RunId!)!;

        Assert.True(GraphTotal(demandStepThree, ScenarioGraphMetric.Revenue, "nonCashIncome") > GraphTotal(baselineStepThree, ScenarioGraphMetric.Revenue, "nonCashIncome"));

        BusinessUnitId production = EconomySimulatorIdentifiers.BusinessUnitId("s2-bakery", "production");
        SimulationRunInterventionCommandResult productionLine = lifecycle.ApplyIntervention(
            interventionStart.RunId!,
            new SimulationRunInterventionRequest(
                SimulationRunInterventionKind.CapacityExpansion,
                production.Value,
                new DateOnly(2026, 4, 1),
                Amount: 125000m,
                DepreciationMonths: 48,
                DisplayName: "Second bakery production line",
                Reason: "Capacity request after demand shock"));

        Assert.True(productionLine.Command.Succeeded, string.Join("; ", productionLine.Command.Messages.Select(message => message.Message)));

        lifecycle.StepRun(interventionStart.RunId!, SimulationRunStepRequestKind.Next);
        SimulationRunConsoleState capacityStepFour = lifecycle.GetRun(interventionStart.RunId!)!;

        Assert.True(GraphTotal(capacityStepFour, ScenarioGraphMetric.Expense, "outflow") > GraphTotal(baselineStepFour, ScenarioGraphMetric.Expense, "outflow"));
        Assert.Contains(capacityStepFour.Observation.Graphs.Single(graph => graph.Filter.Metric == ScenarioGraphMetric.Expense).DrilldownRows,
            row => row.CategoryCode == "capex-production-prep" && row.SourceObjectId.StartsWith("runtime-capacity:", StringComparison.Ordinal));
        Assert.Contains(capacityStepFour.Kpis, kpi => kpi.Code == "runtimeInterventions" && kpi.Value == "2");
        Assert.Contains(capacityStepFour.OutputSources, source => source.OutputCode == "runtimeInterventions");
    }

    [Fact]
    public void RunLifecycle_InvalidCommandReturnsVisibleError()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();
        SimulationRunConsoleState completed = service.EnsureProjectRuns(project).Single(run => run.Run.Status == SimulationRunLifecycleStatus.Completed);

        SimulationRunCommandResult result = service.ApplyCommand(completed.Run.RunId, SimulationRunCommandKind.Pause);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, message => message.Severity == SimulationValidationSeverity.Error && message.Code == "invalid-run-command");
    }

    [Fact]
    public void RunLifecycle_FailedSeedPreservesErrorsAndArtifacts()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();
        SimulationRunConsoleState failed = service.EnsureProjectRuns(project).Single(run => run.Run.Status == SimulationRunLifecycleStatus.Failed);

        Assert.True(failed.Run.ErrorCount > 0);
        Assert.Contains(failed.Observation.Issues, issue => issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(failed.Observation.Artifacts);
        Assert.Contains(failed.Events, item => item.Level == SimulationValidationSeverity.Error);
    }

    [Fact]
    public void RunLifecycle_OutputSourceMapCoversMacroKpis()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService service = CreateLifecycleService();
        SimulationRunConsoleState run = service.EnsureProjectRuns(project)[0];
        string[] required = ["realGdp", "cpi", "unemployment", "policyRate", "budgetBalance", "currentAccount"];

        foreach (string outputCode in required)
        {
            Assert.Contains(run.OutputSources, source => string.Equals(source.OutputCode, outputCode, StringComparison.Ordinal)
                && source.BusinessUnitId is not null
                && !string.IsNullOrWhiteSpace(source.SourceKind));
        }
    }

    private static InMemorySimulationRunLifecycleService CreateLifecycleService()
    {
        var serializer = new EconomySimulatorJsonSerializer();
        var validation = new SimulationProjectValidationService();
        var importExport = new SimulationProjectImportExportService(serializer, validation);
        var observation = new SimulationRunObservationService(serializer);
        return new InMemorySimulationRunLifecycleService(validation, importExport, observation, serializer, new SystemTimeProvider());
    }

    private static ServiceProvider BuildScenarioProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITimeProvider>(new FixedTimeProvider(DateTimeOffset.Parse("2026-05-16T10:30:00Z")));
        services.AddEconomySimulator();
        return services.BuildServiceProvider();
    }

    private static decimal GraphTotal(SimulationRunConsoleState run, ScenarioGraphMetric metric, string key)
        => run.Observation.Graphs.Single(graph => graph.Filter.Metric == metric).Totals[key];

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : ITimeProvider
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
