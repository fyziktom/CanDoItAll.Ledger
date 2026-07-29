namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationRunSnapshot
{
    public string SnapshotId { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "candoitall.economy.simulation-snapshot/v1";
    public string RunId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public DateTimeOffset SimulatedAtUtc { get; set; }
    public SimulationFrame Frame { get; set; } = new();
    public SimulationFrameDelta? LastDelta { get; set; }
    public List<SimulationEvent> AppliedEvents { get; set; } = [];
    public List<SimulationEvent> PendingEvents { get; set; } = [];
    public List<SimulationMetricValue> Metrics { get; set; } = [];
    public List<SimulationInvariantEvaluationResult> Invariants { get; set; } = [];
    public SimulationSnapshotVisualState? VisualState { get; set; }
    public Dictionary<string, string> ProvenanceHashes { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
    public string DeterministicHash { get; set; } = string.Empty;

    public string RefreshDeterministicHash()
    {
        DeterministicHash = SimulationRunSnapshotHasher.CalculateHash(this);
        if (ProvenanceHashes.ContainsKey("snapshot.full"))
        {
            ProvenanceHashes["snapshot.full"] = DeterministicHash;
        }

        return DeterministicHash;
    }
}

public sealed class SimulationSnapshotVisualState
{
    public string VisualFrameId { get; set; } = string.Empty;
    public string PlaybackFrameId { get; set; } = string.Empty;
    public long PlaybackFrameIndex { get; set; }
    public Dictionary<string, string> NodeObjectIds { get; set; } = [];
    public List<string> ActiveActionIds { get; set; } = [];
    public List<string> PendingActionIds { get; set; } = [];
    public List<string> ActiveStageIds { get; set; } = [];
    public List<string> PendingStageIds { get; set; } = [];
    public Dictionary<string, string> RuntimeDiagnostics { get; set; } = [];
    public SimulationSnapshotRuntimeAttachment? RuntimeAttachment { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationSnapshotRuntimeAttachment
{
    public string RuntimeKind { get; set; } = "visual-runtime";
    public string CurrentPlaybackCommand { get; set; } = string.Empty;
    public long CurrentFrameIndex { get; set; }
    public List<string> ActiveMotionIds { get; set; } = [];
    public List<string> QueuedMotionIds { get; set; } = [];
    public List<string> ActiveStageIds { get; set; } = [];
    public List<string> PendingStageIds { get; set; } = [];
    public string ActiveBarrierPolicy { get; set; } = string.Empty;
    public List<string> ActiveBarrierBlockers { get; set; } = [];
    public List<string> CommandJournalTail { get; set; } = [];
    public string RenderDiagnosticsSummary { get; set; } = string.Empty;
    public List<string> SelectedObjectIds { get; set; } = [];
    public List<string> SelectedActionIds { get; set; } = [];
    public Dictionary<string, string> Diagnostics { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public static class SimulationRunSnapshotHasher
{
    public static string CalculateHash(SimulationRunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SimulationDeterministicHash.HashSnapshot(snapshot);
    }
}
