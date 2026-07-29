namespace CanDoItAll.Ledger.Simulation.Abstractions;

public interface ISimulationSnapshotBuilder
{
    SimulationSnapshotBuildResult Build(SimulationSnapshotBuildRequest request);
}

public sealed class SimulationSnapshotBuildRequest
{
    public string SnapshotId { get; set; } = string.Empty;
    public SimulationScenarioDefinition Scenario { get; set; } = new();
    public SimulationRunIdentity Run { get; set; } = new();
    public SimulationFrame CurrentFrame { get; set; } = new();
    public SimulationFrameDelta? LastDelta { get; set; }
    public List<SimulationEvent> AppliedEvents { get; set; } = [];
    public List<SimulationEvent> PendingEvents { get; set; } = [];
    public List<SimulationMetricValue> Metrics { get; set; } = [];
    public List<SimulationInvariantEvaluationResult> Invariants { get; set; } = [];
    public SimulationSnapshotVisualState? VisualState { get; set; }
    public Dictionary<string, string> RuntimeDiagnostics { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationSnapshotBuildResult
{
    public SimulationRunSnapshot Snapshot { get; set; } = new();
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public bool Succeeded => Errors.Count == 0;
    public bool IsValid => Succeeded;
}

public sealed class SimulationSnapshotBuilder(
    SimulationSnapshotProvenanceBuilder? provenanceBuilder = null) : ISimulationSnapshotBuilder
{
    private readonly SimulationSnapshotProvenanceBuilder provenanceBuilder = provenanceBuilder ?? new SimulationSnapshotProvenanceBuilder();

    public SimulationSnapshotBuildResult Build(SimulationSnapshotBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new SimulationSnapshotBuildResult();
        string runId = FirstNonEmpty(request.Run.RunId, request.CurrentFrame.Run.RunId);
        string scenarioId = FirstNonEmpty(request.Scenario.ScenarioId, request.Run.ScenarioId, request.CurrentFrame.Run.ScenarioId);
        Validate(request, runId, scenarioId, result);
        if (!result.Succeeded)
        {
            return result;
        }

        EnsureFrameIdentity(request, runId, scenarioId);
        EnsureHashes(request);
        var snapshot = new SimulationRunSnapshot
        {
            SnapshotId = FirstNonEmpty(request.SnapshotId, $"snapshot.{runId}.{request.CurrentFrame.Clock.StepIndex}"),
            RunId = runId,
            ScenarioId = scenarioId,
            StepIndex = request.CurrentFrame.Clock.StepIndex,
            SimulatedAtUtc = request.CurrentFrame.Clock.SimulatedAtUtc,
            Frame = request.CurrentFrame,
            LastDelta = request.LastDelta,
            AppliedEvents = request.AppliedEvents.Count == 0 ? [.. request.CurrentFrame.Events] : [.. request.AppliedEvents],
            PendingEvents = [.. request.PendingEvents],
            Metrics = [.. request.Metrics],
            Invariants = [.. request.Invariants],
            VisualState = BuildVisualState(request),
            Metadata = BuildMetadata(request, result)
        };

        snapshot.ProvenanceHashes = provenanceBuilder.Build(request, snapshot);
        snapshot.RefreshDeterministicHash();
        snapshot.ProvenanceHashes["snapshot.full"] = snapshot.DeterministicHash;
        snapshot.RefreshDeterministicHash();
        result.Snapshot = snapshot;
        return result;
    }

    private static void Validate(
        SimulationSnapshotBuildRequest request,
        string runId,
        string scenarioId,
        SimulationSnapshotBuildResult result)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            result.Errors.Add("Snapshot build request requires a run id.");
        }

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            result.Errors.Add("Snapshot build request requires a scenario id.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentFrame.FrameId))
        {
            result.Errors.Add("Snapshot build request requires a current frame id.");
        }

        if (request.CurrentFrame.Clock.StepIndex < 0)
        {
            result.Errors.Add("Snapshot build request step index cannot be negative.");
        }
    }

    private static void EnsureFrameIdentity(SimulationSnapshotBuildRequest request, string runId, string scenarioId)
    {
        request.CurrentFrame.Run.RunId = FirstNonEmpty(request.CurrentFrame.Run.RunId, runId);
        request.CurrentFrame.Run.ScenarioId = FirstNonEmpty(request.CurrentFrame.Run.ScenarioId, scenarioId);
        request.CurrentFrame.Run.BackendId = FirstNonEmpty(request.CurrentFrame.Run.BackendId, request.Run.BackendId);
        if (request.CurrentFrame.Run.CreatedAtUtc == default)
        {
            request.CurrentFrame.Run.CreatedAtUtc = request.Run.CreatedAtUtc;
        }
    }

    private static void EnsureHashes(SimulationSnapshotBuildRequest request)
    {
        request.CurrentFrame.DeterministicHash = SimulationDeterministicHash.HashFrame(request.CurrentFrame);
        if (request.LastDelta is not null)
        {
            request.LastDelta.DeterministicHash = SimulationDeterministicHash.HashDelta(request.LastDelta);
        }
    }

    private static Dictionary<string, string> BuildMetadata(
        SimulationSnapshotBuildRequest request,
        SimulationSnapshotBuildResult result)
    {
        var metadata = new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal)
        {
            ["builder"] = nameof(SimulationSnapshotBuilder)
        };
        if (request.RuntimeDiagnostics.Count > 0)
        {
            metadata["runtimeDiagnosticsAttached"] = "true";
        }

        if (request.VisualState is null && request.RuntimeDiagnostics.Count > 0)
        {
            result.Warnings.Add("Runtime diagnostics were attached without a visual state shell.");
        }

        return metadata;
    }

