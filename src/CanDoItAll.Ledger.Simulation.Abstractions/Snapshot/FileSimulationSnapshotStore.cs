using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class FileSimulationSnapshotStore(
    string rootDirectory,
    ISimulationSnapshotPayloadCodec? payloadCodec = null) : IAsyncSimulationSnapshotStore
{
    private const string DescriptorIndexFileName = "descriptors.index.json";
    private const string DescriptorIndexSchemaVersion = "candoitall.economy.simulation-snapshot-descriptors/v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly string rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
        ? throw new ArgumentException("Snapshot root directory is required.", nameof(rootDirectory))
        : rootDirectory;
    private readonly ISimulationSnapshotPayloadCodec payloadCodec = payloadCodec ?? new PlainJsonSimulationSnapshotPayloadCodec();
    private readonly Dictionary<string, SimulationSnapshotDescriptor> descriptorsById = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool indexLoaded;

    public async ValueTask SaveAsync(SimulationRunSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.SnapshotId))
        {
            throw new ArgumentException("Snapshot id is required.", nameof(snapshot));
        }

        Directory.CreateDirectory(rootDirectory);
        await payloadCodec.WriteAsync(PathFor(snapshot.SnapshotId), SimulationRunSnapshotSerializer.Serialize(snapshot), cancellationToken).ConfigureAwait(false);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureIndexCoreAsync(cancellationToken).ConfigureAwait(false);
            descriptorsById[snapshot.SnapshotId] = Descriptor(snapshot);
            await WriteIndexCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<SimulationRunSnapshot?> TryGetAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            return null;
        }

        string path = PathFor(snapshotId);
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await payloadCodec.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        SimulationRunSnapshot snapshot = SimulationRunSnapshotSerializer.Deserialize(json, validateHash: true);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureIndexCoreAsync(cancellationToken).ConfigureAwait(false);
            descriptorsById[snapshot.SnapshotId] = Descriptor(snapshot);
            await WriteIndexCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

        return snapshot;
    }

    public async ValueTask<IReadOnlyList<SimulationSnapshotDescriptor>> ListAsync(
        SimulationSnapshotDescriptorQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureIndexCoreAsync(cancellationToken).ConfigureAwait(false);
            return [.. InMemorySimulationSnapshotStore.Query(descriptorsById.Values, query).Select(Descriptor)];
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            return false;
        }

        string path = PathFor(snapshotId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureIndexCoreAsync(cancellationToken).ConfigureAwait(false);
            bool removed = descriptorsById.Remove(snapshotId);
            if (removed)
            {
                await WriteIndexCoreAsync(cancellationToken).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask EnsureIndexCoreAsync(CancellationToken cancellationToken)
    {
        if (indexLoaded)
        {
            return;
        }

        Directory.CreateDirectory(rootDirectory);
        if (File.Exists(IndexPath))
        {
            string json = await File.ReadAllTextAsync(IndexPath, cancellationToken).ConfigureAwait(false);
            DescriptorIndex index = JsonSerializer.Deserialize<DescriptorIndex>(json, JsonOptions) ?? new();
            descriptorsById.Clear();
            foreach (SimulationSnapshotDescriptor descriptor in index.Snapshots)
            {
                descriptorsById[descriptor.SnapshotId] = Descriptor(descriptor);
            }

            indexLoaded = true;
            return;
        }

        await RebuildIndexFromPayloadsCoreAsync(cancellationToken).ConfigureAwait(false);
        await WriteIndexCoreAsync(cancellationToken).ConfigureAwait(false);
        indexLoaded = true;
    }

    private async ValueTask RebuildIndexFromPayloadsCoreAsync(CancellationToken cancellationToken)
    {
        descriptorsById.Clear();
        foreach (string path in Directory.EnumerateFiles(rootDirectory, $"*{payloadCodec.FileExtension}", SearchOption.TopDirectoryOnly)
                     .Where(path => !string.Equals(Path.GetFileName(path), DescriptorIndexFileName, StringComparison.Ordinal)))
        {
            string json = await payloadCodec.ReadAsync(path, cancellationToken).ConfigureAwait(false);
            SimulationRunSnapshot snapshot = SimulationRunSnapshotSerializer.Deserialize(json, validateHash: true);
            descriptorsById[snapshot.SnapshotId] = Descriptor(snapshot);
        }
    }

    private async ValueTask WriteIndexCoreAsync(CancellationToken cancellationToken)
    {
        var index = new DescriptorIndex
        {
            Snapshots = [.. InMemorySimulationSnapshotStore.Query(descriptorsById.Values, null).Select(Descriptor)]
        };
        string json = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(IndexPath, json, cancellationToken).ConfigureAwait(false);
    }

    private string IndexPath => Path.Combine(rootDirectory, DescriptorIndexFileName);

    private string PathFor(string snapshotId)
    {
        string root = Path.GetFullPath(rootDirectory);
        string path = Path.GetFullPath(Path.Combine(root, $"{Sanitize(snapshotId)}{payloadCodec.FileExtension}"));
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) && !string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Snapshot path resolved outside the snapshot root directory.");
        }

        return path;
    }

    private static string Sanitize(string snapshotId)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new(snapshotId.Select(character => invalid.Contains(character) || character is '/' or '\\' ? '_' : character).ToArray());
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

    private sealed class DescriptorIndex
    {
        public string SchemaVersion { get; set; } = DescriptorIndexSchemaVersion;
        public List<SimulationSnapshotDescriptor> Snapshots { get; set; } = [];
    }
}
