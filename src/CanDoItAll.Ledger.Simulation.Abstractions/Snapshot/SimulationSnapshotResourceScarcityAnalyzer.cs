using System.Globalization;

namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationSnapshotResourceScarcityAnalyzer : ISimulationSnapshotAnalysisFacet
{
    public string AnalyzerId => "resource-scarcity";

    public void Analyze(SimulationRunSnapshot snapshot, SimulationSnapshotAnalysisReport report)
    {
        List<SimulationResourceStore> scarceStores = [.. snapshot.Frame.ResourceStores
            .Where(IsScarceStore)
            .OrderBy(static store => store.StoreId, StringComparer.Ordinal)];
        List<SimulationMetricValue> shortageMetrics = [.. snapshot.Metrics
            .Where(static metric => IsScarcityMetric(metric) && metric.Value > 0)
            .OrderBy(static metric => metric.MetricId, StringComparer.Ordinal)];
        if (scarceStores.Count == 0 && shortageMetrics.Count == 0)
        {
            return;
        }

        report.Findings.Add(new()
        {
            FindingId = $"{AnalyzerId}.visible",
            AnalyzerId = AnalyzerId,
            Code = "resource-scarcity",
            Severity = scarceStores.Count > 0 ? SimulationSnapshotFindingSeverity.Warning : SimulationSnapshotFindingSeverity.Info,
            Title = "Resource scarcity pressure is visible.",
            Explanation = $"{scarceStores.Count} store(s) are empty or below the low-stock threshold and {shortageMetrics.Count} shortage metric(s) are positive.",
            SourcePaths = ["snapshot.frame.resourceStores", "snapshot.metrics"],
            Facts =
            {
                ["scarceStoreCount"] = scarceStores.Count.ToString(CultureInfo.InvariantCulture),
                ["shortageMetricCount"] = shortageMetrics.Count.ToString(CultureInfo.InvariantCulture),
                ["storeIds"] = string.Join(",", scarceStores.Select(static store => store.StoreId)),
                ["metricIds"] = string.Join(",", shortageMetrics.Select(static metric => metric.MetricId)),
                ["lowestCapacityRatio"] = ResolveLowestCapacityRatio(snapshot).ToString(CultureInfo.InvariantCulture)
            }
        });
    }

    private static bool IsScarceStore(SimulationResourceStore store)
    {
        if (store.Quantity <= 0)
        {
            return true;
        }

        return store.Capacity is > 0 && store.Quantity / store.Capacity.Value <= 0.1m;
    }

    private static bool IsScarcityMetric(SimulationMetricValue metric)
        => string.Equals(metric.Metadata.GetValueOrDefault("kind"), "resource-scarcity", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(metric.Metadata.GetValueOrDefault("kind"), "shortage", StringComparison.OrdinalIgnoreCase) ||
           metric.MetricId.Contains("scarcity", StringComparison.OrdinalIgnoreCase) ||
           metric.MetricId.Contains("shortage", StringComparison.OrdinalIgnoreCase);

    private static decimal ResolveLowestCapacityRatio(SimulationRunSnapshot snapshot)
        => snapshot.Frame.ResourceStores
            .Where(static store => store.Capacity is > 0)
            .Select(static store => store.Quantity / store.Capacity!.Value)
            .DefaultIfEmpty(0)
            .Min();
}