    private static SimulationSnapshotVisualState? BuildVisualState(SimulationSnapshotBuildRequest request)
    {
        SimulationSnapshotVisualState? visualState = Clone(request.VisualState);
        if (request.RuntimeDiagnostics.Count == 0)
        {
            return visualState;
        }

        visualState ??= new();
        foreach (KeyValuePair<string, string> diagnostic in request.RuntimeDiagnostics.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            visualState.RuntimeDiagnostics[diagnostic.Key] = diagnostic.Value;
        }

        return visualState;
    }

    private static SimulationSnapshotVisualState? Clone(SimulationSnapshotVisualState? visualState)
        => visualState is null
            ? null
            : new()
            {
                VisualFrameId = visualState.VisualFrameId,
                PlaybackFrameId = visualState.PlaybackFrameId,
                PlaybackFrameIndex = visualState.PlaybackFrameIndex,
                NodeObjectIds = new Dictionary<string, string>(visualState.NodeObjectIds, StringComparer.Ordinal),
                ActiveActionIds = [.. visualState.ActiveActionIds],
                PendingActionIds = [.. visualState.PendingActionIds],
                ActiveStageIds = [.. visualState.ActiveStageIds],
                PendingStageIds = [.. visualState.PendingStageIds],
                RuntimeDiagnostics = new Dictionary<string, string>(visualState.RuntimeDiagnostics, StringComparer.Ordinal),
                RuntimeAttachment = Clone(visualState.RuntimeAttachment),
                Metadata = new Dictionary<string, string>(visualState.Metadata, StringComparer.Ordinal)
            };

    private static SimulationSnapshotRuntimeAttachment? Clone(SimulationSnapshotRuntimeAttachment? attachment)
        => attachment is null
            ? null
            : new()
            {
                RuntimeKind = attachment.RuntimeKind,
                CurrentPlaybackCommand = attachment.CurrentPlaybackCommand,
                CurrentFrameIndex = attachment.CurrentFrameIndex,
                ActiveMotionIds = [.. attachment.ActiveMotionIds],
                QueuedMotionIds = [.. attachment.QueuedMotionIds],
                ActiveStageIds = [.. attachment.ActiveStageIds],
                PendingStageIds = [.. attachment.PendingStageIds],
                ActiveBarrierPolicy = attachment.ActiveBarrierPolicy,
                ActiveBarrierBlockers = [.. attachment.ActiveBarrierBlockers],
                CommandJournalTail = [.. attachment.CommandJournalTail],
                RenderDiagnosticsSummary = attachment.RenderDiagnosticsSummary,
                SelectedObjectIds = [.. attachment.SelectedObjectIds],
                SelectedActionIds = [.. attachment.SelectedActionIds],
                Diagnostics = new Dictionary<string, string>(attachment.Diagnostics, StringComparer.Ordinal),
                Metadata = new Dictionary<string, string>(attachment.Metadata, StringComparer.Ordinal)
            };

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class SimulationSnapshotProvenanceBuilder
{
    public Dictionary<string, string> Build(SimulationSnapshotBuildRequest request, SimulationRunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);

        string dataStateHash = BuildDataHash(snapshot);
        string visualRuntimeHash = SimulationDeterministicHash.HashVisualRuntimeState(snapshot.VisualState);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scenario"] = SimulationDeterministicHash.HashDefinition(request.Scenario),
            ["frame"] = SimulationDeterministicHash.HashFrame(snapshot.Frame),
            ["snapshot.data"] = dataStateHash,
            ["snapshot.dataState"] = dataStateHash,
            ["snapshot.visualState"] = visualRuntimeHash,
            ["snapshot.visualRuntime"] = visualRuntimeHash
        };
        if (snapshot.LastDelta is not null)
        {
            hashes["lastDelta"] = SimulationDeterministicHash.HashDelta(snapshot.LastDelta);
        }

        return hashes;
    }

    public string BuildDataHash(SimulationRunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var dataSnapshot = new SimulationRunSnapshot
        {
            SnapshotId = snapshot.SnapshotId,
            SchemaVersion = snapshot.SchemaVersion,
            RunId = snapshot.RunId,
            ScenarioId = snapshot.ScenarioId,
            StepIndex = snapshot.StepIndex,
            SimulatedAtUtc = snapshot.SimulatedAtUtc,
            Frame = snapshot.Frame,
            LastDelta = snapshot.LastDelta,
            AppliedEvents = [.. snapshot.AppliedEvents],
            PendingEvents = [.. snapshot.PendingEvents],
            Metrics = [.. snapshot.Metrics],
            Invariants = [.. snapshot.Invariants],
            Metadata = StableDataMetadata(snapshot.Metadata)
        };
        return SimulationRunSnapshotHasher.CalculateHash(dataSnapshot);
    }

    private static Dictionary<string, string> StableDataMetadata(Dictionary<string, string> metadata)
        => new(metadata
            .Where(static item => !item.Key.StartsWith("runtime.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static item => item.Key, StringComparer.Ordinal), StringComparer.Ordinal);
}
