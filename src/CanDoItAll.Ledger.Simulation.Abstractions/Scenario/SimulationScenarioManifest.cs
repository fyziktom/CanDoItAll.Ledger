namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationScenarioManifest
{
    public string ScenarioId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ScenarioKind { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "USD";
    public int StepCount { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public TimeSpan StepDuration { get; set; } = TimeSpan.FromDays(1);
    public SimulationExperimentMode ExperimentMode { get; set; } = SimulationExperimentMode.Exploratory;
    public string ExpansionProfileId { get; set; } = string.Empty;
    public string ExpansionProfileVersion { get; set; } = string.Empty;
    public string ExpansionProfileHash { get; set; } = string.Empty;
    public List<SimulationRuleRef> Rules { get; set; } = [];
    public List<SimulationArtifactRef> Artifacts { get; set; } = [];
    public string DeterministicHash { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationRuleRef
{
    public string RuleId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationArtifactRef
{
    public string ArtifactId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
