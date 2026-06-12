namespace CanDoItAll.Economy.Simulation.Abstractions;

public interface ISimulationSnapshotStore
{
    void Save(SimulationRunSnapshot snapshot);

    SimulationRunSnapshot? TryGet(string snapshotId);

    IReadOnlyList<SimulationSnapshotDescriptor> List(string? runId = null);

    bool Delete(string snapshotId);
}

public interface IAsyncSimulationSnapshotStore
{
    ValueTask SaveAsync(SimulationRunSnapshot snapshot, CancellationToken cancellationToken = default);

    ValueTask<SimulationRunSnapshot?> TryGetAsync(string snapshotId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<SimulationSnapshotDescriptor>> ListAsync(
        SimulationSnapshotDescriptorQuery? query = null,
        CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(string snapshotId, CancellationToken cancellationToken = default);
}

public sealed class SimulationSnapshotDescriptor
{
    public string SnapshotId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public DateTimeOffset SimulatedAtUtc { get; set; }
    public string DeterministicHash { get; set; } = string.Empty;
}

public sealed class SimulationSnapshotDescriptorQuery
{
    public string? RunId { get; set; }
    public string? ScenarioId { get; set; }
    public int? StepIndex { get; set; }
}

public interface ISimulationSnapshotPayloadCodec
{
    string FileExtension { get; }
    ValueTask WriteAsync(string path, string json, CancellationToken cancellationToken = default);
    ValueTask<string> ReadAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class PlainJsonSimulationSnapshotPayloadCodec : ISimulationSnapshotPayloadCodec
{
    public string FileExtension => ".json";

    public async ValueTask WriteAsync(string path, string json, CancellationToken cancellationToken = default)
        => await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);

    public async ValueTask<string> ReadAsync(string path, CancellationToken cancellationToken = default)
        => await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
}

public sealed class InMemorySimulationSnapshotStore : ISimulationSnapshotStore, IAsyncSimulationSnapshotStore
{
    private readonly Dictionary<string, string> snapshotsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimulationSnapshotDescriptor> descriptorsById = new(StringComparer.Ordinal);

    public void Save(SimulationRunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.SnapshotId))
        {
            throw new ArgumentException("Snapshot id is required.", nameof(snapshot));
        }

        string json = SimulationRunSnapshotSerializer.Serialize(snapshot);
        SimulationRunSnapshot stored = SimulationRunSnapshotSerializer.Deserialize(json, validateHash: true);
        snapshotsById[stored.SnapshotId] = json;
        descriptorsById[stored.SnapshotId] = Descriptor(stored);
    }

    public SimulationRunSnapshot? TryGet(string snapshotId)
    {
        if (string.IsNullOrWhiteSpace(snapshotId) || !snapshotsById.TryGetValue(snapshotId, out string? json))
        {
            return default;
        }

        return SimulationRunSnapshotSerializer.Deserialize(json, validateHash: true);
    }

    public IReadOnlyList<SimulationSnapshotDescriptor> List(string? runId = null)
        => List(new SimulationSnapshotDescriptorQuery { RunId = runId });

    public IReadOnlyList<SimulationSnapshotDescriptor> List(SimulationSnapshotDescriptorQuery? query)
        => [.. Query(descriptorsById.Values, query).Select(Descriptor)];

    public bool Delete(string snapshotId)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            return false;
        }

        descriptorsById.Remove(snapshotId);
        return snapshotsById.Remove(snapshotId);
    }

    private static SimulationSnapshotDescriptor Descriptor(SimulationRunSnapshot snapshot)
        => new()
        {
            SnapshotId = snapshot.SnapshotId,
            RunId = snapshot.RunId,
            ScenarioId = snapshot.ScenarioId,
            StepIndex = snapshot.StepIndex,
            SimulatedAtUtc = snapshot.SimulatedAtUtc,
            DeterministicHash = snapshot.DeterministicHash
        };

    public ValueTask SaveAsync(SimulationRunSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Save(snapshot);
        return ValueTask.CompletedTask;
    }

    public ValueTask<SimulationRunSnapshot?> TryGetAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TryGet(snapshotId));
    }

    public ValueTask<IReadOnlyList<SimulationSnapshotDescriptor>> ListAsync(
        SimulationSnapshotDescriptorQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(List(query));
    }

    public ValueTask<bool> DeleteAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Delete(snapshotId));
    }

    private static SimulationSnapshotDescriptor Descriptor(SimulationSnapshotDescriptor descriptor)
        => new()
        {
            SnapshotId = descriptor.SnapshotId,
            RunId = descriptor.RunId,
            ScenarioId = descriptor.ScenarioId,
            StepIndex = descriptor.StepIndex,
            SimulatedAtUtc = descriptor.SimulatedAtUtc,
            DeterministicHash = descriptor.DeterministicHash
        };

    internal static IEnumerable<SimulationSnapshotDescriptor> Query(
        IEnumerable<SimulationSnapshotDescriptor> descriptors,
        SimulationSnapshotDescriptorQuery? query)
        => descriptors
            .Where(descriptor => string.IsNullOrWhiteSpace(query?.RunId) || string.Equals(descriptor.RunId, query.RunId, StringComparison.Ordinal))
            .Where(descriptor => string.IsNullOrWhiteSpace(query?.ScenarioId) || string.Equals(descriptor.ScenarioId, query.ScenarioId, StringComparison.Ordinal))
            .Where(descriptor => query?.StepIndex is null || descriptor.StepIndex == query.StepIndex)
            .OrderBy(static descriptor => descriptor.RunId, StringComparer.Ordinal)
            .ThenBy(static descriptor => descriptor.ScenarioId, StringComparer.Ordinal)
            .ThenBy(static descriptor => descriptor.StepIndex)
            .ThenBy(static descriptor => descriptor.SnapshotId, StringComparer.Ordinal);
}
