namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationFrameDelta
{
    public string DeltaId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public int FromStepIndex { get; set; }
    public int ToStepIndex { get; set; }
    public List<SimulationResourceStore> UpdatedResourceStores { get; set; } = [];
    public List<SimulationResourceFlow> AddedFlows { get; set; } = [];
    public List<SimulationRelationship> UpdatedRelationships { get; set; } = [];
    public List<SimulationEvent> AddedEvents { get; set; } = [];
    public List<SimulationIssue> AddedIssues { get; set; } = [];
    public List<SimulationArtifactRef> AddedArtifacts { get; set; } = [];
    public List<string> ResolvedIssueIds { get; set; } = [];
    public string DeterministicHash { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
