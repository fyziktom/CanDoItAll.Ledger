namespace CanDoItAll.Ledger.Simulation.Abstractions;

public static partial class SimulationDeterministicHash
{
    public static string HashSnapshot(SimulationRunSnapshot snapshot)
        => HashObject(new
        {
            snapshot.SchemaVersion,
            snapshot.SnapshotId,
            snapshot.RunId,
            snapshot.ScenarioId,
            snapshot.StepIndex,
            snapshot.SimulatedAtUtc,
            FrameHash = HashFrame(snapshot.Frame),
            LastDeltaHash = snapshot.LastDelta is null
                ? string.Empty
                : HashDelta(snapshot.LastDelta),
            AppliedEvents = snapshot.AppliedEvents.OrderBy(item => item.EventId, StringComparer.Ordinal).Select(ProjectEvent),
            PendingEvents = snapshot.PendingEvents.OrderBy(item => item.EventId, StringComparer.Ordinal).Select(ProjectEvent),
            Metrics = snapshot.Metrics.OrderBy(item => item.MetricId, StringComparer.Ordinal).Select(ProjectMetric),
            Invariants = snapshot.Invariants.OrderBy(item => item.InvariantId, StringComparer.Ordinal).Select(ProjectInvariant),
            VisualState = ProjectVisualState(snapshot.VisualState),
            ProvenanceHashes = StableProvenanceHashes(snapshot.ProvenanceHashes),
            Metadata = StableMetadata(snapshot.Metadata)
        });

    public static string HashVisualState(SimulationSnapshotVisualState? visualState)
        => HashObject(new
        {
            VisualState = ProjectVisualState(visualState)
        });

    public static string HashVisualRuntimeState(SimulationSnapshotVisualState? visualState)
        => HashObject(new
        {
            VisualRuntimeState = ProjectVisualState(visualState)
        });

    private static object? ProjectVisualState(SimulationSnapshotVisualState? visualState)
        => visualState is null
            ? null
            : new
            {
                visualState.VisualFrameId,
                visualState.PlaybackFrameId,
                visualState.PlaybackFrameIndex,
                NodeObjectIds = new SortedDictionary<string, string>(visualState.NodeObjectIds, StringComparer.Ordinal),
                ActiveActionIds = visualState.ActiveActionIds.OrderBy(id => id, StringComparer.Ordinal),
                PendingActionIds = visualState.PendingActionIds.OrderBy(id => id, StringComparer.Ordinal),
                ActiveStageIds = visualState.ActiveStageIds.OrderBy(id => id, StringComparer.Ordinal),
                PendingStageIds = visualState.PendingStageIds.OrderBy(id => id, StringComparer.Ordinal),
                RuntimeDiagnostics = new SortedDictionary<string, string>(visualState.RuntimeDiagnostics, StringComparer.Ordinal),
                RuntimeAttachment = ProjectRuntimeAttachment(visualState.RuntimeAttachment),
                Metadata = StableMetadata(visualState.Metadata)
            };

    private static object? ProjectRuntimeAttachment(SimulationSnapshotRuntimeAttachment? attachment)
        => attachment is null
            ? null
            : new
            {
                attachment.RuntimeKind,
                attachment.CurrentPlaybackCommand,
                attachment.CurrentFrameIndex,
                ActiveMotionIds = attachment.ActiveMotionIds.OrderBy(id => id, StringComparer.Ordinal),
                QueuedMotionIds = attachment.QueuedMotionIds.OrderBy(id => id, StringComparer.Ordinal),
                ActiveStageIds = attachment.ActiveStageIds.OrderBy(id => id, StringComparer.Ordinal),
                PendingStageIds = attachment.PendingStageIds.OrderBy(id => id, StringComparer.Ordinal),
                attachment.ActiveBarrierPolicy,
                ActiveBarrierBlockers = attachment.ActiveBarrierBlockers.OrderBy(id => id, StringComparer.Ordinal),
                CommandJournalTail = attachment.CommandJournalTail,
                attachment.RenderDiagnosticsSummary,
                SelectedObjectIds = attachment.SelectedObjectIds.OrderBy(id => id, StringComparer.Ordinal),
                SelectedActionIds = attachment.SelectedActionIds.OrderBy(id => id, StringComparer.Ordinal),
                Diagnostics = new SortedDictionary<string, string>(attachment.Diagnostics, StringComparer.Ordinal),
                Metadata = StableMetadata(attachment.Metadata)
            };

    private static SortedDictionary<string, string> StableProvenanceHashes(Dictionary<string, string> hashes)
        => new(hashes
            .Where(static item => !string.Equals(item.Key, "snapshot.full", StringComparison.Ordinal))
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal);
}
