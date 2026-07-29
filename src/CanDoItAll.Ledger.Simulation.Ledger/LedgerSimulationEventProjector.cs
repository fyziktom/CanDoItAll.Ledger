using CanDoItAll.Ledger.Simulation.Abstractions;

namespace CanDoItAll.Ledger.Simulation.Ledger;

public sealed class LedgerSimulationEventProjector
{
    public IReadOnlyList<SimulationEvent> ProjectEvents(LedgerSnapshotSimulationSource source, int stepIndex)
    {
        ArgumentNullException.ThrowIfNull(source);

        return [.. source.Evidence
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .Select(item => ProjectEvent(item, source, stepIndex))];
    }

    private static SimulationEvent ProjectEvent(LedgerProjectionEvidence evidence, LedgerSnapshotSimulationSource source, int stepIndex)
    {
        string eventKind = evidence.Kind.ToLowerInvariant() switch
        {
            "transfer" or "payment" => SimulationEventKind.ResourceTransfer,
            "issue" or "warning" or "risk" => SimulationEventKind.IssueRaised,
            "rule" or "rule-execution" => SimulationEventKind.RuleApplied,
            "balance" or "store" or "store-change" => SimulationEventKind.StoreChanged,
            _ => SimulationEventKind.StoreChanged
        };

        return new()
        {
            EventId = $"ledger.event.{evidence.EvidenceId}",
            EventKind = eventKind,
            StepIndex = stepIndex,
            ActorId = evidence.ActorId,
            ResourceId = evidence.ResourceId,
            ActorIds = string.IsNullOrWhiteSpace(evidence.ActorId) ? [] : [evidence.ActorId],
            ResourceIds = string.IsNullOrWhiteSpace(evidence.ResourceId) ? [] : [evidence.ResourceId],
            SourceId = evidence.ActorId,
            TargetId = evidence.ResourceId,
            OccurredAtUtc = source.CapturedAtUtc,
            Duration = TimeSpan.Zero,
            Magnitude = 0,
            Timing = new() { StepIndex = stepIndex, Duration = TimeSpan.Zero },
            Metadata =
            {
                ["ledgerId"] = source.LedgerId,
                ["ledgerSequence"] = source.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["evidenceId"] = evidence.EvidenceId,
                ["evidenceKind"] = evidence.Kind,
                ["evidenceSummary"] = evidence.Summary,
                ["evidenceSeverity"] = evidence.Severity
            }
        };
    }
}
