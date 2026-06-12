namespace CanDoItAll.Economy.Simulation.Abstractions;

public static class SimulationSnapshotJsonExport
{
    public static string Export(SimulationRunSnapshot snapshot)
        => SimulationRunSnapshotSerializer.Serialize(snapshot);

    public static SimulationRunSnapshot Import(string json, bool validateHash = true)
        => SimulationRunSnapshotSerializer.Deserialize(json, validateHash);
}
