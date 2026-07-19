using CanDoItAll.Economy.Simulator;

namespace CanDoItAll.Economy.Simulator.Components.Models;

public sealed record SimulationRunRegistryRevision(
    string RunId,
    SimulationRunLifecycleStatus Status,
    int CurrentStep,
    int SelectedStep,
    int AvailableStep,
    string OutputHash,
    int WarningCount,
    int ErrorCount,
    DateTimeOffset LastUpdatedAtUtc)
{
    public static IReadOnlyList<SimulationRunRegistryRevision> Capture(
        IEnumerable<SimulationRunRegistryRecord> runs)
        => runs
            .OrderBy(run => run.RunId, StringComparer.Ordinal)
            .Select(run => new SimulationRunRegistryRevision(
                run.RunId,
                run.Status,
                run.CurrentStep,
                run.SelectedStep,
                run.AvailableStep,
                run.OutputHash,
                run.WarningCount,
                run.ErrorCount,
                run.LastUpdatedAtUtc))
            .ToArray();
}
