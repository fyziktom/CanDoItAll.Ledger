namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationRunIdentity
{
    public string RunId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string BackendId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationStepIdentity
{
    public string RunId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public DateTimeOffset SimulatedAtUtc { get; set; }
}
