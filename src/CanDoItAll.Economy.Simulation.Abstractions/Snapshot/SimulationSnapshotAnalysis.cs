using System.Globalization;

namespace CanDoItAll.Economy.Simulation.Abstractions;

public interface ISimulationSnapshotAnalysisService
{
    SimulationSnapshotAnalysisReport Analyze(SimulationRunSnapshot snapshot, string question = "");
}

public interface ISimulationSnapshotAnalyzer
{
    string AnalyzerId { get; }
    void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report);
}

public interface ISimulationSnapshotAnalysisFacet : ISimulationSnapshotAnalyzer
{
}

public sealed class SimulationSnapshotAnalysisReport
{
    public string SnapshotId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public string DataHash { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<SimulationSnapshotAnalysisFinding> Findings { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationSnapshotAnalysisFinding
{
    public string FindingId { get; set; } = string.Empty;
    public string AnalyzerId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = SimulationSnapshotFindingSeverity.Info;
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public List<string> SourcePaths { get; set; } = [];
    public Dictionary<string, string> Facts { get; set; } = [];
}

public static class SimulationSnapshotFindingSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public sealed class SimulationSnapshotAnalysisService(IEnumerable<ISimulationSnapshotAnalyzer>? analyzers = null) : ISimulationSnapshotAnalysisService
{
    private readonly IReadOnlyList<ISimulationSnapshotAnalyzer> analyzers =
        analyzers?.ToArray() ??
        [
            new SimulationSnapshotAdminBurdenAnalyzer(),
            new SimulationSnapshotActiveIssueAnalyzer(),
            new SimulationSnapshotResourceConcentrationAnalyzer(),
            new SimulationSnapshotResourceScarcityAnalyzer(),
            new SimulationSnapshotRelationshipStressAnalyzer(),
            new SimulationSnapshotPendingEventAnalyzer(),
            new SimulationSnapshotVisualStagePressureAnalyzer(),
            new SimulationSnapshotUnresolvedVisualMappingAnalyzer(),
            new SimulationSnapshotInvariantSummaryAnalyzer()
        ];

    public SimulationSnapshotAnalysisReport Analyze(SimulationRunSnapshot snapshot, string question = "")
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var report = new SimulationSnapshotAnalysisReport
        {
            SnapshotId = snapshot.SnapshotId,
            RunId = snapshot.RunId,
            StepIndex = snapshot.StepIndex,
            DataHash = snapshot.ProvenanceHashes.GetValueOrDefault("snapshot.data", string.Empty),
            Metadata =
            {
                ["question"] = question,
                ["analyzerCount"] = analyzers.Count.ToString(CultureInfo.InvariantCulture)
            }
        };

        foreach (ISimulationSnapshotAnalyzer analyzer in analyzers)
        {
            analyzer.Analyze(snapshot, report);
        }

        report.Summary = BuildSummary(report, question);
        return report;
    }

    private static string BuildSummary(SimulationSnapshotAnalysisReport report, string question)
    {
        int adminActors = IntFact(report, "admin-burden", "actorCount");
        decimal adminMinutes = DecimalFact(report, "admin-burden", "adminMinutes");
        int activeIssues = IntFact(report, "active-issues", "activeIssueCount");
        int pendingEvents = IntFact(report, "pending-events", "pendingEventCount");
        int visualStages = IntFact(report, "visual-stage-pressure", "stagePressureCount");
        int failedInvariants = IntFact(report, "invariant-summary", "failedInvariantCount");

        if (question.Contains("paperwork", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
            adminActors > 0 ||
            adminMinutes > 0)
        {
            return $"The snapshot shows paperwork pressure because {adminActors} actor(s) are performing admin work, admin burden is {adminMinutes:0.###}, {activeIssues} active issue(s) remain, {pendingEvents} event(s) are pending, {visualStages} visual stage/action item(s) are queued or active, and {failedInvariants} invariant(s) failed.";
        }

        return $"The snapshot has {report.Findings.Count} finding(s): {string.Join("; ", report.Findings.Take(3).Select(static finding => finding.Title))}.";
    }

    private static int IntFact(SimulationSnapshotAnalysisReport report, string code, string key)
        => int.TryParse(FindFact(report, code, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

    private static decimal DecimalFact(SimulationSnapshotAnalysisReport report, string code, string key)
        => decimal.TryParse(FindFact(report, code, key), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : 0;

    private static string FindFact(SimulationSnapshotAnalysisReport report, string code, string key)
        => report.Findings.FirstOrDefault(finding => string.Equals(finding.Code, code, StringComparison.Ordinal))?.Facts.GetValueOrDefault(key) ?? string.Empty;
}
