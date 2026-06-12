using System.Globalization;

namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationSnapshotUnresolvedVisualMappingAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "unresolved-visual-mapping";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        SimulationSnapshotVisualState? visual = snapshot.VisualState;
        if (visual is null)
        {
            return;
        }

        List<KeyValuePair<string, string>> diagnostics = [.. CollectDiagnostics(visual)
            .Where(static item => IsUnresolvedOrFallback(item.Key, item.Value))
            .OrderBy(static item => item.Key, StringComparer.Ordinal)];
        if (diagnostics.Count == 0)
        {
            return;
        }

        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.diagnostics",
            AnalyzerId = AnalyzerId,
            Code = "unresolved-visual-mapping",
            Severity = SimulationSnapshotFindingSeverity.Warning,
            Title = "Visual mapping diagnostics require review.",
            Explanation = $"{diagnostics.Count} visual mapping diagnostic item(s) mention unresolved targets or fallback behavior.",
            SourcePaths = ["snapshot.visualState.runtimeDiagnostics", "snapshot.visualState.runtimeAttachment.diagnostics"],
            Facts =
            {
                ["diagnosticCount"] = diagnostics.Count.ToString(CultureInfo.InvariantCulture),
                ["diagnosticKeys"] = string.Join(",", diagnostics.Select(static item => item.Key)),
                ["fallbackDiagnosticCount"] = diagnostics.Count(static item => ContainsFallback(item.Key) || ContainsFallback(item.Value)).ToString(CultureInfo.InvariantCulture)
            }
        });
    }

    private static IEnumerable<KeyValuePair<string, string>> CollectDiagnostics(SimulationSnapshotVisualState visual)
    {
        foreach (KeyValuePair<string, string> diagnostic in visual.RuntimeDiagnostics)
        {
            yield return diagnostic;
        }

        if (visual.RuntimeAttachment is null)
        {
            yield break;
        }

        foreach (KeyValuePair<string, string> diagnostic in visual.RuntimeAttachment.Diagnostics)
        {
            yield return new($"runtimeAttachment.{diagnostic.Key}", diagnostic.Value);
        }

        foreach (KeyValuePair<string, string> metadata in visual.RuntimeAttachment.Metadata)
        {
            yield return new($"runtimeAttachment.metadata.{metadata.Key}", metadata.Value);
        }
    }

    private static bool IsUnresolvedOrFallback(string key, string value)
        => ContainsUnresolved(key) || ContainsUnresolved(value) || ContainsFallback(key) || ContainsFallback(value);

    private static bool ContainsUnresolved(string value)
        => value.Contains("unresolved", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsFallback(string value)
        => value.Contains("fallback", StringComparison.OrdinalIgnoreCase);
}
