using System.Globalization;

namespace CanDoItAll.Economy.Simulator;

public interface ISimulationRunLifecycleService
{
    IReadOnlyList<SimulationRunConsoleState> EnsureProjectRuns(SimulationProjectState project);

    SimulationRunConsoleState? GetRun(string runId);

    IReadOnlyList<SimulationRunRegistryRecord> ListRuns(string? projectId = null);

    SimulationRunCommandResult StartRun(
        SimulationProjectState project,
        string? setupId = null,
        DateTimeOffset? startedAtUtc = null,
        SimulationRunMode mode = SimulationRunMode.Automatic);

    Task<SimulationRunCommandResult> StartRunAsync(
        SimulationProjectState project,
        string? setupId = null,
        DateTimeOffset? startedAtUtc = null,
        SimulationRunMode mode = SimulationRunMode.Automatic,
        CancellationToken cancellationToken = default);

    SimulationRunCommandResult ApplyCommand(string runId, SimulationRunCommandKind command);

    SimulationRunCommandResult StepRun(string runId, SimulationRunStepRequestKind step, int? targetStep = null);

    SimulationRunInterventionCommandResult ApplyIntervention(string runId, SimulationRunInterventionRequest request);
}

public sealed class InMemorySimulationRunLifecycleService(
    ISimulationProjectValidationService validationService,
    ISimulationProjectImportExportService importExportService,
    ISimulationRunObservationService observationService,
    IEconomySimulatorJsonSerializer serializer,
    ITimeProvider timeProvider,
    IEconomyScenarioTemplateCatalog? scenarioCatalog = null,
    IEconomyScenarioEventCompiler? scenarioCompiler = null,
    IEconomyProjectionService? projectionService = null) : ISimulationRunLifecycleService
{
    private readonly object sync = new();
    private readonly Dictionary<string, SimulationRunSession> runs = new(StringComparer.Ordinal);

    public IReadOnlyList<SimulationRunConsoleState> EnsureProjectRuns(SimulationProjectState project)
    {
        lock (sync)
        {
            if (!IsScenarioProject(project)
                && (!runs.Values.Any(run => string.Equals(run.Selected.Project.Summary.ProjectId, project.Summary.ProjectId, StringComparison.Ordinal))
                    || !runs.ContainsKey("RUN-2026-05-16-001")))
            {
                SeedProjectRuns(project);
            }

            return runs.Values
                .Select(RefreshSelectedState)
                .Where(run => string.Equals(run.Project.Summary.ProjectId, project.Summary.ProjectId, StringComparison.Ordinal))
                .OrderByDescending(run => run.Run.StartedAtUtc)
                .ToArray();
        }
    }

    public SimulationRunConsoleState? GetRun(string runId)
    {
        lock (sync)
        {
            return runs.TryGetValue(runId, out SimulationRunSession? run) ? RefreshSelectedState(run) : null;
        }
    }

    public IReadOnlyList<SimulationRunRegistryRecord> ListRuns(string? projectId = null)
    {
        lock (sync)
        {
            return runs.Values
                .Select(RefreshSelectedState)
                .Where(run => string.IsNullOrWhiteSpace(projectId) || string.Equals(run.Project.Summary.ProjectId, projectId, StringComparison.Ordinal))
                .Select(run => run.Run)
                .OrderByDescending(run => run.StartedAtUtc)
                .ToArray();
        }
    }

    public SimulationRunCommandResult StartRun(
        SimulationProjectState project,
        string? setupId = null,
        DateTimeOffset? startedAtUtc = null,
        SimulationRunMode mode = SimulationRunMode.Automatic)
    {
        SimulationRunSetupState? setup = string.IsNullOrWhiteSpace(setupId)
            ? project.RunSetups.FirstOrDefault()
            : project.RunSetups.FirstOrDefault(candidate => string.Equals(candidate.SetupId, setupId, StringComparison.Ordinal));
        if (setup is null)
        {
            return Failure(null, "missing-run-setup", $"Run setup '{setupId ?? "<default>"}' was not found.", "runSetups", setupId);
        }

        SimulationProjectValidationResult validation = validationService.Validate(project);
        if (!validation.IsValid)
        {
            return new SimulationRunCommandResult(false, null, null, validation.Messages);
        }

        DateTimeOffset started = startedAtUtc ?? timeProvider.UtcNow;
        string runId = EconomySimulatorIdentifiers.RunId(project.Summary.ProjectId, setup.ScenarioId, started).Value;
        SimulationRunSession session = CreateRunSession(
            project,
            setup,
            runId,
            started,
            mode,
            mode == SimulationRunMode.Automatic
                ? "Automatic run materialized to the configured horizon."
                : "Manual run created and waiting for the next step.");

        lock (sync)
        {
            runs[runId] = session;
        }

        return new SimulationRunCommandResult(true, runId, session.Selected.Run.Status, []);
    }

    public Task<SimulationRunCommandResult> StartRunAsync(
        SimulationProjectState project,
        string? setupId = null,
        DateTimeOffset? startedAtUtc = null,
        SimulationRunMode mode = SimulationRunMode.Automatic,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode != SimulationRunMode.Automatic)
        {
            return Task.FromResult(StartRun(project, setupId, startedAtUtc, mode));
        }

        SimulationRunSetupState? setup = string.IsNullOrWhiteSpace(setupId)
            ? project.RunSetups.FirstOrDefault()
            : project.RunSetups.FirstOrDefault(candidate => string.Equals(candidate.SetupId, setupId, StringComparison.Ordinal));
        if (setup is null)
        {
            return Task.FromResult(Failure(null, "missing-run-setup", $"Run setup '{setupId ?? "<default>"}' was not found.", "runSetups", setupId));
        }

        SimulationProjectValidationResult validation = validationService.Validate(project);
        if (!validation.IsValid)
        {
            return Task.FromResult(new SimulationRunCommandResult(false, null, null, validation.Messages));
        }

        DateTimeOffset started = startedAtUtc ?? timeProvider.UtcNow;
        string runId = EconomySimulatorIdentifiers.RunId(project.Summary.ProjectId, setup.ScenarioId, started).Value;
        SimulationRunSession session = CreateLiveAutomaticRunSession(
            project,
            setup,
            runId,
            started,
            "Automatic run started in live mode. Steps will materialize in the background.");

        lock (sync)
        {
            runs[runId] = session;
        }

        _ = MaterializeAutomaticRunAsync(runId, cancellationToken);
        return Task.FromResult(new SimulationRunCommandResult(true, runId, session.Selected.Run.Status, []));
    }

    public SimulationRunCommandResult ApplyCommand(string runId, SimulationRunCommandKind command)
    {
        lock (sync)
        {
            if (!runs.TryGetValue(runId, out SimulationRunSession? session))
            {
                return Failure(runId, "run-not-found", $"Run '{runId}' was not found.", "runs", runId);
            }

            SimulationRunConsoleState state = RefreshSelectedState(session);
            SimulationRunLifecycleStatus current = state.Run.Status;
            SimulationRunLifecycleStatus next = command switch
            {
                SimulationRunCommandKind.Pause when current == SimulationRunLifecycleStatus.Running => SimulationRunLifecycleStatus.Paused,
                SimulationRunCommandKind.Resume when current == SimulationRunLifecycleStatus.Paused => SimulationRunLifecycleStatus.Running,
                SimulationRunCommandKind.Stop when current is SimulationRunLifecycleStatus.Running or SimulationRunLifecycleStatus.Paused or SimulationRunLifecycleStatus.Queued => SimulationRunLifecycleStatus.Stopped,
                SimulationRunCommandKind.Restart => SimulationRunLifecycleStatus.Running,
                _ => current
            };

            if (next == current && command is not SimulationRunCommandKind.Restart)
            {
                return Failure(runId, "invalid-run-command", $"Cannot {command.ToString().ToLowerInvariant()} a run while it is {DisplayStatus(current)}.", "runs.status", runId);
            }

            DateTimeOffset updated = timeProvider.UtcNow;
            if (command == SimulationRunCommandKind.Restart)
            {
                string nextRunId = EconomySimulatorIdentifiers.RunId(state.Project.Summary.ProjectId, state.Setup.ScenarioId, updated).Value;
                SimulationRunSession restarted = CreateRunSession(
                    state.Project,
                    state.Setup,
                    nextRunId,
                    updated,
                    state.Run.RunMode,
                    "Run restarted from console.");
                runs.Remove(runId);
                runs[nextRunId] = restarted;
                return new SimulationRunCommandResult(true, restarted.Selected.Run.RunId, restarted.Selected.Run.Status, []);
            }

            SimulationRunConsoleState updatedState = CreateConsoleState(
                state.Project,
                state.Setup,
                state.Run.RunId,
                next,
                state.Run.StartedAtUtc,
                next is SimulationRunLifecycleStatus.Stopped or SimulationRunLifecycleStatus.Completed or SimulationRunLifecycleStatus.CompletedWithWarnings or SimulationRunLifecycleStatus.Failed
                    ? updated
                    : null,
                state.Run.CurrentStep,
                state.Run.RunMode,
                state.Run.AvailableStep,
                $"Run command applied: {command}.",
                session.SnapshotMetadata,
                session.Interventions);

            session.Snapshots[state.Run.CurrentStep] = updatedState;
            session.SelectedStep = updatedState.Run.CurrentStep;
            return new SimulationRunCommandResult(true, updatedState.Run.RunId, updatedState.Run.Status, []);
        }
    }

    public SimulationRunCommandResult StepRun(string runId, SimulationRunStepRequestKind step, int? targetStep = null)
    {
        lock (sync)
        {
            if (!runs.TryGetValue(runId, out SimulationRunSession? session))
            {
                return Failure(runId, "run-not-found", $"Run '{runId}' was not found.", "runs", runId);
            }

            SimulationRunConsoleState current = RefreshSelectedState(session);
            int selectedStep = current.Run.SelectedStep;
            int requestedStep = step switch
            {
                SimulationRunStepRequestKind.Previous => selectedStep - 1,
                SimulationRunStepRequestKind.Next => selectedStep + 1,
                _ => targetStep ?? selectedStep
            };
            int totalSteps = Math.Max(1, current.Run.TotalSteps);
            if (requestedStep < 1 || requestedStep > totalSteps)
            {
                return Failure(runId, "invalid-run-step", $"Step {requestedStep.ToString(CultureInfo.InvariantCulture)} is outside the run range 1-{totalSteps.ToString(CultureInfo.InvariantCulture)}.", "runs.step", runId);
            }

            if (requestedStep > session.AvailableStep && current.Run.Status is SimulationRunLifecycleStatus.Stopped or SimulationRunLifecycleStatus.Failed)
            {
                return Failure(runId, "invalid-run-step-status", $"Cannot generate step {requestedStep.ToString(CultureInfo.InvariantCulture)} while the run is {DisplayStatus(current.Run.Status)}.", "runs.status", runId);
            }

            DateTimeOffset updated = timeProvider.UtcNow;
            for (int materializedStep = session.AvailableStep + 1; materializedStep <= requestedStep; materializedStep++)
            {
                SimulationRunLifecycleStatus materializedStatus = materializedStep >= totalSteps
                    ? SimulationRunLifecycleStatus.Completed
                    : session.Mode == SimulationRunMode.Manual
                        ? SimulationRunLifecycleStatus.Paused
                        : SimulationRunLifecycleStatus.Running;
                SimulationRunConsoleState materializedState = CreateConsoleState(
                    current.Project,
                    current.Setup,
                    current.Run.RunId,
                    materializedStatus,
                    current.Run.StartedAtUtc,
                    materializedStatus == SimulationRunLifecycleStatus.Completed ? updated : null,
                    materializedStep,
                    session.Mode,
                    materializedStep,
                    $"Step {materializedStep.ToString(CultureInfo.InvariantCulture)} materialized.",
                    session.SnapshotMetadata,
                    session.Interventions);
                session.Snapshots[materializedStep] = materializedState;
            }

            session.SelectedStep = requestedStep;
            SimulationRunConsoleState selected = RefreshSelectedState(session);
            return new SimulationRunCommandResult(true, selected.Run.RunId, selected.Run.Status, []);
        }
    }

    public SimulationRunInterventionCommandResult ApplyIntervention(string runId, SimulationRunInterventionRequest request)
    {
        lock (sync)
        {
            if (!runs.TryGetValue(runId, out SimulationRunSession? session))
            {
                return new SimulationRunInterventionCommandResult(
                    Failure(runId, "run-not-found", $"Run '{runId}' was not found.", "runs", runId),
                    null);
            }

            SimulationRunConsoleState current = RefreshSelectedState(session);
            if (current.Run.RunMode != SimulationRunMode.Manual)
            {
                return new SimulationRunInterventionCommandResult(
                    Failure(runId, "invalid-run-intervention-mode", "Runtime interventions are available only for manual stepping runs.", "runs.runMode", runId),
                    null);
            }

            if (current.Run.Status is SimulationRunLifecycleStatus.Completed or SimulationRunLifecycleStatus.CompletedWithWarnings or SimulationRunLifecycleStatus.Failed or SimulationRunLifecycleStatus.Stopped)
            {
                return new SimulationRunInterventionCommandResult(
                    Failure(runId, "invalid-run-intervention-status", $"Cannot apply an intervention while the run is {DisplayStatus(current.Run.Status)}.", "runs.status", runId),
                    null);
            }

            SimulationProjectValidationMessage[] validationMessages = ValidateInterventionRequest(current, request).ToArray();
            if (validationMessages.Any(message => message.Severity == SimulationValidationSeverity.Error))
            {
                return new SimulationRunInterventionCommandResult(
                    new SimulationRunCommandResult(false, runId, current.Run.Status, validationMessages),
                    null);
            }

            DateTimeOffset appliedAt = timeProvider.UtcNow;
            SimulationRunInterventionState intervention = CreateInterventionState(current, request, appliedAt);
            session.Interventions.Add(intervention);

            int selectedStep = current.Run.SelectedStep;
            foreach (int futureStep in session.Snapshots.Keys.Where(step => step > selectedStep).ToArray())
            {
                session.Snapshots.Remove(futureStep);
            }

            SimulationProjectState projectWithIntervention = AddInterventionDefinition(current.Project, intervention);
            var messages = new List<SimulationProjectValidationMessage>(validationMessages)
            {
                new(
                    "runtime-intervention-applied",
                    SimulationValidationSeverity.Info,
                    $"Intervention '{intervention.DisplayName}' will affect newly materialized steps from {intervention.EffectiveDate:yyyy-MM-dd}.",
                    "runs.interventions",
                    intervention.InterventionId)
            };

            SimulationRunConsoleState updatedState = CreateConsoleState(
                projectWithIntervention,
                current.Setup,
                current.Run.RunId,
                current.Run.Status,
                current.Run.StartedAtUtc,
                current.Run.FinishedAtUtc,
                selectedStep,
                session.Mode,
                selectedStep,
                $"Runtime intervention applied: {intervention.DisplayName}.",
                session.SnapshotMetadata,
                session.Interventions,
                messages);

            session.Snapshots[selectedStep] = updatedState;
            session.SelectedStep = selectedStep;
            SimulationRunConsoleState selected = RefreshSelectedState(session);
            return new SimulationRunInterventionCommandResult(
                new SimulationRunCommandResult(true, selected.Run.RunId, selected.Run.Status, messages),
                intervention);
        }
    }

    private void SeedProjectRuns(SimulationProjectState project)
    {
        SimulationRunSetupState? setup = project.RunSetups.FirstOrDefault();
        if (setup is null)
        {
            return;
        }

        var seeds = new[]
        {
            new SeedRun("RUN-2026-05-16-001", SimulationRunLifecycleStatus.Running, DateTimeOffset.Parse("2026-05-16T10:15:00Z", CultureInfo.InvariantCulture), null, Math.Max(1, setup.StepCount / 2), "Core simulation is processing May 2026."),
            new SeedRun("RUN-2026-05-15-003", SimulationRunLifecycleStatus.Completed, DateTimeOffset.Parse("2026-05-15T16:22:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-05-15T16:30:41Z", CultureInfo.InvariantCulture), setup.StepCount, "Baseline completed successfully."),
            new SeedRun("RUN-2026-05-15-002", SimulationRunLifecycleStatus.Failed, DateTimeOffset.Parse("2026-05-15T11:03:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-05-15T11:05:17Z", CultureInfo.InvariantCulture), Math.Max(1, setup.StepCount / 4), "Inventory negative in 3 BUs."),
            new SeedRun("RUN-2026-05-14-001", SimulationRunLifecycleStatus.CompletedWithWarnings, DateTimeOffset.Parse("2026-05-14T09:47:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-05-14T09:59:53Z", CultureInfo.InvariantCulture), setup.StepCount, "Current account deficit above threshold.")
        };

        foreach (SeedRun seed in seeds)
        {
            SimulationRunSession session = CreateSeedSession(project, setup, seed);
            runs[session.Selected.Run.RunId] = session;
        }
    }

    private SimulationRunSession CreateRunSession(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        string runId,
        DateTimeOffset started,
        SimulationRunMode mode,
        string message)
    {
        int totalSteps = Math.Max(1, setup.StepCount);
        int materializedSteps = mode == SimulationRunMode.Automatic ? totalSteps : 1;
        int selectedStep = mode == SimulationRunMode.Automatic ? totalSteps : 1;
        var session = new SimulationRunSession(mode) { SelectedStep = selectedStep };

        for (int step = 1; step <= materializedSteps; step++)
        {
            SimulationRunLifecycleStatus status = mode == SimulationRunMode.Automatic
                ? step == totalSteps ? SimulationRunLifecycleStatus.Completed : SimulationRunLifecycleStatus.Running
                : SimulationRunLifecycleStatus.Paused;
            DateTimeOffset? finishedAtUtc = status == SimulationRunLifecycleStatus.Completed
                ? started.AddMinutes(totalSteps)
                : null;
            SimulationRunConsoleState state = CreateConsoleState(
                project,
                setup,
                runId,
                status,
                started,
                finishedAtUtc,
                step,
                mode,
                materializedSteps,
                step == selectedStep ? message : $"Step {step.ToString(CultureInfo.InvariantCulture)} materialized.",
                session.SnapshotMetadata,
                session.Interventions);
            session.Snapshots[step] = state;
        }

        session.Selected = RefreshSelectedState(session);
        return session;
    }

    private SimulationRunSession CreateLiveAutomaticRunSession(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        string runId,
        DateTimeOffset started,
        string message)
    {
        int totalSteps = Math.Max(1, setup.StepCount);
        var session = new SimulationRunSession(SimulationRunMode.Automatic) { SelectedStep = 1 };
        SimulationRunLifecycleStatus status = totalSteps == 1
            ? SimulationRunLifecycleStatus.Completed
            : SimulationRunLifecycleStatus.Running;
        SimulationRunConsoleState state = CreateConsoleState(
            project,
            setup,
            runId,
            status,
            started,
            status == SimulationRunLifecycleStatus.Completed ? started.AddMinutes(1) : null,
            1,
            SimulationRunMode.Automatic,
            1,
            message,
            session.SnapshotMetadata,
            session.Interventions);
        session.Snapshots[1] = state;
        session.Selected = RefreshSelectedState(session);
        return session;
    }

    private async Task MaterializeAutomaticRunAsync(string runId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                bool completed = false;
                lock (sync)
                {
                    if (!runs.TryGetValue(runId, out SimulationRunSession? session))
                    {
                        return;
                    }

                    SimulationRunConsoleState current = RefreshSelectedState(session);
                    if (current.Run.Status is SimulationRunLifecycleStatus.Stopped or SimulationRunLifecycleStatus.Failed or SimulationRunLifecycleStatus.Completed or SimulationRunLifecycleStatus.CompletedWithWarnings)
                    {
                        return;
                    }

                    if (current.Run.Status == SimulationRunLifecycleStatus.Paused)
                    {
                        completed = false;
                    }
                    else
                    {
                        int totalSteps = Math.Max(1, current.Run.TotalSteps);
                        int nextStep = Math.Min(totalSteps, session.AvailableStep + 1);
                        SimulationRunLifecycleStatus nextStatus = nextStep >= totalSteps
                            ? SimulationRunLifecycleStatus.Completed
                            : SimulationRunLifecycleStatus.Running;
                        DateTimeOffset? finishedAtUtc = nextStatus == SimulationRunLifecycleStatus.Completed
                            ? timeProvider.UtcNow
                            : null;
                        SimulationRunConsoleState materializedState = CreateConsoleState(
                            current.Project,
                            current.Setup,
                            current.Run.RunId,
                            nextStatus,
                            current.Run.StartedAtUtc,
                            finishedAtUtc,
                            nextStep,
                            session.Mode,
                            nextStep,
                            nextStatus == SimulationRunLifecycleStatus.Completed
                                ? "Automatic live run completed."
                                : $"Automatic live step {nextStep.ToString(CultureInfo.InvariantCulture)} materialized.",
                            session.SnapshotMetadata,
                            session.Interventions);
                        session.Snapshots[nextStep] = materializedState;
                        session.SelectedStep = nextStep;
                        session.Selected = RefreshSelectedState(session);
                        completed = nextStatus == SimulationRunLifecycleStatus.Completed;
                    }
                }

                if (completed)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The app is shutting down or the caller abandoned the live run request.
        }
        catch (Exception ex)
        {
            MarkRunFailed(runId, ex.Message);
        }
    }

    private void MarkRunFailed(string runId, string message)
    {
        lock (sync)
        {
            if (!runs.TryGetValue(runId, out SimulationRunSession? session))
            {
                return;
            }

            SimulationRunConsoleState current = RefreshSelectedState(session);
            SimulationRunConsoleState failed = CreateConsoleState(
                current.Project,
                current.Setup,
                current.Run.RunId,
                SimulationRunLifecycleStatus.Failed,
                current.Run.StartedAtUtc,
                timeProvider.UtcNow,
                current.Run.CurrentStep,
                session.Mode,
                current.Run.AvailableStep,
                $"Automatic live run failed: {message}",
                session.SnapshotMetadata,
                session.Interventions,
                [new("automatic-live-run-failed", SimulationValidationSeverity.Error, message, "runs.live", runId)]);
            session.Snapshots[current.Run.CurrentStep] = failed;
            session.Selected = RefreshSelectedState(session);
        }
    }

    private SimulationRunSession CreateSeedSession(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        SeedRun seed)
    {
        int totalSteps = Math.Max(1, setup.StepCount);
        int selectedStep = Math.Clamp(seed.CurrentStep, 1, totalSteps);
        int materializedSteps = seed.Status is SimulationRunLifecycleStatus.Completed or SimulationRunLifecycleStatus.CompletedWithWarnings
            ? totalSteps
            : selectedStep;
        var session = new SimulationRunSession(SimulationRunMode.Automatic) { SelectedStep = selectedStep };

        for (int step = 1; step <= materializedSteps; step++)
        {
            SimulationRunLifecycleStatus status = step == selectedStep
                ? seed.Status
                : SimulationRunLifecycleStatus.Running;
            SimulationRunConsoleState state = CreateConsoleState(
                project,
                setup,
                seed.RunId,
                status,
                seed.StartedAtUtc,
                step == selectedStep ? seed.FinishedAtUtc : null,
                step,
                SimulationRunMode.Automatic,
                materializedSteps,
                step == selectedStep ? seed.Message : $"Seeded step {step.ToString(CultureInfo.InvariantCulture)}.",
                session.SnapshotMetadata,
                session.Interventions,
                seed.Status == SimulationRunLifecycleStatus.Failed && step == selectedStep
                    ? [new("seed-failure", SimulationValidationSeverity.Error, seed.Message)]
                    : []);
            session.Snapshots[step] = state;
        }

        session.Selected = RefreshSelectedState(session);
        return session;
    }

    private SimulationRunConsoleState RefreshSelectedState(SimulationRunSession session)
    {
        if (!session.Snapshots.TryGetValue(session.SelectedStep, out SimulationRunConsoleState? selected))
        {
            selected = session.Snapshots.Values.First();
            session.SelectedStep = selected.Run.CurrentStep;
        }

        int availableStep = session.AvailableStep;
        SimulationRunConsoleState refreshed = selected with
        {
            Run = selected.Run with
            {
                RunMode = session.Mode,
                SelectedStep = session.SelectedStep,
                AvailableStep = availableStep,
                SelectedStepHasData = session.Snapshots.ContainsKey(session.SelectedStep)
            },
            StepSnapshots = session.SnapshotMetadata
        };
        session.Selected = refreshed;
        return refreshed;
    }

    private SimulationRunConsoleState CreateConsoleState(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        string runId,
        SimulationRunLifecycleStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? finishedAtUtc,
        int currentStep,
        SimulationRunMode runMode,
        int availableStep,
        string eventMessage,
        IReadOnlyList<SimulationRunStepSnapshotState> stepSnapshots,
        IReadOnlyList<SimulationRunInterventionState> interventions,
        IReadOnlyList<SimulationProjectValidationMessage>? commandMessages = null)
    {
        int normalizedStep = Math.Clamp(currentStep, 1, Math.Max(1, setup.StepCount));
        int normalizedAvailableStep = Math.Clamp(availableStep, normalizedStep, Math.Max(1, setup.StepCount));
        DateOnly currentDate = ProjectDateAtStep(setup, normalizedStep);
        ScenarioRunMaterialization? scenarioMaterialization = TryBuildScenarioMaterialization(
            project,
            setup,
            runId,
            status,
            startedAtUtc,
            finishedAtUtc,
            normalizedStep,
            currentDate,
            interventions,
            commandMessages ?? []);
        IReadOnlyList<SimulationIssue> issues = scenarioMaterialization?.Result.Issues
            ?? BuildIssues(project, setup, status, currentDate, commandMessages ?? []);
        IReadOnlyList<CashFlowProjectionPoint> cashFlow = scenarioMaterialization?.Result.CashFlow
            ?? BuildCashFlow(project, currentDate);
        IReadOnlyDictionary<string, decimal> kpiValues = scenarioMaterialization?.KpiValues
            ?? BuildKpiValues(status, issues);
        string outputHash = scenarioMaterialization?.OutputHash
            ?? serializer.HashJson(new { project.Summary.ProjectId, setup.SetupId, runId, status, normalizedStep, issues = issues.Select(x => x.Code) });
        SimulationResultState result = scenarioMaterialization?.Result
            ?? new SimulationResultState(
                project.Summary.ProjectId,
                setup.ScenarioId,
                runId,
                finishedAtUtc ?? startedAtUtc.AddMinutes(Math.Max(1, normalizedStep)),
                setup.HorizonStartDate,
                setup.HorizonEndDate,
                Amount.FromInt64(184200),
                cashFlow.LastOrDefault()?.BalanceAfter ?? Amount.FromInt64(184200),
                cashFlow.Count == 0 ? Amount.FromInt64(184200) : Amount.FromDecimal(cashFlow.Min(x => x.BalanceAfter.Value)),
                issues.FirstOrDefault(issue => issue.Code.Contains("deficit", StringComparison.OrdinalIgnoreCase))?.Date,
                issues.FirstOrDefault(issue => issue.BusinessUnitId is not null)?.BusinessUnitId,
                Amount.Zero,
                Amount.FromInt64(65000),
                cashFlow,
                issues,
                kpiValues,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["result"] = outputHash,
                    ["cashflow"] = serializer.HashJson(cashFlow),
                    ["runLog"] = serializer.HashJson(new { runId, status, normalizedStep })
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["currentStep"] = normalizedStep.ToString(CultureInfo.InvariantCulture),
                    ["currentDate"] = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                });
        var runState = new ScenarioRunState(
            project.Summary.ProjectId,
            setup.ScenarioId,
            runId,
            setup.ScenarioName,
            null,
            null,
            startedAtUtc,
            setup.HorizonStartDate,
            setup.HorizonEndDate,
            ScenarioRunMode.DraftAndScenarioPosting.ToString(),
            setup.Seed,
            MergeAssumptions(setup.AssumptionOverrides, interventions),
            setup.IncludedTemplateIds,
            setup.IncludedBusinessUnits,
            DisplayStatus(status),
            $"simres:{EconomySimulatorIdentifiers.NormalizeSegment(project.Summary.ProjectId)}:{EconomySimulatorIdentifiers.NormalizeSegment(runId)}",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["currentStep"] = normalizedStep.ToString(CultureInfo.InvariantCulture)
            });
        IReadOnlyList<RuleExecutionEvidenceState> evidence = BuildRuleEvidence(project, setup, runId, status, startedAtUtc);
        IReadOnlyList<EconomyGraphData> graphs = scenarioMaterialization?.Graphs ?? BuildGraphs(project, result);
        SimulationRunObservationState observation = observationService.BuildObservation(project, setup, runState, result, graphs, evidence);
        int warningCount = issues.Count(issue => issue.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
        int errorCount = issues.Count(issue => issue.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        TimeSpan elapsed = finishedAtUtc is null
            ? TimeSpan.FromMinutes(Math.Max(1, normalizedStep))
            : finishedAtUtc.Value - startedAtUtc;
        TimeSpan? remaining = status is SimulationRunLifecycleStatus.Running or SimulationRunLifecycleStatus.Paused
            && normalizedStep < setup.StepCount
            ? TimeSpan.FromMinutes(Math.Max(1, setup.StepCount - normalizedStep))
            : null;
        var registry = new SimulationRunRegistryRecord(
            project.Summary.ProjectId,
            project.Summary.DisplayName,
            setup.SetupId,
            setup.ScenarioId,
            setup.ScenarioName,
            runId,
            status,
            startedAtUtc,
            finishedAtUtc,
            currentDate,
            normalizedStep,
            setup.StepCount,
            runMode,
            normalizedStep,
            normalizedAvailableStep,
            stepSnapshots.Any(snapshot => snapshot.Step == normalizedStep),
            outputHash,
            warningCount,
            errorCount,
            elapsed,
            remaining,
            "Austin Davis",
            finishedAtUtc ?? startedAtUtc.Add(elapsed),
            status == SimulationRunLifecycleStatus.Failed ? issues.FirstOrDefault(issue => issue.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))?.Message : null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["setupHash"] = importExportService.BuildDeterministicRunSetupHash(project, setup),
                ["eventMessage"] = eventMessage,
                ["runMode"] = runMode.ToString(),
                ["availableStep"] = normalizedAvailableStep.ToString(CultureInfo.InvariantCulture),
                ["selectedStepHasData"] = stepSnapshots.Any(snapshot => snapshot.Step == normalizedStep).ToString(CultureInfo.InvariantCulture)
            });

        return new SimulationRunConsoleState(
            project,
            setup,
            registry,
            observation,
            scenarioMaterialization is null ? BuildKpis(kpiValues, warningCount, errorCount) : BuildScenarioKpis(kpiValues, warningCount, errorCount),
            BuildEvents(project, setup, registry, eventMessage, issues, interventions),
            BuildTopIssues(issues),
            BuildPipeline(status),
            BuildResourceSnapshot(status, registry.LastUpdatedAtUtc),
            BuildAssumptions(project, setup, interventions),
            scenarioMaterialization is null ? BuildOutputSources(project, evidence) : BuildScenarioOutputSources(project, scenarioMaterialization, evidence),
            stepSnapshots);
    }

    private ScenarioRunMaterialization? TryBuildScenarioMaterialization(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        string runId,
        SimulationRunLifecycleStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? finishedAtUtc,
        int normalizedStep,
        DateOnly currentDate,
        IReadOnlyList<SimulationRunInterventionState> interventions,
        IReadOnlyList<SimulationProjectValidationMessage> commandMessages)
    {
        if (!IsScenarioProject(project)
            || scenarioCatalog is null
            || scenarioCompiler is null
            || projectionService is null
            || !project.Metadata.TryGetValue("scenarioCode", out string? scenarioCode)
            || string.IsNullOrWhiteSpace(scenarioCode))
        {
            return null;
        }

        ScenarioBusinessModelState scenario = ApplyScenarioInterventions(
            scenarioCatalog.GetScenario(scenarioCode),
            interventions);
        DateOnly stepEndDate = ProjectDateAtStepEnd(setup, normalizedStep);
        EconomyScenarioEventCompileResult compiled = scenarioCompiler.Compile(new EconomyScenarioEventCompileRequest(
            scenario,
            setup.ScenarioId,
            setup.HorizonStartDate,
            stepEndDate));
        IReadOnlyList<EconomicEventState> scheduledEvents = EconomyProjectionService.SortEvents(compiled.Events)
            .Where(x => x.EconomicDate >= setup.HorizonStartDate && x.EconomicDate <= stepEndDate)
            .Select(x =>
            {
                var metadata = new Dictionary<string, string>(x.Metadata, StringComparer.Ordinal)
                {
                    ["runId"] = runId
                };
                return x with
                {
                    Status = x.Status == EconomicEventStatus.Planned ? EconomicEventStatus.Simulated : x.Status,
                    Metadata = metadata
                };
            })
            .ToArray();
        EconomyProjection projection = projectionService.BuildProjection(new ProjectionRequest(project.Summary.ProjectId, scenario.OpeningCash, scheduledEvents));
        IReadOnlyList<SimulationIssue> issues = projection.Issues
            .Concat(commandMessages.Select(message => new SimulationIssue(
                $"issue:{setup.SetupId}:{message.Code}",
                message.Code,
                message.Severity.ToString(),
                currentDate,
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
        IReadOnlyDictionary<string, decimal> kpis = BuildScenarioKpiValues(scenario, scheduledEvents, projection, issues, interventions);
        string eventsHash = serializer.HashJson(scheduledEvents);
        string interventionHash = serializer.HashJson(interventions.Select(intervention => new
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
            project.Summary.ProjectId,
            setup.ScenarioId,
            runId,
            finishedAtUtc ?? startedAtUtc.AddMinutes(Math.Max(1, normalizedStep)),
            setup.HorizonStartDate,
            setup.HorizonEndDate,
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
                ["result"] = serializer.HashJson(new { runId, status, normalizedStep, eventsHash, projectionHash = serializer.HashJson(projection.CashFlow), interventionHash }),
                ["events"] = eventsHash,
                ["interventions"] = interventionHash,
                ["cashflow"] = serializer.HashJson(projection.CashFlow),
                ["schedule"] = compiled.ScheduleHash,
                ["runLog"] = serializer.HashJson(new { runId, status, normalizedStep, scenarioCode })
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scenarioCode"] = scenarioCode,
                ["currentStep"] = normalizedStep.ToString(CultureInfo.InvariantCulture),
                ["currentDate"] = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["stepEndDate"] = stepEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["eventCount"] = scheduledEvents.Count.ToString(CultureInfo.InvariantCulture),
                ["interventionCount"] = interventions.Count.ToString(CultureInfo.InvariantCulture)
            });

        IReadOnlyList<EconomyGraphData> graphs = BuildScenarioGraphs(project, result, scheduledEvents, projection.CashFlow, stepEndDate);
        return new ScenarioRunMaterialization(scenario, scheduledEvents, result, graphs, kpis, result.OutputHashes["result"], interventions);
    }

    private IReadOnlyList<SimulationProjectValidationMessage> ValidateInterventionRequest(
        SimulationRunConsoleState current,
        SimulationRunInterventionRequest request)
    {
        var messages = new List<SimulationProjectValidationMessage>();
        if (string.IsNullOrWhiteSpace(request.TargetId))
        {
            messages.Add(new("missing-intervention-target", SimulationValidationSeverity.Error, "Intervention target id is required.", "runs.interventions.targetId"));
        }

        DateOnly effectiveDate = ResolveInterventionEffectiveDate(current, request);
        if (effectiveDate < current.Run.CurrentDate)
        {
            messages.Add(new(
                "invalid-intervention-effective-date",
                SimulationValidationSeverity.Error,
                "Intervention effective date cannot be before the selected run date.",
                "runs.interventions.effectiveDate",
                request.TargetId));
        }

        switch (request.Kind)
        {
            case SimulationRunInterventionKind.DemandChange:
                ValidateDemandIntervention(current, request, messages);
                break;
            case SimulationRunInterventionKind.CapacityExpansion:
                ValidateCapacityIntervention(current, request, messages);
                break;
            case SimulationRunInterventionKind.FinancialInputOverride:
            case SimulationRunInterventionKind.Custom:
                messages.Add(new(
                    "runtime-intervention-generic-bo",
                    SimulationValidationSeverity.Info,
                    "Intervention will be stored as a runtime BO and surfaced in the run evidence.",
                    "runs.interventions",
                    request.TargetId));
                break;
        }

        return messages;
    }

    private void ValidateDemandIntervention(
        SimulationRunConsoleState current,
        SimulationRunInterventionRequest request,
        ICollection<SimulationProjectValidationMessage> messages)
    {
        if (request.Multiplier is null && request.QuantityDelta is null)
        {
            messages.Add(new(
                "missing-demand-change-value",
                SimulationValidationSeverity.Error,
                "Demand interventions require a multiplier or quantity delta.",
                "runs.interventions.demand",
                request.TargetId));
        }

        if (request.Multiplier is <= 0m)
        {
            messages.Add(new(
                "invalid-demand-multiplier",
                SimulationValidationSeverity.Error,
                "Demand multiplier must be greater than zero.",
                "runs.interventions.multiplier",
                request.TargetId));
        }

        if (!TryGetScenarioFromProject(current.Project, out ScenarioBusinessModelState? scenario))
        {
            messages.Add(new(
                "invalid-demand-intervention-project",
                SimulationValidationSeverity.Error,
                "Demand interventions require a scenario-backed project.",
                "runs.interventions",
                current.Project.Summary.ProjectId));
            return;
        }

        if (!scenario.DemandSchedules.Any(schedule => string.Equals(schedule.DemandScheduleId, request.TargetId, StringComparison.Ordinal)))
        {
            messages.Add(new(
                "unknown-demand-schedule",
                SimulationValidationSeverity.Error,
                $"Demand schedule '{request.TargetId}' was not found in scenario {scenario.ScenarioCode}.",
                "scenario.demandSchedules",
                request.TargetId));
        }
    }

    private static void ValidateCapacityIntervention(
        SimulationRunConsoleState current,
        SimulationRunInterventionRequest request,
        ICollection<SimulationProjectValidationMessage> messages)
    {
        if (request.Amount is null or <= 0m)
        {
            messages.Add(new(
                "invalid-capacity-expansion-amount",
                SimulationValidationSeverity.Error,
                "Capacity expansion requires a positive CapEx amount.",
                "runs.interventions.amount",
                request.TargetId));
        }

        if (request.DepreciationMonths is <= 0)
        {
            messages.Add(new(
                "invalid-capacity-depreciation",
                SimulationValidationSeverity.Error,
                "Depreciation months must be greater than zero.",
                "runs.interventions.depreciationMonths",
                request.TargetId));
        }

        if (ResolveBusinessUnit(current.Project, request.TargetId) is null)
        {
            messages.Add(new(
                "unknown-capacity-business-unit",
                SimulationValidationSeverity.Error,
                $"Business unit target '{request.TargetId}' was not found.",
                "project.businessUnits",
                request.TargetId));
        }
    }

    private SimulationRunInterventionState CreateInterventionState(
        SimulationRunConsoleState current,
        SimulationRunInterventionRequest request,
        DateTimeOffset appliedAt)
    {
        DateOnly effectiveDate = ResolveInterventionEffectiveDate(current, request);
        string targetId = request.Kind == SimulationRunInterventionKind.CapacityExpansion
            ? ResolveBusinessUnit(current.Project, request.TargetId)?.BusinessUnitId.Value ?? request.TargetId.Trim()
            : request.TargetId.Trim();
        string segment = EconomySimulatorIdentifiers.NormalizeSegment(
            $"{request.Kind}-{targetId}-{effectiveDate:yyyyMMdd}-{appliedAt.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture)}");
        string interventionId = $"runint:{segment}";
        string definitionId = $"def:runtime-intervention:{segment}";
        string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? request.Kind switch
            {
                SimulationRunInterventionKind.DemandChange => "Demand change",
                SimulationRunInterventionKind.CapacityExpansion => "Capacity expansion",
                SimulationRunInterventionKind.FinancialInputOverride => "Financial input override",
                _ => "Runtime intervention"
            }
            : request.DisplayName.Trim();
        string reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Manual management intervention"
            : request.Reason.Trim();
        var metadata = new Dictionary<string, string>(request.Metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal)
        {
            ["runId"] = current.Run.RunId,
            ["projectId"] = current.Project.Summary.ProjectId,
            ["setupId"] = current.Setup.SetupId,
            ["selectedStep"] = current.Run.SelectedStep.ToString(CultureInfo.InvariantCulture),
            ["selectedDate"] = current.Run.CurrentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["controlProcess"] = request.Kind switch
            {
                SimulationRunInterventionKind.DemandChange => "forecast-demand-change",
                SimulationRunInterventionKind.CapacityExpansion => "capex-capacity-request",
                SimulationRunInterventionKind.FinancialInputOverride => "financial-input-override",
                _ => "runtime-bo-change"
            },
            ["approvalStatus"] = "management-review",
            ["source"] = "manual-run-console"
        };

        return new SimulationRunInterventionState(
            interventionId,
            request.Kind,
            SimulationRunInterventionStatus.Applied,
            targetId,
            displayName,
            effectiveDate,
            request.QuantityDelta,
            request.Multiplier,
            request.Amount,
            request.DepreciationMonths,
            reason,
            current.Run.SelectedStep,
            appliedAt,
            appliedAt,
            definitionId,
            metadata);
    }

    private DateOnly ResolveInterventionEffectiveDate(SimulationRunConsoleState current, SimulationRunInterventionRequest request)
    {
        if (request.EffectiveDate is { } requested)
        {
            return requested;
        }

        int nextStep = Math.Min(current.Run.TotalSteps, current.Run.SelectedStep + 1);
        return ProjectDateAtStep(current.Setup, nextStep);
    }

    private SimulationProjectState AddInterventionDefinition(
        SimulationProjectState project,
        SimulationRunInterventionState intervention)
    {
        const string folderId = "folder:runtime-interventions";
        IReadOnlyList<SimulationProjectFolderState> folders = project.Folders.Any(folder => string.Equals(folder.FolderId, folderId, StringComparison.Ordinal))
            ? project.Folders
            :
            [
                ..project.Folders,
                new(folderId, null, "Runtime interventions", SimulationProjectFolderKind.ImportedObjects, 70, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "manual-run-console"
                })
            ];
        BusinessUnitState? owner = ResolveBusinessUnit(project, intervention.TargetId)
            ?? project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.RevenueCenter)
            ?? project.BusinessUnits.FirstOrDefault();
        var definition = new SimulationBusinessObjectDefinitionState(
            intervention.BusinessObjectDefinitionId,
            intervention.DisplayName,
            SimulationBusinessObjectDefinitionKind.Custom,
            EconomySimulatorObjectTypes.RuntimeIntervention,
            EconomySimulatorSchema.Version,
            folderId,
            owner?.BusinessUnitId,
            owner?.PrimaryAddress,
            serializer.Serialize(intervention),
            null,
            SimulationDefinitionMaterializationStatus.Validated,
            [],
            [],
            new Dictionary<string, string>(intervention.Metadata, StringComparer.Ordinal)
            {
                ["interventionId"] = intervention.InterventionId,
                ["effectiveDate"] = intervention.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });

        return project with
        {
            Folders = folders,
            BusinessObjectDefinitions =
            [
                ..project.BusinessObjectDefinitions.Where(def => !string.Equals(def.DefinitionId, definition.DefinitionId, StringComparison.Ordinal)),
                definition
            ]
        };
    }

    private ScenarioBusinessModelState ApplyScenarioInterventions(
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

    private bool TryGetScenarioFromProject(SimulationProjectState project, out ScenarioBusinessModelState scenario)
    {
        if (scenarioCatalog is not null
            && project.Metadata.TryGetValue("scenarioCode", out string? scenarioCode)
            && !string.IsNullOrWhiteSpace(scenarioCode))
        {
            try
            {
                scenario = scenarioCatalog.GetScenario(scenarioCode);
                return true;
            }
            catch (InvalidOperationException)
            {
            }
        }

        scenario = default!;
        return false;
    }

    private static BusinessUnitState? ResolveBusinessUnit(SimulationProjectState project, string? targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return null;
        }

        return project.BusinessUnits.FirstOrDefault(unit => string.Equals(unit.BusinessUnitId.Value, targetId, StringComparison.Ordinal))
            ?? project.BusinessUnits.FirstOrDefault(unit => string.Equals(unit.Path, targetId, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, string> MergeAssumptions(
        IReadOnlyDictionary<string, string> assumptions,
        IReadOnlyList<SimulationRunInterventionState> interventions)
    {
        var merged = new Dictionary<string, string>(assumptions, StringComparer.Ordinal);
        if (interventions.Count > 0)
        {
            merged["runtimeInterventionCount"] = interventions.Count.ToString(CultureInfo.InvariantCulture);
            merged["runtimeInterventionIds"] = string.Join(';', interventions.Select(intervention => intervention.InterventionId));
        }

        return merged;
    }

    private static SimulationRunCommandResult Failure(string? runId, string code, string message, string path, string? relatedId)
        => new(false, runId, null, [new SimulationProjectValidationMessage(code, SimulationValidationSeverity.Error, message, path, relatedId)]);

    private static DateOnly ProjectDateAtStep(SimulationRunSetupState setup, int currentStep)
        => setup.StepPeriod switch
        {
            SimulationRunStepPeriod.Day => setup.HorizonStartDate.AddDays(currentStep - 1),
            SimulationRunStepPeriod.Week => setup.HorizonStartDate.AddDays((currentStep - 1) * 7),
            SimulationRunStepPeriod.Quarter => setup.HorizonStartDate.AddMonths((currentStep - 1) * 3),
            SimulationRunStepPeriod.HalfYear => setup.HorizonStartDate.AddMonths((currentStep - 1) * 6),
            SimulationRunStepPeriod.Year => setup.HorizonStartDate.AddYears(currentStep - 1),
            _ => setup.HorizonStartDate.AddMonths(currentStep - 1)
        };

    private static DateOnly ProjectDateAtStepEnd(SimulationRunSetupState setup, int currentStep)
    {
        DateOnly periodStart = ProjectDateAtStep(setup, currentStep);
        DateOnly periodEnd = setup.StepPeriod switch
        {
            SimulationRunStepPeriod.Day => periodStart,
            SimulationRunStepPeriod.Week => periodStart.AddDays(6),
            SimulationRunStepPeriod.Quarter => periodStart.AddMonths(3).AddDays(-1),
            SimulationRunStepPeriod.HalfYear => periodStart.AddMonths(6).AddDays(-1),
            SimulationRunStepPeriod.Year => periodStart.AddYears(1).AddDays(-1),
            _ => periodStart.AddMonths(1).AddDays(-1)
        };

        return periodEnd > setup.HorizonEndDate ? setup.HorizonEndDate : periodEnd;
    }

    private static bool IsScenarioProject(SimulationProjectState project)
        => project.Metadata.TryGetValue("scenarioProject", out string? value)
            && bool.TryParse(value, out bool parsed)
            && parsed;

    private static IReadOnlyList<CashFlowProjectionPoint> BuildCashFlow(SimulationProjectState project, DateOnly currentDate)
    {
        BusinessUnitId treasury = project.Model.TreasuryBusinessUnitId;
        BusinessUnitId revenue = project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.RevenueCenter)?.BusinessUnitId ?? treasury;
        BusinessUnitId cost = project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.CostCenter)?.BusinessUnitId ?? treasury;
        return
        [
            Point(project.Model.PlanningStartDate, treasury, "opening", 0, 0, 184200),
            Point(currentDate.AddDays(-20), revenue, "cash-receipts", 42000, 0, 226200),
            Point(currentDate.AddDays(-8), cost, "payroll-expense", 0, 36000, 190200),
            Point(currentDate, revenue, "cash-receipts", 58000, 0, 242200)
        ];
    }

    private static IReadOnlyDictionary<string, decimal> BuildKpiValues(SimulationRunLifecycleStatus status, IReadOnlyList<SimulationIssue> issues)
        => new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["realGdp"] = status == SimulationRunLifecycleStatus.Failed ? 22410.3m : 22842.6m,
            ["cpi"] = 268.7m,
            ["unemployment"] = issues.Any(issue => issue.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)) ? 4.9m : 4.2m,
            ["policyRate"] = 5.33m,
            ["budgetBalance"] = -1087.4m,
            ["currentAccount"] = -226.5m,
            ["deficitCount"] = issues.Count(issue => issue.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)),
            ["warningCount"] = issues.Count(issue => issue.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
        };

    private static IReadOnlyList<SimulationIssue> BuildIssues(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        SimulationRunLifecycleStatus status,
        DateOnly currentDate,
        IReadOnlyList<SimulationProjectValidationMessage> commandMessages)
    {
        BusinessUnitId operations = project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.CostCenter)?.BusinessUnitId ?? project.Model.TreasuryBusinessUnitId;
        BusinessUnitId external = project.Model.ExternalClearingBusinessUnitId;
        var issues = new List<SimulationIssue>();
        if (status == SimulationRunLifecycleStatus.Failed)
        {
            issues.Add(new SimulationIssue(
                $"issue:{setup.SetupId}:inventory-negative",
                "BO-INV-01",
                "Error",
                currentDate,
                operations,
                new BusinessObjectId("bo:project-alpha:inventory"),
                null,
                Amount.FromInt64(18000),
                "Inventory negative in 3 BUs.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["firstSeen"] = currentDate.ToString("MMM yyyy", CultureInfo.InvariantCulture)
                }));
        }

        if (status is SimulationRunLifecycleStatus.Running or SimulationRunLifecycleStatus.CompletedWithWarnings or SimulationRunLifecycleStatus.Failed)
        {
            issues.Add(new SimulationIssue(
                $"issue:{setup.SetupId}:current-account",
                "BO-FX-02",
                "Warning",
                currentDate.AddMonths(-3),
                external,
                new BusinessObjectId("bo:project-alpha:external-sector"),
                null,
                Amount.FromInt64(226500),
                "Current account deficit above threshold.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["firstSeen"] = currentDate.AddMonths(-3).ToString("MMM yyyy", CultureInfo.InvariantCulture)
                }));
        }

        foreach (SimulationProjectValidationMessage message in commandMessages)
        {
            issues.Add(new SimulationIssue(
                $"issue:{setup.SetupId}:{message.Code}",
                message.Code,
                message.Severity.ToString(),
                currentDate,
                null,
                null,
                null,
                null,
                message.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)));
        }

        return issues;
    }

    private static IReadOnlyList<RuleExecutionEvidenceState> BuildRuleEvidence(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        string runId,
        SimulationRunLifecycleStatus status,
        DateTimeOffset observedAtUtc)
    {
        return project.BusinessObjectDefinitions
            .Where(definition => definition.RuleDefinition is not null)
            .OrderBy(definition => definition.DefinitionId, StringComparer.Ordinal)
            .Select((definition, index) => new RuleExecutionEvidenceState(
                project.Summary.ProjectId,
                setup.ScenarioId.Value,
                definition.RuleDefinition!.RuleId,
                index % 2 == 0 ? AccountRuleActionKind.Payment.ToString() : AccountRuleActionKind.UpdateBusinessObject.ToString(),
                status == SimulationRunLifecycleStatus.Failed && index == 0
                    ? AccountRuleExecutionStatus.Failed.ToString()
                    : AccountRuleExecutionStatus.Executed.ToString(),
                new BusinessObjectId(definition.DefinitionId),
                null,
                status == SimulationRunLifecycleStatus.Failed && index == 0 ? null : new TxId($"tx:{EconomySimulatorIdentifiers.NormalizeSegment(runId)}:{index + 1:000}"),
                observedAtUtc.AddSeconds(index + 1),
                status == SimulationRunLifecycleStatus.Failed && index == 0
                    ? "Rule execution halted after inventory precondition failed."
                    : $"Rule evidence linked to {definition.DisplayName}.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["definitionId"] = definition.DefinitionId,
                    ["businessUnitId"] = definition.OwnerBusinessUnitId?.Value ?? project.Model.TreasuryBusinessUnitId.Value
                }))
            .ToArray();
    }

    private static IReadOnlyList<EconomyGraphData> BuildGraphs(SimulationProjectState project, SimulationResultState result)
    {
        return
        [
            Graph(project, result, ScenarioGraphMetric.CashFlow),
            Graph(project, result, ScenarioGraphMetric.Revenue),
            Graph(project, result, ScenarioGraphMetric.Expense)
        ];
    }

    private static EconomyGraphData Graph(SimulationProjectState project, SimulationResultState result, ScenarioGraphMetric metric)
    {
        IReadOnlyList<EconomyGraphPoint> points = result.CashFlow
            .Select(point => new EconomyGraphPoint(
                point.Date,
                point.BusinessUnitId.Value,
                null,
                point.CategoryCode,
                metric == ScenarioGraphMetric.Expense ? FinancialStatementSection.Opex.ToString() : FinancialStatementSection.CashReceipts.ToString(),
                point.PlannedInflow.Value,
                point.PlannedOutflow.Value,
                point.CommittedInflow.Value,
                point.CommittedOutflow.Value,
                point.ActualInflow.Value,
                point.ActualOutflow.Value,
                point.SimulatedInflow.Value,
                point.SimulatedOutflow.Value,
                metric == ScenarioGraphMetric.Revenue ? Math.Max(0m, point.PlannedInflow.Value + point.CommittedInflow.Value + point.SimulatedInflow.Value) : 0m,
                metric == ScenarioGraphMetric.Expense ? Math.Max(0m, point.PlannedOutflow.Value + point.CommittedOutflow.Value + point.SimulatedOutflow.Value) : 0m,
                point.BalanceAfter.Value))
            .ToArray();
        var filter = new EconomyGraphQueryFilter(project.Summary.ProjectId, result.ScenarioId.Value, result.HorizonStartDate, result.HorizonEndDate, metric, null, [], [], 500);
        IReadOnlyList<EconomyGraphSeries> series = points
            .GroupBy(point => point.CategoryCode, StringComparer.Ordinal)
            .Select(group => new EconomyGraphSeries(group.Key, group.Key, group.ToArray()))
            .ToArray();
        IReadOnlyList<EconomyGraphDrilldownRow> rows = points
            .Select((point, index) => new EconomyGraphDrilldownRow(
                point.PeriodStart,
                $"evt:{project.Summary.ProjectId}:{metric.ToString().ToLowerInvariant()}:{index + 1:000}",
                point.BusinessUnitId,
                point.AccountAddress,
                point.CategoryCode,
                point.FinancialStatementSection,
                "Simulated",
                GraphAmount(metric, point),
                point.CategoryCode,
                point.CategoryCode.Replace('-', ' ')))
            .ToArray();
        IReadOnlyDictionary<string, decimal> totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["inflow"] = points.Sum(point => point.PlannedInflow + point.CommittedInflow + point.ActualInflow + point.SimulatedInflow),
            ["outflow"] = points.Sum(point => point.PlannedOutflow + point.CommittedOutflow + point.ActualOutflow + point.SimulatedOutflow),
            ["netCashFlow"] = points.Sum(point => point.PlannedInflow + point.CommittedInflow + point.ActualInflow + point.SimulatedInflow - point.PlannedOutflow - point.CommittedOutflow - point.ActualOutflow - point.SimulatedOutflow),
            ["rowCount"] = points.Count
        };
        return new EconomyGraphData(filter, series, rows, totals);
    }

    private static decimal GraphAmount(ScenarioGraphMetric metric, EconomyGraphPoint point)
        => metric switch
        {
            ScenarioGraphMetric.Revenue => point.PlannedInflow + point.CommittedInflow + point.SimulatedInflow + point.NonCashIncome,
            ScenarioGraphMetric.Expense => point.PlannedOutflow + point.CommittedOutflow + point.SimulatedOutflow + point.NonCashExpense,
            _ => point.PlannedInflow + point.CommittedInflow + point.SimulatedInflow - point.PlannedOutflow - point.CommittedOutflow - point.SimulatedOutflow
        };

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
            .Where(x => EconomyProjectionService.IsOutflow(x))
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

    private static IReadOnlyList<EconomyGraphData> BuildScenarioGraphs(
        SimulationProjectState project,
        SimulationResultState result,
        IReadOnlyList<EconomicEventState> events,
        IReadOnlyList<CashFlowProjectionPoint> cashFlow,
        DateOnly periodEnd)
        =>
        [
            ScenarioGraph(project, result, events, cashFlow, periodEnd, ScenarioGraphMetric.CashFlow),
            ScenarioGraph(project, result, events, cashFlow, periodEnd, ScenarioGraphMetric.Revenue),
            ScenarioGraph(project, result, events, cashFlow, periodEnd, ScenarioGraphMetric.Expense)
        ];

    private static EconomyGraphData ScenarioGraph(
        SimulationProjectState project,
        SimulationResultState result,
        IReadOnlyList<EconomicEventState> events,
        IReadOnlyList<CashFlowProjectionPoint> cashFlow,
        DateOnly periodEnd,
        ScenarioGraphMetric metric)
    {
        IReadOnlyList<EconomyGraphPoint> points = events
            .Select((eventState, index) => ToScenarioGraphPoint(eventState, index < cashFlow.Count ? cashFlow[index] : null))
            .Where(point => metric switch
            {
                ScenarioGraphMetric.Revenue => point.FinancialStatementSection == FinancialStatementSection.Revenue.ToString() || point.FinancialStatementSection == FinancialStatementSection.CashReceipts.ToString(),
                ScenarioGraphMetric.Expense => point.FinancialStatementSection == FinancialStatementSection.Cogs.ToString()
                    || point.FinancialStatementSection == FinancialStatementSection.Opex.ToString()
                    || point.FinancialStatementSection == FinancialStatementSection.Payroll.ToString()
                    || point.FinancialStatementSection == FinancialStatementSection.Capex.ToString()
                    || point.FinancialStatementSection == FinancialStatementSection.Depreciation.ToString(),
                _ => true
            })
            .OrderBy(point => point.PeriodStart)
            .ThenBy(point => point.BusinessUnitId, StringComparer.Ordinal)
            .ThenBy(point => point.CategoryCode, StringComparer.Ordinal)
            .ToArray();
        var filter = new EconomyGraphQueryFilter(project.Summary.ProjectId, result.ScenarioId.Value, result.HorizonStartDate, periodEnd, metric, null, [], [], 5000);
        IReadOnlyList<EconomyGraphSeries> series = points
            .GroupBy(point => point.CategoryCode, StringComparer.Ordinal)
            .Select(group => new EconomyGraphSeries(group.Key, group.Key, group.ToArray()))
            .ToArray();
        IReadOnlyList<EconomyGraphDrilldownRow> rows = events
            .Where(eventState => EventIncludedInMetric(eventState, metric))
            .OrderBy(eventState => eventState.EconomicDate)
            .ThenBy(eventState => eventState.EventId, StringComparer.Ordinal)
            .Take(250)
            .Select(eventState =>
            {
                EconomyGraphPoint point = ToScenarioGraphPoint(eventState, null);
                return new EconomyGraphDrilldownRow(
                    point.PeriodStart,
                    eventState.EventId,
                    point.BusinessUnitId,
                    point.AccountAddress,
                    point.CategoryCode,
                    point.FinancialStatementSection,
                    eventState.Status.ToString(),
                    GraphAmount(metric, point),
                    eventState.SourceObjectId ?? string.Empty,
                    eventState.Metadata.TryGetValue(EconomyScenarioMetadataKeys.Description, out string? description) ? description : eventState.CategoryCode);
            })
            .ToArray();
        IReadOnlyDictionary<string, decimal> totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["inflow"] = points.Sum(x => x.PlannedInflow + x.CommittedInflow + x.ActualInflow + x.SimulatedInflow),
            ["outflow"] = points.Sum(x => x.PlannedOutflow + x.CommittedOutflow + x.ActualOutflow + x.SimulatedOutflow),
            ["nonCashIncome"] = points.Sum(x => x.NonCashIncome),
            ["nonCashExpense"] = points.Sum(x => x.NonCashExpense),
            ["netCashFlow"] = points.Sum(x => x.PlannedInflow + x.CommittedInflow + x.ActualInflow + x.SimulatedInflow - x.PlannedOutflow - x.CommittedOutflow - x.ActualOutflow - x.SimulatedOutflow),
            ["rowCount"] = points.Count
        };

        return new EconomyGraphData(filter, series, rows, totals);
    }

    private static EconomyGraphPoint ToScenarioGraphPoint(EconomicEventState eventState, CashFlowProjectionPoint? cashFlowPoint)
    {
        bool outflow = EconomyProjectionService.IsOutflow(eventState);
        bool cashImpact = eventState.ImpactKind is EconomicImpactKind.CashOnly or EconomicImpactKind.CashAndAccrual;
        bool pnlWithoutCashMovement = eventState.ImpactKind is EconomicImpactKind.AccrualOnly or EconomicImpactKind.NonCashPnl;
        decimal plannedInflow = 0m;
        decimal plannedOutflow = 0m;
        decimal committedInflow = 0m;
        decimal committedOutflow = 0m;
        decimal actualInflow = 0m;
        decimal actualOutflow = 0m;
        decimal simulatedInflow = 0m;
        decimal simulatedOutflow = 0m;

        if (cashImpact)
        {
            switch (eventState.Status)
            {
                case EconomicEventStatus.Actual:
                case EconomicEventStatus.Settled:
                case EconomicEventStatus.Reconciled:
                    (actualInflow, actualOutflow) = Split(eventState.Amount.Value, outflow);
                    break;
                case EconomicEventStatus.Committed:
                case EconomicEventStatus.Invoiced:
                case EconomicEventStatus.Due:
                case EconomicEventStatus.PartiallySettled:
                    (committedInflow, committedOutflow) = Split(eventState.Amount.Value, outflow);
                    break;
                case EconomicEventStatus.Simulated:
                    (simulatedInflow, simulatedOutflow) = Split(eventState.Amount.Value, outflow);
                    break;
                default:
                    (plannedInflow, plannedOutflow) = Split(eventState.Amount.Value, outflow);
                    break;
            }
        }

        string section = eventState.Metadata.TryGetValue(EconomyScenarioMetadataKeys.FinancialStatementSection, out string? value)
            ? value
            : FinancialStatementSection.Opex.ToString();
        return new EconomyGraphPoint(
            EconomySimulatorPeriods.MonthStart(eventState.EconomicDate),
            eventState.SourceBusinessUnitId.Value,
            eventState.SourceAddress?.Value ?? (eventState.Metadata.TryGetValue(EconomyScenarioMetadataKeys.AccountAddress, out string? address) ? address : null),
            eventState.CategoryCode,
            section,
            plannedInflow,
            plannedOutflow,
            committedInflow,
            committedOutflow,
            actualInflow,
            actualOutflow,
            simulatedInflow,
            simulatedOutflow,
            pnlWithoutCashMovement && !outflow ? eventState.Amount.Value : 0m,
            pnlWithoutCashMovement && outflow ? eventState.Amount.Value : 0m,
            cashFlowPoint?.BalanceAfter.Value ?? 0m);
    }

    private static bool EventIncludedInMetric(EconomicEventState eventState, ScenarioGraphMetric metric)
    {
        string section = eventState.Metadata.TryGetValue(EconomyScenarioMetadataKeys.FinancialStatementSection, out string? value)
            ? value
            : FinancialStatementSection.Opex.ToString();
        return metric switch
        {
            ScenarioGraphMetric.Revenue => section == FinancialStatementSection.Revenue.ToString() || section == FinancialStatementSection.CashReceipts.ToString(),
            ScenarioGraphMetric.Expense => section == FinancialStatementSection.Cogs.ToString()
                || section == FinancialStatementSection.Opex.ToString()
                || section == FinancialStatementSection.Payroll.ToString()
                || section == FinancialStatementSection.Capex.ToString()
                || section == FinancialStatementSection.Depreciation.ToString(),
            _ => true
        };
    }

    private static (decimal Inflow, decimal Outflow) Split(decimal amount, bool outflow)
        => outflow ? (0m, amount) : (amount, 0m);

    private static IReadOnlyList<SimulationRunKpiSnapshot> BuildScenarioKpis(IReadOnlyDictionary<string, decimal> values, int warningCount, int errorCount)
    {
        string tone = errorCount > 0 ? "warning" : "success";
        return
        [
            Kpi("cashBalance", "Cash balance", values["cashBalance"].ToString("#,##0.##", CultureInfo.InvariantCulture), "cash", tone, $"Headroom {values["cashHeadroom"].ToString("#,##0.##", CultureInfo.InvariantCulture)}", "Scenario cash projection"),
            Kpi("minimumCash", "Minimum cash observed", values["minimumCash"].ToString("#,##0.##", CultureInfo.InvariantCulture), "cash", values["minimumCash"] < values["minimumCashFloor"] ? "warning" : "success", $"Floor {values["minimumCashFloor"].ToString("#,##0.##", CultureInfo.InvariantCulture)}", "Scenario liquidity floor"),
            Kpi("revenue", "Revenue accrued", values["revenue"].ToString("#,##0.##", CultureInfo.InvariantCulture), "P&L", "success", $"Cash receipts {values["cashReceipts"].ToString("#,##0.##", CultureInfo.InvariantCulture)}", "Compiled customer demand"),
            Kpi("expenses", "P&L expenses", values["expenses"].ToString("#,##0.##", CultureInfo.InvariantCulture), "P&L", "info", $"Profit {values["profit"].ToString("#,##0.##", CultureInfo.InvariantCulture)}", "Compiled cost plans"),
            Kpi("netCashFlow", "Net cash flow", values["netCashFlow"].ToString("#,##0.##", CultureInfo.InvariantCulture), "cash", values["netCashFlow"] < 0m ? "warning" : "success", $"In {values["cashInflow"].ToString("#,##0.##", CultureInfo.InvariantCulture)} / out {values["cashOutflow"].ToString("#,##0.##", CultureInfo.InvariantCulture)}", "Compiled event cash timing"),
            Kpi("serviceHours", "Service hours delivered", values["serviceHours"].ToString("#,##0.##", CultureInfo.InvariantCulture), "hours", values["serviceHours"] > 0m ? "success" : "info", "Workdone token flow", "Work orders"),
            Kpi("productFlowUnits", "Product-unit flow", values["productFlowUnits"].ToString("#,##0.##", CultureInfo.InvariantCulture), "units", values["productFlowUnits"] > 0m ? "success" : "info", "Production token flow", "Product flow plans"),
            Kpi("runtimeInterventions", "Runtime interventions", values["interventionCount"].ToString("#,##0.##", CultureInfo.InvariantCulture), "BOs", values["interventionCount"] > 0m ? "info" : "muted", "Manual changes", "Intervention BOs")
        ];
    }

    private static IReadOnlyList<SimulationRunKpiSnapshot> BuildKpis(IReadOnlyDictionary<string, decimal> values, int warningCount, int errorCount)
        =>
        [
            Kpi("realGdp", "Real GDP (SAAR)", values["realGdp"].ToString("#,##0.0", CultureInfo.InvariantCulture), "USD B", errorCount > 0 ? "warning" : "success", "+1.24% vs Apr 2026", "FiscalPolicy + Revenue BOs"),
            Kpi("cpi", "CPI (All Items)", values["cpi"].ToString("#,##0.0", CultureInfo.InvariantCulture), "Index (2020=100)", "success", "+0.31% vs Apr 2026", "MonetaryPolicy BO"),
            Kpi("unemployment", "Unemployment Rate", $"{values["unemployment"]:0.0}%", "Labor force", errorCount > 0 ? "warning" : "success", "-0.1 pp vs Apr 2026", "LaborMarket BU"),
            Kpi("policyRate", "Federal Funds Rate", $"{values["policyRate"]:0.00}%", "Effective", "info", "0.00 pp vs Apr 2026", "Central bank rule"),
            Kpi("budgetBalance", "Budget Balance", values["budgetBalance"].ToString("#,##0.0", CultureInfo.InvariantCulture), "USD B", warningCount > 0 ? "warning" : "success", "-8.76% vs Apr 2026", "FiscalPolicy BO"),
            Kpi("currentAccount", "Current Account", values["currentAccount"].ToString("#,##0.0", CultureInfo.InvariantCulture), "USD B", warningCount > 0 ? "warning" : "success", "-2.41% vs Apr 2026", "ExternalSector BO")
        ];

    private static SimulationRunKpiSnapshot Kpi(string code, string displayName, string value, string unit, string tone, string delta, string source)
        => new(code, displayName, value, unit, tone, delta, source, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceKind"] = "BO/BU mapped sample provider"
        });

    private static IReadOnlyList<SimulationRunEventState> BuildEvents(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        SimulationRunRegistryRecord run,
        string message,
        IReadOnlyList<SimulationIssue> issues,
        IReadOnlyList<SimulationRunInterventionState> interventions)
    {
        var events = new List<SimulationRunEventState>
        {
            Event(run, 0, SimulationValidationSeverity.Info, "Simulation", message),
            Event(run, -1, SimulationValidationSeverity.Info, "FiscalPolicy", $"Automatic stabilizer applied ({run.CurrentDate:MMM yyyy})."),
            Event(run, -2, SimulationValidationSeverity.Info, "MonetaryPolicy", "Policy rate unchanged."),
            Event(run, -3, SimulationValidationSeverity.Info, "LaborMarket", "Participation rate updated.")
        };

        events.AddRange(issues.Select((issue, index) => new SimulationRunEventState(
            $"event:{run.RunId}:issue:{index + 1:000}",
            issue.Date,
            run.LastUpdatedAtUtc.AddSeconds(index + 8),
            Severity(issue.Severity),
            issue.Code,
            issue.Message,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["issueId"] = issue.IssueId,
                ["scenarioId"] = setup.ScenarioId.Value,
                ["projectId"] = project.Summary.ProjectId
            })));
        events.AddRange(interventions.Select((intervention, index) => new SimulationRunEventState(
            $"event:{run.RunId}:intervention:{index + 1:000}",
            intervention.EffectiveDate,
            intervention.AppliedAtUtc,
            SimulationValidationSeverity.Info,
            "Runtime intervention",
            $"{intervention.DisplayName} ({intervention.Kind}).",
            new Dictionary<string, string>(intervention.Metadata, StringComparer.Ordinal)
            {
                ["interventionId"] = intervention.InterventionId,
                ["businessObjectDefinitionId"] = intervention.BusinessObjectDefinitionId
            })));
        return events
            .OrderByDescending(x => x.ObservedAtUtc)
            .ToArray();
    }

    private static SimulationRunEventState Event(SimulationRunRegistryRecord run, int monthOffset, SimulationValidationSeverity level, string source, string message)
        => new(
            $"event:{run.RunId}:{EconomySimulatorIdentifiers.NormalizeSegment(source)}:{Math.Abs(monthOffset):000}",
            run.CurrentDate.AddMonths(monthOffset),
            run.LastUpdatedAtUtc.AddSeconds(Math.Abs(monthOffset)),
            level,
            source,
            message,
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static IReadOnlyList<SimulationRunTopIssueState> BuildTopIssues(IReadOnlyList<SimulationIssue> issues)
        => issues
            .GroupBy(issue => $"{issue.Code}|{issue.Severity}|{issue.Message}", StringComparer.Ordinal)
            .Select(group => new SimulationRunTopIssueState(
                group.First().IssueId,
                Severity(group.First().Severity),
                group.First().Code,
                group.First().Message,
                group.Min(issue => issue.Date),
                group.Count(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["businessUnitId"] = group.First().BusinessUnitId?.Value ?? string.Empty
                }))
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Source, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<SimulationRunPipelineStepState> BuildPipeline(SimulationRunLifecycleStatus status)
    {
        string[] names = ["Initialize", "Data load", "Preconditions", "Policies", "Core simulation", "Post processing", "Finalize"];
        int activeIndex = status switch
        {
            SimulationRunLifecycleStatus.Running => 4,
            SimulationRunLifecycleStatus.Paused => 4,
            SimulationRunLifecycleStatus.Failed => 2,
            SimulationRunLifecycleStatus.Stopped => 4,
            _ => names.Length
        };
        return names
            .Select((name, index) =>
            {
                SimulationRunPipelineStepStatus stepStatus = status switch
                {
                    SimulationRunLifecycleStatus.Failed when index == activeIndex => SimulationRunPipelineStepStatus.Failed,
                    SimulationRunLifecycleStatus.Completed or SimulationRunLifecycleStatus.CompletedWithWarnings when index < names.Length => SimulationRunPipelineStepStatus.Completed,
                    SimulationRunLifecycleStatus.Running or SimulationRunLifecycleStatus.Paused when index < activeIndex => SimulationRunPipelineStepStatus.Completed,
                    SimulationRunLifecycleStatus.Running when index == activeIndex => SimulationRunPipelineStepStatus.Running,
                    SimulationRunLifecycleStatus.Paused when index == activeIndex => SimulationRunPipelineStepStatus.Running,
                    SimulationRunLifecycleStatus.Stopped when index == activeIndex => SimulationRunPipelineStepStatus.Skipped,
                    _ => SimulationRunPipelineStepStatus.Pending
                };
                return new SimulationRunPipelineStepState(
                    $"pipeline:{index + 1:00}",
                    index + 1,
                    name,
                    stepStatus,
                    stepStatus == SimulationRunPipelineStepStatus.Completed ? TimeSpan.FromSeconds((index + 1) * 3) : null,
                    stepStatus.ToString(),
                    new Dictionary<string, string>(StringComparer.Ordinal));
            })
            .ToArray();
    }

    private static SimulationRunResourceSnapshot BuildResourceSnapshot(SimulationRunLifecycleStatus status, DateTimeOffset observedAtUtc)
        => new(
            observedAtUtc,
            [
                new SimulationRunResourceMetricState("cpu", "CPU", status == SimulationRunLifecycleStatus.Running ? 42m : 18m, status == SimulationRunLifecycleStatus.Running ? "42%" : "18%", new Dictionary<string, string>()),
                new SimulationRunResourceMetricState("memory", "Memory", 39m, "6.1 GB / 15.8 GB", new Dictionary<string, string>()),
                new SimulationRunResourceMetricState("io", "I/O", status == SimulationRunLifecycleStatus.Running ? 28m : 8m, status == SimulationRunLifecycleStatus.Running ? "128 MB/s" : "16 MB/s", new Dictionary<string, string>()),
                new SimulationRunResourceMetricState("disk", "Disk", 31m, "312 MB/s", new Dictionary<string, string>())
            ],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "deterministic-simulator-sample"
            });

    private static IReadOnlyList<SimulationRunAssumptionItem> BuildAssumptions(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        IReadOnlyList<SimulationRunInterventionState> interventions)
        =>
        [
            new("scenario", "Scenario", setup.ScenarioName, "setup", new Dictionary<string, string>()),
            new("baseYear", "Base year", setup.HorizonStartDate.Year.ToString(CultureInfo.InvariantCulture), "setup", new Dictionary<string, string>()),
            new("calendar", "Calendar", project.Model.CalendarCode, "project", new Dictionary<string, string>()),
            new("rounding", "Rounding", "Nearest", "runtime", new Dictionary<string, string>()),
            new("inflation", "Inflation model", project.Metadata.TryGetValue("inflationModel", out string? model) ? model : "Core CPI", "FinancialInput", new Dictionary<string, string>()),
            new("fx", "FX model", "Static", "ExternalSector BO", new Dictionary<string, string>()),
            new("interest", "Interest rate model", "Yield curve", "MonetaryPolicy BO", new Dictionary<string, string>()),
            new("policy", "Policy set", project.Model.DefaultTaxRuleSetId ?? "Default Policy Pack", "FiscalPolicy BO", new Dictionary<string, string>()),
            new("seed", "Random seed", setup.Seed.ToString(CultureInfo.InvariantCulture), "setup", new Dictionary<string, string>()),
            ..interventions.Select(intervention => new SimulationRunAssumptionItem(
                intervention.InterventionId,
                intervention.DisplayName,
                intervention.Kind.ToString(),
                EconomySimulatorObjectTypes.RuntimeIntervention,
                new Dictionary<string, string>(intervention.Metadata, StringComparer.Ordinal)
                {
                    ["effectiveDate"] = intervention.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }))
        ];

    private static IReadOnlyList<SimulationOutputSourceRef> BuildOutputSources(
        SimulationProjectState project,
        IReadOnlyList<RuleExecutionEvidenceState> evidence)
    {
        BusinessUnitState treasury = project.BusinessUnits.FirstOrDefault(unit => unit.BusinessUnitId == project.Model.TreasuryBusinessUnitId) ?? project.BusinessUnits[0];
        BusinessUnitState external = project.BusinessUnits.FirstOrDefault(unit => unit.BusinessUnitId == project.Model.ExternalClearingBusinessUnitId) ?? treasury;
        BusinessUnitState operations = project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.CostCenter) ?? treasury;
        BusinessUnitState sales = project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.RevenueCenter) ?? treasury;
        string? firstRule = evidence.FirstOrDefault()?.RuleId;

        return
        [
            Source("realGdp", "Real GDP", "Revenue and operations BU rollup", sales, null, firstRule, evidence.FirstOrDefault()?.RuleId, "Sample macro projection mapped to revenue and operations activity."),
            Source("cpi", "CPI", "MonetaryPolicy BO", treasury, "def:project-config", firstRule, evidence.FirstOrDefault()?.RuleId, "Sample CPI path from financial input inflation assumption."),
            Source("unemployment", "Unemployment", "LaborMarket BU", operations, "def:payable-trigger", firstRule, evidence.FirstOrDefault()?.RuleId, "Workforce/payroll proxy pending provider-specific labor rules."),
            Source("policyRate", "Federal Funds Rate", "MonetaryPolicy BO", treasury, "def:reserve-rule", firstRule, evidence.FirstOrDefault()?.RuleId, "Policy-rate sample mapped to reserve guard rule."),
            Source("budgetBalance", "Budget Balance", "FiscalPolicy BO", treasury, "def:cashpool-rule", firstRule, evidence.FirstOrDefault()?.RuleId, "Budget balance sample mapped to treasury/fiscal policy evidence."),
            Source("currentAccount", "Current Account", "ExternalSector BO", external, null, firstRule, evidence.FirstOrDefault()?.RuleId, "External clearing BU owns current-account warning sample.")
        ];
    }

    private static IReadOnlyList<SimulationOutputSourceRef> BuildScenarioOutputSources(
        SimulationProjectState project,
        ScenarioRunMaterialization materialization,
        IReadOnlyList<RuleExecutionEvidenceState> evidence)
    {
        BusinessUnitState treasury = project.BusinessUnits.FirstOrDefault(unit => unit.BusinessUnitId == project.Model.TreasuryBusinessUnitId) ?? project.BusinessUnits[0];
        BusinessUnitState revenue = project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.RevenueCenter) ?? treasury;
        BusinessUnitState cost = project.BusinessUnits.FirstOrDefault(unit => unit.Kind == BusinessUnitKind.CostCenter) ?? treasury;
        string? ruleId = evidence.FirstOrDefault()?.RuleId;
        string? evidenceId = evidence.FirstOrDefault()?.RuleId;

        IReadOnlyList<SimulationOutputSourceRef> baseSources =
        [
            Source("cashBalance", "Cash balance", "Scenario projection cash-flow points", treasury, "def:project-config", ruleId, evidenceId, "Closing cash from compiled scenario events through selected step."),
            Source("minimumCash", "Minimum cash observed", "Scenario projection liquidity floor", treasury, "def:project-config", ruleId, evidenceId, "Minimum cash from the step projection compared with scenario minimum cash."),
            Source("revenue", "Revenue accrued", "Compiled customer demand and invoices", revenue, "def:scenario-model", ruleId, evidenceId, "Accrual revenue from customer contracts, subscriptions, implementation milestones, and product sales."),
            Source("expenses", "P&L expenses", "Compiled cost, payroll, COGS, and depreciation events", cost, "def:scenario-model", ruleId, evidenceId, "Scenario cost chain including COGS, OPEX, payroll, and depreciation."),
            Source("netCashFlow", "Net cash flow", "Compiled payment timing", treasury, "def:scenario-model", ruleId, evidenceId, "Cash inflows and outflows from event settlement dates."),
            Source("serviceHours", "Service hours delivered", "Work order service-hour tokens", revenue, "def:scenario-model", ruleId, evidenceId, $"Service-hour token count from {materialization.Events.Count(x => x.CategoryCode == "workdone-service-hours").ToString(CultureInfo.InvariantCulture)} workdone events."),
            Source("productFlowUnits", "Product-unit flow", "Product-flow control tokens", cost, "def:scenario-model", ruleId, evidenceId, $"Product-flow token count from {materialization.Events.Count(x => x.CategoryCode.StartsWith("product-flow-", StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture)} product-flow events.")
        ];
        if (materialization.Interventions.Count == 0)
        {
            return baseSources;
        }

        SimulationRunInterventionState latest = materialization.Interventions[^1];
        return
        [
            ..baseSources,
            Source("runtimeInterventions", "Runtime interventions", "Manual intervention BOs", treasury, latest.BusinessObjectDefinitionId, ruleId, evidenceId, $"{materialization.Interventions.Count.ToString(CultureInfo.InvariantCulture)} manual intervention(s) are applied to future materialized steps.")
        ];
    }

    private static SimulationOutputSourceRef Source(
        string outputCode,
        string displayName,
        string sourceKind,
        BusinessUnitState businessUnit,
        string? definitionId,
        string? ruleId,
        string? evidenceId,
        string notes)
        => new(
            outputCode,
            displayName,
            sourceKind,
            businessUnit.BusinessUnitId,
            businessUnit.DisplayName,
            definitionId,
            ruleId,
            evidenceId,
            notes,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "implemented-sample-source-map"
            });

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

    private static string DisplayStatus(SimulationRunLifecycleStatus status)
        => status switch
        {
            SimulationRunLifecycleStatus.CompletedWithWarnings => "Completed with warnings",
            _ => status.ToString()
        };

    private static SimulationValidationSeverity Severity(string value)
        => value.Equals("error", StringComparison.OrdinalIgnoreCase)
            ? SimulationValidationSeverity.Error
            : value.Equals("warning", StringComparison.OrdinalIgnoreCase)
                ? SimulationValidationSeverity.Warning
                : SimulationValidationSeverity.Info;

    private sealed class SimulationRunSession(SimulationRunMode mode)
    {
        public SimulationRunMode Mode { get; } = mode;

        public SortedDictionary<int, SimulationRunConsoleState> Snapshots { get; } = new();

        public List<SimulationRunInterventionState> Interventions { get; } = [];

        public int SelectedStep { get; set; } = 1;

        public SimulationRunConsoleState Selected { get; set; } = default!;

        public int AvailableStep => Snapshots.Count == 0 ? 0 : Snapshots.Keys.Max();

        public IReadOnlyList<SimulationRunStepSnapshotState> SnapshotMetadata
            => Snapshots
                .OrderBy(snapshot => snapshot.Key)
                .Select(snapshot => new SimulationRunStepSnapshotState(
                    snapshot.Key,
                    snapshot.Value.Run.CurrentDate,
                    snapshot.Value.Run.LastUpdatedAtUtc,
                    snapshot.Value.Run.OutputHash,
                    snapshot.Value.Run.Status,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["runId"] = snapshot.Value.Run.RunId,
                        ["mode"] = Mode.ToString()
                    }))
                .ToArray();
    }

    private sealed record ScenarioRunMaterialization(
        ScenarioBusinessModelState Scenario,
        IReadOnlyList<EconomicEventState> Events,
        SimulationResultState Result,
        IReadOnlyList<EconomyGraphData> Graphs,
        IReadOnlyDictionary<string, decimal> KpiValues,
        string OutputHash,
        IReadOnlyList<SimulationRunInterventionState> Interventions);

    private sealed record SeedRun(
        string RunId,
        SimulationRunLifecycleStatus Status,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? FinishedAtUtc,
        int CurrentStep,
        string Message);
}
