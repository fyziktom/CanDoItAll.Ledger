using CanDoItAll.Economy.Simulation.Abstractions;

namespace CanDoItAll.Economy.Simulation.Ledger;

public sealed class LedgerSimulationIssueMapper
{
    public List<SimulationIssue> MapIssues(IEnumerable<LedgerProjectionEvidence> evidence)
        => evidence
            .Where(item => string.Equals(item.Kind, "issue", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .Select(item => new SimulationIssue
            {
                IssueId = $"ledger.issue.{item.EvidenceId}",
                Severity = item.Severity,
                Category = "risk",
                Title = item.Summary,
                Description = item.Summary,
                ActorIds = string.IsNullOrWhiteSpace(item.ActorId) ? [] : [item.ActorId],
                ResourceIds = string.IsNullOrWhiteSpace(item.ResourceId) ? [] : [item.ResourceId]
            })
            .ToList();
}
