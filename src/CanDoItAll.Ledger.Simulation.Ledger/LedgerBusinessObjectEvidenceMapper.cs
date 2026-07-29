using CanDoItAll.Ledger.Simulation.Abstractions;

namespace CanDoItAll.Ledger.Simulation.Ledger;

public sealed class LedgerBusinessObjectEvidenceMapper
{
    public List<SimulationArtifactRef> MapArtifacts(IEnumerable<LedgerProjectionEvidence> evidence)
        => evidence
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .Select(item => new SimulationArtifactRef
            {
                ArtifactId = $"ledger.evidence.{item.EvidenceId}",
                Kind = item.Kind,
                Uri = $"ledger://evidence/{item.EvidenceId}",
                DisplayName = item.Summary,
                Metadata =
                {
                    ["actorId"] = item.ActorId,
                    ["resourceId"] = item.ResourceId
                }
            })
            .ToList();
}
