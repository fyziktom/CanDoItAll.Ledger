namespace CanDoItAll.Economy.Simulation.Abstractions;

public static class SimulationMetricKindRegistry
{
    public const string RegistryVersion = "simulation-metric-kind-registry/v1";

    private static readonly Dictionary<string, SimulationMetricKindDescriptor> Descriptors = new(StringComparer.Ordinal)
    {
        ["resource-total"] = new("resource-total", "unit", 6, ["resourceId"]),
        ["resource-owner-hhi"] = new("resource-owner-hhi", "ratio", 6, ["resourceId"]),
        ["top-owner-share"] = new("top-owner-share", "ratio", 6, ["resourceId"]),
        ["issue-count"] = new("issue-count", "count", 0, []),
        ["rule-violation-count"] = new("rule-violation-count", "count", 0, []),
        ["flow-count"] = new("flow-count", "count", 0, []),
        ["average-access-cost"] = new("average-access-cost", "unit", 6, []),
        ["stock-depletion-rate"] = new("stock-depletion-rate", "ratio", 6, ["resourceId", "baselineTotal"]),
        ["relationship-aggregate"] = new("relationship-aggregate", "score", 6, ["relationshipKind"]),
        ["relationship-trust-aggregate"] = new("relationship-trust-aggregate", "score", 6, []),
        ["relationship-conflict-aggregate"] = new("relationship-conflict-aggregate", "score", 6, []),
        ["admin-burden"] = new("admin-burden", "minutes", 6, []),
        ["admin-burden-time"] = new("admin-burden-time", "minutes", 6, []),
        ["admin-burden-cost"] = new("admin-burden-cost", "unit", 6, []),
        ["transfer-volume"] = new("transfer-volume", "unit", 6, ["resourceId"]),
        ["trade-volume"] = new("trade-volume", "unit", 6, ["resourceId"])
    };

    public static bool IsKnown(string? kind)
        => !string.IsNullOrWhiteSpace(kind) && Descriptors.ContainsKey(kind);

    public static SimulationMetricKindDescriptor? Find(string? kind)
        => !string.IsNullOrWhiteSpace(kind) && Descriptors.TryGetValue(kind, out SimulationMetricKindDescriptor? descriptor)
            ? descriptor
            : null;
}

public sealed record SimulationMetricKindDescriptor(
    string Kind,
    string DefaultUnit,
    int Precision,
    IReadOnlyList<string> RequiredMetadataKeys);

public static class SimulationInvariantKindRegistry
{
    public const string RegistryVersion = "simulation-invariant-kind-registry/v1";

    private static readonly Dictionary<string, SimulationInvariantKindDescriptor> Descriptors = new(StringComparer.Ordinal)
    {
        ["metric-threshold"] = new("metric-threshold", ["metricId"], ["min", "max"]),
        ["no-store-below-zero"] = new("no-store-below-zero", [], []),
        ["max-resource-share"] = new("max-resource-share", ["resourceId"], ["maxShare", "max"]),
        ["resource-total-conserved"] = new("resource-total-conserved", ["resourceId", "expectedTotal"], []),
        ["total-conserved-resource"] = new("total-conserved-resource", ["resourceId", "expectedTotal"], []),
        ["known-frame-references"] = new("known-frame-references", [], []),
        ["known-event-participants"] = new("known-event-participants", [], []),
        ["visual-action-known-node"] = new("visual-action-known-node", [], [])
    };

    public static bool IsKnown(string? kind)
        => !string.IsNullOrWhiteSpace(kind) && Descriptors.ContainsKey(kind);

    public static SimulationInvariantKindDescriptor? Find(string? kind)
        => !string.IsNullOrWhiteSpace(kind) && Descriptors.TryGetValue(kind, out SimulationInvariantKindDescriptor? descriptor)
            ? descriptor
            : null;
}

public sealed record SimulationInvariantKindDescriptor(
    string Kind,
    IReadOnlyList<string> RequiredMetadataKeys,
    IReadOnlyList<string> RequireAnyMetadataKeys);

public sealed class SimulationMetricInvariantValidationOptions
{
    public SimulationExperimentMode Mode { get; set; }
    public bool Strict => Mode is SimulationExperimentMode.Strict or SimulationExperimentMode.ResearchGrade;
}

