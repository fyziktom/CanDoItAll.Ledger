namespace CanDoItAll.Ledger.Simulation.Ledger;

public sealed class LedgerScenarioForkDescriptor
{
    public string ForkId { get; set; } = string.Empty;
    public string LedgerId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public long StartingSequence { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
