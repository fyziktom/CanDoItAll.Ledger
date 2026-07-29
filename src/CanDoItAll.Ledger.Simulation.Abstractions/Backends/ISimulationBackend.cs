namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationBackendCapabilities
{
    public string BackendId { get; set; } = string.Empty;
    public bool SupportsDeterministicReplay { get; set; } = true;
    public bool SupportsFrameDeltas { get; set; } = true;
    public bool SupportsLedgerForks { get; set; }
    public bool SupportsIssueProjection { get; set; } = true;
    public bool SupportsReverseProjection { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public interface ISimulationBackend
{
    string BackendId { get; }
    SimulationBackendCapabilities Capabilities { get; }
    ValueTask<SimulationScenarioManifest> GetManifestAsync(string scenarioId, CancellationToken cancellationToken = default);
    ValueTask<SimulationFrame> GetFrameAsync(string runId, int stepIndex, CancellationToken cancellationToken = default);
    ValueTask<SimulationFrameDelta> GetDeltaAsync(string runId, int fromStepIndex, int toStepIndex, CancellationToken cancellationToken = default);
}

public interface ISimulationFrameProjector
{
    ValueTask<SimulationFrame> ProjectFrameAsync(SimulationScenarioManifest manifest, SimulationStepIdentity step, CancellationToken cancellationToken = default);
}

public interface ISimulationRunFactory
{
    SimulationRunIdentity CreateRun(SimulationScenarioDefinition definition, string backendId);
}

public interface ISimulationRunFrameProvider
{
    ValueTask<SimulationFrame> GetInitialFrameAsync(SimulationScenarioDefinition definition, SimulationRunIdentity run, CancellationToken cancellationToken = default);
}

public interface ISimulationEventMaterializer
{
    IReadOnlyList<SimulationEvent> MaterializeEvents(SimulationScenarioDefinition definition, int stepIndex);
}
