using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Ledger.Simulation.Abstractions;

public static class SimulationRunSnapshotSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static string Serialize(SimulationRunSnapshot snapshot, bool refreshHash = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (refreshHash)
        {
            snapshot.RefreshDeterministicHash();
        }

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static SimulationRunSnapshot Deserialize(string json, bool validateHash = false)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Snapshot JSON is required.", nameof(json));
        }

        SimulationRunSnapshot snapshot = JsonSerializer.Deserialize<SimulationRunSnapshot>(json, JsonOptions)
            ?? throw new InvalidOperationException("Snapshot JSON did not contain a simulation snapshot.");

        if (validateHash)
        {
            string expected = SimulationRunSnapshotHasher.CalculateHash(snapshot);
            if (!string.Equals(snapshot.DeterministicHash, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Snapshot deterministic hash does not match its canonical content.");
            }
        }

        return snapshot;
    }
}
