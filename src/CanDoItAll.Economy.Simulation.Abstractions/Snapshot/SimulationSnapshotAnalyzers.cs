using System.Globalization;

namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationSnapshotAdminBurdenAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "admin-burden";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        decimal adminMinutes = snapshot.Metrics
            .Where(static metric => IsAdminMetric(metric))
            .Sum(static metric => metric.Value);
        List<string> actors = [.. snapshot.Frame.Events
            .Where(static item => IsAdminEvent(item))
            .SelectMany(EventActorIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)];
        if (adminMinutes <= 0 && actors.Count == 0)
        {
            return;
        }

        Add(report, new()
        {
            FindingId = $"{AnalyzerId}.visible",
            AnalyzerId = AnalyzerId,
            Code = "admin-burden",
            Severity = SimulationSnapshotFindingSeverity.Warning,
            Title = "Admin work is visible in the snapshot.",
            Explanation = $"{actors.Count} actor(s) are associated with admin work and the admin burden metric is {adminMinutes:0.###}.",
            SourcePaths = ["snapshot.frame.events", "snapshot.metrics"],
            Facts =
            {
                ["actorCount"] = actors.Count.ToString(CultureInfo.InvariantCulture),
                ["actorIds"] = string.Join(",", actors),
                ["adminMinutes"] = adminMinutes.ToString(CultureInfo.InvariantCulture)
            }
        });
    }

    private static bool IsAdminMetric(SimulationMetricValue metric)
        => string.Equals(metric.Metadata.GetValueOrDefault("kind"), "admin-burden", StringComparison.OrdinalIgnoreCase) ||
           metric.MetricId.Contains("admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdminEvent(SimulationEvent item)
        => string.Equals(SimulationEventKindRegistry.Normalize(item.EventKind), SimulationEventKind.ActorAdminPerform, StringComparison.Ordinal) ||
           item.EventKind.Contains("admin", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EventActorIds(SimulationEvent item)
        => item.ActorIds.Count > 0
            ? item.ActorIds
            : string.IsNullOrWhiteSpace(item.ActorId) ? [] : [item.ActorId];

    private static void Add(SimulationSnapshotAnalysisReport report, SimulationSnapshotAnalysisFinding finding)
        => report.Findings.Add(finding);
}

public sealed class SimulationSnapshotActiveIssueAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "active-issue";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        List<SimulationIssue> active = [.. snapshot.Frame.Issues.Where(static issue => !issue.IsResolved)];
        if (active.Count == 0)
        {
            return;
        }

        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.open",
            AnalyzerId = AnalyzerId,
            Code = "active-issues",
            Severity = active.Any(static issue => string.Equals(issue.Severity, "critical", StringComparison.OrdinalIgnoreCase))
                ? SimulationSnapshotFindingSeverity.Critical
                : SimulationSnapshotFindingSeverity.Warning,
            Title = "Active issues remain in the snapshot.",
            Explanation = $"{active.Count} unresolved issue(s) are present.",
            SourcePaths = ["snapshot.frame.issues"],
            Facts =
            {
                ["activeIssueCount"] = active.Count.ToString(CultureInfo.InvariantCulture),
                ["issueIds"] = string.Join(",", active.Select(static issue => issue.IssueId).OrderBy(static id => id, StringComparer.Ordinal))
            }
        });
    }
}

public sealed class SimulationSnapshotResourceConcentrationAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "resource-concentration";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        decimal topShare = ResolveTopShare(snapshot);
        if (topShare <= 0)
        {
            return;
        }

        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.top-holder",
            AnalyzerId = AnalyzerId,
            Code = "resource-concentration",
            Severity = topShare >= 0.5m ? SimulationSnapshotFindingSeverity.Warning : SimulationSnapshotFindingSeverity.Info,
            Title = "Resources are concentrated.",
            Explanation = $"The largest resource holder share is {topShare:0.###}.",
            SourcePaths = ["snapshot.frame.resourceStores", "snapshot.metrics"],
            Facts =
            {
                ["topHolderShare"] = topShare.ToString(CultureInfo.InvariantCulture)
            }
        });
    }

    private static decimal ResolveTopShare(SimulationRunSnapshot snapshot)
    {
        decimal metricShare = snapshot.Metrics
            .Where(static metric => string.Equals(metric.Metadata.GetValueOrDefault("kind"), "top-owner-share", StringComparison.OrdinalIgnoreCase))
            .Select(static metric => metric.Value)
            .DefaultIfEmpty(0)
            .Max();
        if (metricShare > 0)
        {
            return metricShare;
        }

        return snapshot.Frame.ResourceStores
            .GroupBy(static store => store.ResourceId, StringComparer.Ordinal)
            .Select(static group =>
            {
                decimal total = group.Sum(static store => Math.Abs(store.Quantity));
                return total == 0 ? 0 : group.Max(static store => Math.Abs(store.Quantity)) / total;
            })
            .DefaultIfEmpty(0)
            .Max();
    }
}

