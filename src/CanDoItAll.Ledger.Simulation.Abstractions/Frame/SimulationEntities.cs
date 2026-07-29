namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationActor
{
    public string ActorId { get; set; } = string.Empty;
    public string Kind { get; set; } = "actor";
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationResourceStore
{
    public string StoreId { get; set; } = string.Empty;
    public string OwnerActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceKind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? Capacity { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationResourceFlow
{
    public string FlowId { get; set; } = string.Empty;
    public string FromStoreId { get; set; } = string.Empty;
    public string ToStoreId { get; set; } = string.Empty;
    public string FromActorId { get; set; } = string.Empty;
    public string ToActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationRelationship
{
    public string RelationshipId { get; set; } = string.Empty;
    public string SourceActorId { get; set; } = string.Empty;
    public string TargetActorId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public decimal Strength { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationIssue
{
    public string IssueId { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public List<string> ActorIds { get; set; } = [];
    public List<string> ResourceIds { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}
