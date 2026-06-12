using System.Globalization;

namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationSnapshotDiffResult
{
    public string LeftSnapshotId { get; set; } = string.Empty;
    public string RightSnapshotId { get; set; } = string.Empty;
    public List<SimulationSnapshotDiffEntry> Changes { get; set; } = [];
    public bool HasChanges => Changes.Count > 0;
}

public sealed class SimulationSnapshotDiffEntry
{
    public string Path { get; set; } = string.Empty;
    public string ChangeKind { get; set; } = string.Empty;
    public string LeftValue { get; set; } = string.Empty;
    public string RightValue { get; set; } = string.Empty;
}

public static class SimulationSnapshotDiff
{
    public static SimulationSnapshotDiffResult Compare(SimulationRunSnapshot left, SimulationRunSnapshot right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var result = new SimulationSnapshotDiffResult
        {
            LeftSnapshotId = left.SnapshotId,
            RightSnapshotId = right.SnapshotId
        };

        AddScalar(result, "stepIndex", left.StepIndex.ToString(CultureInfo.InvariantCulture), right.StepIndex.ToString(CultureInfo.InvariantCulture));
        AddScalar(result, "frameHash", SimulationDeterministicHash.HashFrame(left.Frame), SimulationDeterministicHash.HashFrame(right.Frame));
        AddScalar(result, "visualStateHash", SimulationDeterministicHash.HashVisualState(left.VisualState), SimulationDeterministicHash.HashVisualState(right.VisualState));
        CompareItems(result, "actors", left.Frame.Actors, right.Frame.Actors, static actor => actor.ActorId, ActorFingerprint);
        CompareItems(result, "resourceStores", left.Frame.ResourceStores, right.Frame.ResourceStores, static store => store.StoreId, StoreFingerprint);
        CompareItems(result, "flows", left.Frame.Flows, right.Frame.Flows, static flow => flow.FlowId, FlowFingerprint);
        CompareItems(result, "relationships", left.Frame.Relationships, right.Frame.Relationships, static relationship => relationship.RelationshipId, RelationshipFingerprint);
        CompareItems(result, "issues", left.Frame.Issues, right.Frame.Issues, static issue => issue.IssueId, IssueFingerprint);
        CompareItems(result, "metrics", left.Metrics, right.Metrics, static metric => metric.MetricId, MetricFingerprint);
        CompareItems(result, "invariants", left.Invariants, right.Invariants, static invariant => invariant.InvariantId, InvariantFingerprint);
        CompareItems(result, "appliedEvents", left.AppliedEvents, right.AppliedEvents, static item => item.EventId, EventFingerprint);
        CompareItems(result, "pendingEvents", left.PendingEvents, right.PendingEvents, static item => item.EventId, EventFingerprint);
        CompareKeyValues(result, "provenanceHashes", left.ProvenanceHashes, right.ProvenanceHashes);
        CompareKeyValues(result, "metadata", left.Metadata, right.Metadata);
        CompareVisualState(result, left.VisualState, right.VisualState);
        return result;
    }

    private static void CompareItems<T>(
        SimulationSnapshotDiffResult result,
        string path,
        IEnumerable<T> left,
        IEnumerable<T> right,
        Func<T, string> keySelector,
        Func<T, string> fingerprint)
    {
        Dictionary<string, T> leftById = left.ToDictionary(keySelector, StringComparer.Ordinal);
        Dictionary<string, T> rightById = right.ToDictionary(keySelector, StringComparer.Ordinal);
        foreach (string id in leftById.Keys.Union(rightById.Keys, StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal))
        {
            bool hasLeft = leftById.TryGetValue(id, out T? leftItem);
            bool hasRight = rightById.TryGetValue(id, out T? rightItem);
            string itemPath = $"{path}[{id}]";
            if (!hasLeft)
            {
                Add(result, itemPath, "added", string.Empty, fingerprint(rightItem!));
            }
            else if (!hasRight)
            {
                Add(result, itemPath, "removed", fingerprint(leftItem!), string.Empty);
            }
            else
            {
                AddScalar(result, itemPath, fingerprint(leftItem!), fingerprint(rightItem!));
            }
        }
    }

    private static void AddScalar(SimulationSnapshotDiffResult result, string path, string left, string right)
    {
        if (!string.Equals(left, right, StringComparison.Ordinal))
        {
            Add(result, path, "changed", left, right);
        }
    }

    private static void Add(SimulationSnapshotDiffResult result, string path, string kind, string left, string right)
        => result.Changes.Add(new() { Path = path, ChangeKind = kind, LeftValue = left, RightValue = right });

    private static string ActorFingerprint(SimulationActor actor)
        => Join(actor.Kind, actor.DisplayName, actor.Role, Metadata(actor.Metadata));

