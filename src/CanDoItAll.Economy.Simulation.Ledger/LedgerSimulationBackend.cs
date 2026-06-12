using CanDoItAll.Economy.Simulation.Abstractions;

namespace CanDoItAll.Economy.Simulation.Ledger;

public sealed class LedgerSimulationBackend : ISimulationBackend
{
    private readonly LedgerProjectionFrameProjector projector = new();
    private readonly SimulationScenarioManifest manifest;
    private readonly SimulationRunIdentity run;
    private readonly IReadOnlyDictionary<int, LedgerSnapshotSimulationSource> snapshotsByStep;

    public LedgerSimulationBackend(
        SimulationScenarioManifest manifest,
        SimulationRunIdentity run,
        IReadOnlyList<LedgerSnapshotSimulationSource> snapshots)
    {
        LedgerSnapshotSequenceValidator.Validate(snapshots);
        this.manifest = manifest;
        this.run = run;
        snapshotsByStep = snapshots
            .Select((snapshot, index) => new { Step = index, Snapshot = snapshot })
            .ToDictionary(static item => item.Step, static item => item.Snapshot);
    }

    public string BackendId => "ledger-adapter";

    public SimulationBackendCapabilities Capabilities { get; } = new()
    {
        BackendId = "ledger-adapter",
        SupportsDeterministicReplay = true,
        SupportsFrameDeltas = true,
        SupportsLedgerForks = true,
        SupportsIssueProjection = true
    };

    public ValueTask<SimulationScenarioManifest> GetManifestAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(manifest.ScenarioId, scenarioId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException($"Unknown ledger simulation scenario '{scenarioId}'.");
        }

        return ValueTask.FromResult(manifest);
    }

    public ValueTask<SimulationFrame> GetFrameAsync(string runId, int stepIndex, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(run.RunId, runId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException($"Unknown ledger simulation run '{runId}'.");
        }

        LedgerSnapshotSimulationSource source = snapshotsByStep.TryGetValue(stepIndex, out LedgerSnapshotSimulationSource? snapshot)
            ? snapshot
            : throw new KeyNotFoundException($"Unknown ledger simulation step '{stepIndex}'.");
        return ValueTask.FromResult(projector.Project(source, manifest, run, stepIndex));
    }

    public async ValueTask<SimulationFrameDelta> GetDeltaAsync(string runId, int fromStepIndex, int toStepIndex, CancellationToken cancellationToken = default)
    {
        SimulationFrame? previous = fromStepIndex == toStepIndex
            ? null
            : await GetFrameAsync(runId, fromStepIndex, cancellationToken).ConfigureAwait(false);
        SimulationFrame frame = await GetFrameAsync(runId, toStepIndex, cancellationToken).ConfigureAwait(false);
        var delta = new SimulationFrameDelta
        {
            DeltaId = $"{runId}.ledger.delta.{fromStepIndex}.{toStepIndex}",
            RunId = runId,
            FromStepIndex = fromStepIndex,
            ToStepIndex = toStepIndex,
            UpdatedResourceStores = previous is null ? [.. frame.ResourceStores] : ChangedStores(previous.ResourceStores, frame.ResourceStores),
            AddedEvents = previous is null ? [.. frame.Events] : AddedById(previous.Events, frame.Events, static item => item.EventId),
            AddedIssues = previous is null ? [.. frame.Issues] : AddedById(previous.Issues, frame.Issues, static item => item.IssueId),
            AddedArtifacts = previous is null ? [.. frame.Artifacts] : AddedById(previous.Artifacts, frame.Artifacts, static item => item.ArtifactId),
            ResolvedIssueIds = previous is null ? [] : RemovedIds(previous.Issues, frame.Issues, static item => item.IssueId),
            Metadata =
            {
                ["adapter"] = "ledger",
                ["diffMode"] = "dictionary"
            }
        };
        delta.DeterministicHash = SimulationDeterministicHash.HashDelta(delta);
        return delta;
    }

    private static List<SimulationResourceStore> ChangedStores(
        IReadOnlyList<SimulationResourceStore> previous,
        IReadOnlyList<SimulationResourceStore> current)
    {
        Dictionary<string, SimulationResourceStore> previousById = previous.ToDictionary(static item => item.StoreId, StringComparer.Ordinal);
        return [.. current.Where(store => !previousById.TryGetValue(store.StoreId, out SimulationResourceStore? old) || !StoreEquals(old, store))];
    }

    private static bool StoreEquals(SimulationResourceStore left, SimulationResourceStore right)
        => left.OwnerActorId == right.OwnerActorId &&
           left.ResourceId == right.ResourceId &&
           left.ResourceKind == right.ResourceKind &&
           left.DisplayName == right.DisplayName &&
           left.Quantity == right.Quantity &&
           left.Unit == right.Unit &&
           left.Capacity == right.Capacity;

    private static List<T> AddedById<T>(IReadOnlyList<T> previous, IReadOnlyList<T> current, Func<T, string> idSelector)
    {
        var previousIds = new HashSet<string>(previous.Select(idSelector), StringComparer.Ordinal);
        return [.. current.Where(item => !previousIds.Contains(idSelector(item)))];
    }

    private static List<string> RemovedIds<T>(IReadOnlyList<T> previous, IReadOnlyList<T> current, Func<T, string> idSelector)
    {
        var currentIds = new HashSet<string>(current.Select(idSelector), StringComparer.Ordinal);
        return [.. previous.Select(idSelector).Where(id => !currentIds.Contains(id))];
    }
}
