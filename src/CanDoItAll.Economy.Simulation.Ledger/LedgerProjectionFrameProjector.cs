using CanDoItAll.Economy.Simulation.Abstractions;

namespace CanDoItAll.Economy.Simulation.Ledger;

public sealed class LedgerProjectionFrameProjector
{
    private readonly LedgerBusinessObjectEvidenceMapper evidenceMapper = new();
    private readonly LedgerSimulationIssueMapper issueMapper = new();
    private readonly LedgerSimulationEventProjector eventProjector = new();

    public SimulationFrame Project(LedgerSnapshotSimulationSource source, SimulationScenarioManifest manifest, SimulationRunIdentity run, int stepIndex)
    {
        List<SimulationActor> actors = source.ActorBalances
            .GroupBy(item => item.ActorId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SimulationActor
            {
                ActorId = group.Key,
                Kind = "ledger-actor",
                DisplayName = group.First().DisplayName,
                Role = "ledger-participant",
                Metadata =
                {
                    ["ledgerId"] = source.LedgerId
                }
            })
            .ToList();

        List<SimulationResourceStore> stores = source.ActorBalances
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ThenBy(item => item.ResourceId, StringComparer.Ordinal)
            .Select(item => new SimulationResourceStore
            {
                StoreId = $"ledger.store.{item.ActorId}.{item.ResourceId}",
                OwnerActorId = item.ActorId,
                ResourceId = item.ResourceId,
                ResourceKind = "ledger-balance",
                DisplayName = $"{item.DisplayName} {item.ResourceId}",
                Quantity = item.Quantity,
                Unit = item.Unit,
                Metadata =
                {
                    ["sequence"] = source.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            })
            .ToList();

        var frame = new SimulationFrame
        {
            FrameId = $"{run.RunId}.ledger.frame.{stepIndex}",
            Run = run,
            Clock = new() { StepIndex = stepIndex, SimulatedAtUtc = source.CapturedAtUtc, StepDuration = manifest.StepDuration },
            Actors = actors,
            ResourceStores = stores,
            Events = [.. eventProjector.ProjectEvents(source, stepIndex)],
            Issues = issueMapper.MapIssues(source.Evidence),
            Artifacts = evidenceMapper.MapArtifacts(source.Evidence),
            Metadata =
            {
                ["adapter"] = "ledger",
                ["ledgerId"] = source.LedgerId,
                ["ledgerSequence"] = source.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };
        frame.DeterministicHash = SimulationDeterministicHash.HashFrame(frame);
        return frame;
    }
}