public sealed class SimulationSnapshotRelationshipStressAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "relationship-stress";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        List<SimulationRelationship> stressed = [.. snapshot.Frame.Relationships
            .Where(static relationship => relationship.Strength < 0 ||
                                          relationship.Kind.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
                                          relationship.Kind.Contains("stress", StringComparison.OrdinalIgnoreCase))];
        if (stressed.Count == 0)
        {
            return;
        }

        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.visible",
            AnalyzerId = AnalyzerId,
            Code = "relationship-stress",
            Severity = SimulationSnapshotFindingSeverity.Warning,
            Title = "Relationship stress is visible.",
            Explanation = $"{stressed.Count} stressed relationship(s) are present.",
            SourcePaths = ["snapshot.frame.relationships"],
            Facts =
            {
                ["relationshipCount"] = stressed.Count.ToString(CultureInfo.InvariantCulture),
                ["relationshipIds"] = string.Join(",", stressed.Select(static item => item.RelationshipId).OrderBy(static id => id, StringComparer.Ordinal))
            }
        });
    }
}

public sealed class SimulationSnapshotPendingEventAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "pending-event";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.queue",
            AnalyzerId = AnalyzerId,
            Code = "pending-events",
            Severity = SimulationSnapshotFindingSeverity.Info,
            Title = snapshot.PendingEvents.Count == 0 ? "No future events are pending." : "Future events are pending.",
            Explanation = $"{snapshot.PendingEvents.Count} event(s) are scheduled after this snapshot.",
            SourcePaths = ["snapshot.pendingEvents"],
            Facts =
            {
                ["pendingEventCount"] = snapshot.PendingEvents.Count.ToString(CultureInfo.InvariantCulture)
            }
        });
    }
}

public sealed class SimulationSnapshotVisualStagePressureAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "visual-stage-pressure";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        SimulationSnapshotVisualState? visual = snapshot.VisualState;
        if (visual is null)
        {
            return;
        }

        SimulationSnapshotRuntimeAttachment? runtime = visual.RuntimeAttachment;
        int runtimePressure = runtime is null
            ? 0
            : runtime.ActiveMotionIds.Count +
              runtime.QueuedMotionIds.Count +
              runtime.ActiveStageIds.Count +
              runtime.PendingStageIds.Count +
              runtime.ActiveBarrierBlockers.Count +
              runtime.CommandJournalTail.Count;
        int pressure = visual.ActiveActionIds.Count +
                       visual.PendingActionIds.Count +
                       visual.ActiveStageIds.Count +
                       visual.PendingStageIds.Count +
                       runtimePressure;
        if (pressure == 0)
        {
            return;
        }

        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.queue",
            AnalyzerId = AnalyzerId,
            Code = "visual-stage-pressure",
            Severity = pressure > 10 ? SimulationSnapshotFindingSeverity.Warning : SimulationSnapshotFindingSeverity.Info,
            Title = "Visual playback has active or pending work.",
            Explanation = $"{pressure} visual action, stage, motion, barrier, or command journal item(s) are active or pending.",
            SourcePaths =
            [
                "snapshot.visualState.activeActionIds",
                "snapshot.visualState.pendingActionIds",
                "snapshot.visualState.activeStageIds",
                "snapshot.visualState.pendingStageIds",
                "snapshot.visualState.runtimeAttachment"
            ],
            Facts =
            {
                ["stagePressureCount"] = pressure.ToString(CultureInfo.InvariantCulture),
                ["activeActionCount"] = visual.ActiveActionIds.Count.ToString(CultureInfo.InvariantCulture),
                ["pendingActionCount"] = visual.PendingActionIds.Count.ToString(CultureInfo.InvariantCulture),
                ["activeStageCount"] = visual.ActiveStageIds.Count.ToString(CultureInfo.InvariantCulture),
                ["pendingStageCount"] = visual.PendingStageIds.Count.ToString(CultureInfo.InvariantCulture),
                ["activeMotionCount"] = (runtime?.ActiveMotionIds.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                ["queuedMotionCount"] = (runtime?.QueuedMotionIds.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                ["barrierBlockerCount"] = (runtime?.ActiveBarrierBlockers.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                ["commandJournalTailCount"] = (runtime?.CommandJournalTail.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                ["activeBarrierPolicy"] = runtime?.ActiveBarrierPolicy ?? string.Empty,
                ["renderDiagnosticsSummary"] = runtime?.RenderDiagnosticsSummary ?? string.Empty
            }
        });
    }
}

public sealed class SimulationSnapshotInvariantSummaryAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "invariant-summary";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        int failed = snapshot.Invariants.Count(static invariant => !invariant.Passed);
        string severity = failed > 0 ? SimulationSnapshotFindingSeverity.Warning : SimulationSnapshotFindingSeverity.Info;
        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.status",
            AnalyzerId = AnalyzerId,
            Code = "invariant-summary",
            Severity = severity,
            Title = failed > 0 ? "Some invariants failed." : "All evaluated invariants passed.",
            Explanation = $"{failed} of {snapshot.Invariants.Count} invariant(s) failed.",
            SourcePaths = ["snapshot.invariants"],
            Facts =
            {
                ["failedInvariantCount"] = failed.ToString(CultureInfo.InvariantCulture),
                ["invariantCount"] = snapshot.Invariants.Count.ToString(CultureInfo.InvariantCulture)
            }
        });
    }
}