public sealed class SimulationMetricInvariantDefinitionValidator
{
    public SimulationScenarioDefinitionValidationResult Validate(
        IEnumerable<SimulationMetricDefinition> metrics,
        IEnumerable<SimulationExpectedInvariantDefinition> invariants,
        SimulationMetricInvariantValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(invariants);
        options ??= new();

        var result = new SimulationScenarioDefinitionValidationResult();
        Dictionary<string, SimulationMetricDefinition> metricsById = new(StringComparer.Ordinal);
        foreach (SimulationMetricDefinition metric in metrics)
        {
            if (string.IsNullOrWhiteSpace(metric.MetricId))
            {
                Add(result, "missing-metric-id", "metrics", "Metric id is required.");
                continue;
            }

            if (!metricsById.TryAdd(metric.MetricId, metric))
            {
                Add(result, "duplicate-metric-id", $"metrics.{metric.MetricId}", $"Duplicate metric id '{metric.MetricId}'.");
            }

            string kind = metric.Metadata.GetValueOrDefault("kind", string.Empty);
            if (options.Strict && !SimulationMetricKindRegistry.IsKnown(kind))
            {
                Add(result, "unknown-metric-kind", $"metrics.{metric.MetricId}.kind", $"Unknown metric kind '{kind}'.");
            }

            if (options.Strict && SimulationMetricKindRegistry.Find(kind) is { } descriptor)
            {
                foreach (string requiredKey in descriptor.RequiredMetadataKeys.Where(requiredKey => string.IsNullOrWhiteSpace(metric.Metadata.GetValueOrDefault(requiredKey))))
                {
                    Add(result, "missing-metric-metadata", $"metrics.{metric.MetricId}.{requiredKey}", $"Metric '{metric.MetricId}' kind '{kind}' requires metadata key '{requiredKey}'.");
                }
            }
        }

        foreach (SimulationExpectedInvariantDefinition invariant in invariants)
        {
            if (string.IsNullOrWhiteSpace(invariant.InvariantId))
            {
                Add(result, "missing-invariant-id", "invariants", "Invariant id is required.");
                continue;
            }

            string kind = invariant.Metadata.GetValueOrDefault("kind", "metric-threshold");
            if (options.Strict && !SimulationInvariantKindRegistry.IsKnown(kind))
            {
                Add(result, "unknown-invariant-kind", $"invariants.{invariant.InvariantId}.kind", $"Unknown invariant kind '{kind}'.");
            }

            if (options.Strict && SimulationInvariantKindRegistry.Find(kind) is { } descriptor)
            {
                foreach (string requiredKey in descriptor.RequiredMetadataKeys.Where(requiredKey => string.IsNullOrWhiteSpace(invariant.Metadata.GetValueOrDefault(requiredKey))))
                {
                    Add(result, "missing-invariant-metadata", $"invariants.{invariant.InvariantId}.{requiredKey}", $"Invariant '{invariant.InvariantId}' kind '{kind}' requires metadata key '{requiredKey}'.");
                }

                if (descriptor.RequireAnyMetadataKeys.Count > 0 &&
                    !descriptor.RequireAnyMetadataKeys.Any(key => !string.IsNullOrWhiteSpace(invariant.Metadata.GetValueOrDefault(key))))
                {
                    string joinedKeys = string.Join("|", descriptor.RequireAnyMetadataKeys);
                    Add(result, "missing-invariant-metadata", $"invariants.{invariant.InvariantId}.{joinedKeys}", $"Invariant '{invariant.InvariantId}' kind '{kind}' requires one of metadata keys '{joinedKeys}'.");
                }
            }

            if (string.Equals(kind, "metric-threshold", StringComparison.Ordinal) ||
                !invariant.Metadata.ContainsKey("kind"))
            {
                string metricId = invariant.Metadata.GetValueOrDefault("metricId", string.Empty);
                if (options.Strict && (string.IsNullOrWhiteSpace(metricId) || !metricsById.ContainsKey(metricId)))
                {
                    Add(result, "missing-invariant-metric", $"invariants.{invariant.InvariantId}.metricId", $"Invariant '{invariant.InvariantId}' references missing metric '{metricId}'.");
                }
            }
        }

        return result;
    }

    private static void Add(
        SimulationScenarioDefinitionValidationResult result,
        string code,
        string path,
        string message)
        => result.Messages.Add(new()
        {
            Severity = "error",
            Code = code,
            Path = path,
            Message = message
        });
}