    private static string StoreFingerprint(SimulationResourceStore store)
        => Join(store.OwnerActorId, store.ResourceId, store.ResourceKind, store.DisplayName, Number(store.Quantity), store.Unit, Number(store.Capacity), Metadata(store.Metadata));

    private static string FlowFingerprint(SimulationResourceFlow flow)
        => Join(flow.FromStoreId, flow.ToStoreId, flow.FromActorId, flow.ToActorId, flow.ResourceId, Number(flow.Quantity), flow.Unit, flow.Reason, flow.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture), Metadata(flow.Metadata));

    private static string RelationshipFingerprint(SimulationRelationship relationship)
        => Join(relationship.SourceActorId, relationship.TargetActorId, relationship.Kind, Number(relationship.Strength), Metadata(relationship.Metadata));

    private static string IssueFingerprint(SimulationIssue issue)
        => Join(issue.Severity, issue.Category, issue.Title, issue.Description, issue.IsResolved.ToString(), string.Join(",", issue.ActorIds.OrderBy(static id => id, StringComparer.Ordinal)), string.Join(",", issue.ResourceIds.OrderBy(static id => id, StringComparer.Ordinal)), Metadata(issue.Metadata));

    private static string MetricFingerprint(SimulationMetricValue metric)
        => Join(Number(metric.Value), metric.Unit, Metadata(metric.Metadata));

    private static string InvariantFingerprint(SimulationInvariantEvaluationResult invariant)
        => Join(invariant.Passed.ToString(), invariant.Severity, invariant.Message, Metadata(invariant.Metadata));

    private static string EventFingerprint(SimulationEvent item)
        => Join(item.EventKind, item.ActorId, item.TargetActorId, item.ResourceId, Number(item.Magnitude), item.Timing.StepIndex.ToString(CultureInfo.InvariantCulture), Metadata(item.Metadata));

    private static void CompareVisualState(
        SimulationSnapshotDiffResult result,
        SimulationSnapshotVisualState? left,
        SimulationSnapshotVisualState? right)
    {
        AddScalar(result, "visualState.present", (left is not null).ToString(), (right is not null).ToString());
        if (left is null || right is null)
        {
            return;
        }

        AddScalar(result, "visualState.visualFrameId", left.VisualFrameId, right.VisualFrameId);
        AddScalar(result, "visualState.playbackFrameId", left.PlaybackFrameId, right.PlaybackFrameId);
        AddScalar(result, "visualState.playbackFrameIndex", left.PlaybackFrameIndex.ToString(CultureInfo.InvariantCulture), right.PlaybackFrameIndex.ToString(CultureInfo.InvariantCulture));
        CompareKeyValues(result, "visualState.nodeObjectIds", left.NodeObjectIds, right.NodeObjectIds);
        CompareLists(result, "visualState.activeActionIds", left.ActiveActionIds, right.ActiveActionIds);
        CompareLists(result, "visualState.pendingActionIds", left.PendingActionIds, right.PendingActionIds);
        CompareLists(result, "visualState.activeStageIds", left.ActiveStageIds, right.ActiveStageIds);
        CompareLists(result, "visualState.pendingStageIds", left.PendingStageIds, right.PendingStageIds);
        CompareKeyValues(result, "visualState.runtimeDiagnostics", left.RuntimeDiagnostics, right.RuntimeDiagnostics);
        CompareKeyValues(result, "visualState.metadata", left.Metadata, right.Metadata);
    }

    private static void CompareKeyValues(
        SimulationSnapshotDiffResult result,
        string path,
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        foreach (string key in left.Keys.Union(right.Keys, StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal))
        {
            bool hasLeft = left.TryGetValue(key, out string? leftValue);
            bool hasRight = right.TryGetValue(key, out string? rightValue);
            string itemPath = $"{path}[{key}]";
            if (!hasLeft)
            {
                Add(result, itemPath, "added", string.Empty, rightValue ?? string.Empty);
            }
            else if (!hasRight)
            {
                Add(result, itemPath, "removed", leftValue ?? string.Empty, string.Empty);
            }
            else
            {
                AddScalar(result, itemPath, leftValue ?? string.Empty, rightValue ?? string.Empty);
            }
        }
    }

    private static void CompareLists(
        SimulationSnapshotDiffResult result,
        string path,
        IReadOnlyCollection<string> left,
        IReadOnlyCollection<string> right)
    {
        CompareKeyValues(
            result,
            path,
            left.Distinct(StringComparer.Ordinal).ToDictionary(static value => value, static value => value, StringComparer.Ordinal),
            right.Distinct(StringComparer.Ordinal).ToDictionary(static value => value, static value => value, StringComparer.Ordinal));
    }

    private static string Metadata(Dictionary<string, string> metadata)
        => string.Join(",", metadata.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(static item => $"{item.Key}={item.Value}"));

    private static string Number(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Join(params string[] values)
        => string.Join("|", values);
}
