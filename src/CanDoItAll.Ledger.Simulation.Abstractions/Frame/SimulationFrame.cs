namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationFrame
{
    public string FrameId { get; set; } = string.Empty;
    public SimulationRunIdentity Run { get; set; } = new();
    public SimulationClockState Clock { get; set; } = new();
    public List<SimulationActor> Actors { get; set; } = [];
    public List<SimulationResourceStore> ResourceStores { get; set; } = [];
    public List<SimulationResourceFlow> Flows { get; set; } = [];
    public List<SimulationRelationship> Relationships { get; set; } = [];
    public List<SimulationEvent> Events { get; set; } = [];
    public List<SimulationRuleRef> Rules { get; set; } = [];
    public List<SimulationIssue> Issues { get; set; } = [];
    public List<SimulationArtifactRef> Artifacts { get; set; } = [];
    public string DeterministicHash { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
