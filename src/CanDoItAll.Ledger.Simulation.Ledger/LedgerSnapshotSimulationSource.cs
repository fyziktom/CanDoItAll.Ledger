namespace CanDoItAll.Ledger.Simulation.Ledger;

public sealed class LedgerSnapshotSimulationSource
{
    public string SourceId { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public List<LedgerProjectionActorBalance> ActorBalances { get; set; } = [];
    public List<LedgerProjectionEvidence> Evidence { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class LedgerProjectionActorBalance
{
    public string ActorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public sealed class LedgerProjectionEvidence
{
    public string EvidenceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
}
