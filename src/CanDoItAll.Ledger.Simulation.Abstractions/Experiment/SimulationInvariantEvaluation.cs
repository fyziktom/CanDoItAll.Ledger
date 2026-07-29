namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationInvariantEvaluationOptions
{
    public SimulationExperimentMode Mode { get; set; }
    public bool Strict => Mode is SimulationExperimentMode.Strict or SimulationExperimentMode.ResearchGrade;
}

public sealed class SimulationInvariantEvaluator
{
    private readonly SimulationFrameMetricEvaluator metricEvaluator = new();

    public List<SimulationInvariantEvaluationResult> Evaluate(
        SimulationFrame frame,
        IEnumerable<SimulationExpectedInvariantDefinition> invariants,
        IEnumerable<SimulationMetricDefinition> metrics)
        => Evaluate(frame, invariants, metrics, new SimulationInvariantEvaluationOptions());

    public List<SimulationInvariantEvaluationResult> Evaluate(
        SimulationFrame frame,
        IEnumerable<SimulationExpectedInvariantDefinition> invariants,
        IEnumerable<SimulationMetricDefinition> metrics,
        SimulationInvariantEvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(invariants);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);

        SimulationMetricEvaluationResult metricResult = metricEvaluator
            .Evaluate(frame, metrics, new() { Mode = options.Mode });
        Dictionary<string, SimulationMetricValue> values = metricResult
            .Metrics
            .ToDictionary(static metric => metric.MetricId, StringComparer.Ordinal);
        List<SimulationInvariantEvaluationResult> results = [.. metricResult.Diagnostics
            .Where(static message => string.Equals(message.Severity, "error", StringComparison.OrdinalIgnoreCase))
            .Select(static message => new SimulationInvariantEvaluationResult
            {
                InvariantId = message.Path,
                Passed = false,
                Severity = "error",
                Message = message.Message,
                Metadata = { ["code"] = message.Code }
            })];
        results.AddRange(invariants.Select(invariant => EvaluateInvariant(frame, invariant, values, options)));
        return results;
    }

    private static SimulationInvariantEvaluationResult EvaluateInvariant(
        SimulationFrame frame,
        SimulationExpectedInvariantDefinition invariant,
        IReadOnlyDictionary<string, SimulationMetricValue> metricValues,
        SimulationInvariantEvaluationOptions options)
    {
        string kind = invariant.Metadata.GetValueOrDefault("kind", "metric-threshold");
        if (!SimulationInvariantKindRegistry.IsKnown(kind))
        {
            return new()
            {
                InvariantId = invariant.InvariantId,
                Passed = !options.Strict,
                Severity = options.Strict ? "error" : "warning",
                Message = $"Unknown invariant kind '{kind}'.",
                Metadata =
                {
                    ["code"] = "unknown-invariant-kind",
                    ["kind"] = kind
                }
            };
        }

        if (options.Strict && MissingInvariantSchemaMetadata(invariant, kind) is { } schemaFailure)
        {
            return schemaFailure;
        }

        return kind switch
        {
            "no-store-below-zero" => NoStoreBelowZero(frame, invariant),
            "max-resource-share" => MaxResourceShare(frame, invariant),
            "resource-total-conserved" or "total-conserved-resource" => ResourceTotalConserved(frame, invariant),
            "known-frame-references" => KnownFrameReferences(frame, invariant),
            "known-event-participants" => KnownEventParticipants(frame, invariant),
            "visual-action-known-node" => VisualActionKnownNode(frame, invariant),
            _ => MetricThreshold(invariant, metricValues, options)
        };
    }

    private static SimulationInvariantEvaluationResult? MissingInvariantSchemaMetadata(SimulationExpectedInvariantDefinition invariant, string kind)
    {
        if (SimulationInvariantKindRegistry.Find(kind) is not { } descriptor)
        {
            return null;
        }

        string? missingRequiredKey = descriptor.RequiredMetadataKeys
            .FirstOrDefault(requiredKey => string.IsNullOrWhiteSpace(invariant.Metadata.GetValueOrDefault(requiredKey)));
        if (missingRequiredKey is not null)
        {
            return SchemaFailure(
                invariant,
                "missing-invariant-metadata",
                $"Invariant '{invariant.InvariantId}' kind '{kind}' requires metadata key '{missingRequiredKey}'.",
                missingRequiredKey);
        }

        if (descriptor.RequireAnyMetadataKeys.Count > 0 &&
            !descriptor.RequireAnyMetadataKeys.Any(key => !string.IsNullOrWhiteSpace(invariant.Metadata.GetValueOrDefault(key))))
        {
            string joinedKeys = string.Join("|", descriptor.RequireAnyMetadataKeys);
            return SchemaFailure(
                invariant,
                "missing-invariant-metadata",
                $"Invariant '{invariant.InvariantId}' kind '{kind}' requires one of metadata keys '{joinedKeys}'.",
                joinedKeys);
        }

        return null;
    }

    private static SimulationInvariantEvaluationResult SchemaFailure(
        SimulationExpectedInvariantDefinition invariant,
        string code,
        string message,
        string missingKey)
        => new()
        {
            InvariantId = invariant.InvariantId,
            Passed = false,
            Severity = "error",
            Message = message,
            Metadata =
            {
                ["code"] = code,
                ["missingKey"] = missingKey
            }
        };

    private static SimulationInvariantEvaluationResult MetricThreshold(
        SimulationExpectedInvariantDefinition invariant,
        IReadOnlyDictionary<string, SimulationMetricValue> metricValues,
        SimulationInvariantEvaluationOptions options)
    {
        string metricId = invariant.Metadata.GetValueOrDefault("metricId", string.Empty);
        metricValues.TryGetValue(metricId, out SimulationMetricValue? metric);
        if ((string.IsNullOrWhiteSpace(metricId) || metric is null) && options.Strict)
        {
            return new()
            {
                InvariantId = invariant.InvariantId,
                Passed = false,
                Severity = "error",
                Message = $"Invariant '{invariant.InvariantId}' references missing metric '{metricId}'.",
                Metadata =
                {
                    ["code"] = "missing-invariant-metric",
                    ["metricId"] = metricId
                }
            };
        }

        decimal value = metric?.Value ?? 0;
        bool passed = true;
        if (TryDecimal(invariant.Metadata, "max", out decimal max))
        {
            passed = value <= max;
        }

        if (TryDecimal(invariant.Metadata, "min", out decimal min))
        {
            passed = passed && value >= min;
        }

        return new()
        {
            InvariantId = invariant.InvariantId,
            Passed = passed,
            Severity = invariant.Severity,
            Message = passed
                ? $"Invariant '{invariant.InvariantId}' passed."
                : $"Invariant '{invariant.InvariantId}' failed for metric '{metricId}' with value {value}.",
            Metadata =
            {
                ["metricId"] = metricId,
                ["value"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };
    }

    private static SimulationInvariantEvaluationResult NoStoreBelowZero(SimulationFrame frame, SimulationExpectedInvariantDefinition invariant)
    {
        bool allowNegative = string.Equals(invariant.Metadata.GetValueOrDefault("allowNegativeStores"), "true", StringComparison.OrdinalIgnoreCase);
        SimulationResourceStore[] negativeStores = [.. frame.ResourceStores.Where(static store => store.Quantity < 0)];
        return Result(invariant, allowNegative || negativeStores.Length == 0, negativeStores.Length, "negativeStoreCount");
    }

    private static SimulationInvariantEvaluationResult MaxResourceShare(SimulationFrame frame, SimulationExpectedInvariantDefinition invariant)
    {
        string resourceId = invariant.Metadata.GetValueOrDefault("resourceId", string.Empty);
        decimal[] quantities = [.. frame.ResourceStores
            .Where(store => string.IsNullOrWhiteSpace(resourceId) || string.Equals(store.ResourceId, resourceId, StringComparison.Ordinal))
            .GroupBy(static store => store.OwnerActorId, StringComparer.Ordinal)
            .Select(static group => group.Sum(store => Math.Max(0, store.Quantity)))];
        decimal total = quantities.Sum();
        decimal share = total <= 0 ? 0 : quantities.Max() / total;
        decimal max = ReadInvariantDecimal(invariant, "maxShare") ?? ReadInvariantDecimal(invariant, "max") ?? 1;
        return Result(invariant, share <= max, share, "topOwnerShare");
    }

    private static SimulationInvariantEvaluationResult ResourceTotalConserved(SimulationFrame frame, SimulationExpectedInvariantDefinition invariant)
    {
        string resourceId = invariant.Metadata.GetValueOrDefault("resourceId", string.Empty);
        decimal current = frame.ResourceStores
            .Where(store => string.IsNullOrWhiteSpace(resourceId) || string.Equals(store.ResourceId, resourceId, StringComparison.Ordinal))
            .Sum(static store => store.Quantity);
        decimal expected = ReadInvariantDecimal(invariant, "expectedTotal") ?? current;
        decimal tolerance = ReadInvariantDecimal(invariant, "tolerance") ?? 0;
        return Result(invariant, Math.Abs(current - expected) <= tolerance, current, "resourceTotal");
    }

    private static SimulationInvariantEvaluationResult KnownFrameReferences(SimulationFrame frame, SimulationExpectedInvariantDefinition invariant)
    {
        HashSet<string> actors = [.. frame.Actors.Select(static actor => actor.ActorId)];
        HashSet<string> resources = [.. frame.ResourceStores.Select(static store => store.ResourceId)];
        HashSet<string> stores = [.. frame.ResourceStores.Select(static store => store.StoreId)];
        int missing = frame.Flows.Count(flow =>
            (!string.IsNullOrWhiteSpace(flow.FromActorId) && !actors.Contains(flow.FromActorId)) ||
            (!string.IsNullOrWhiteSpace(flow.ToActorId) && !actors.Contains(flow.ToActorId)) ||
            (!string.IsNullOrWhiteSpace(flow.ResourceId) && !resources.Contains(flow.ResourceId)) ||
            (!string.IsNullOrWhiteSpace(flow.FromStoreId) && !stores.Contains(flow.FromStoreId)) ||
            (!string.IsNullOrWhiteSpace(flow.ToStoreId) && !stores.Contains(flow.ToStoreId)));
        return Result(invariant, missing == 0, missing, "missingReferenceCount");
    }

    private static SimulationInvariantEvaluationResult KnownEventParticipants(SimulationFrame frame, SimulationExpectedInvariantDefinition invariant)
    {
        HashSet<string> actors = [.. frame.Actors.Select(static actor => actor.ActorId)];
        HashSet<string> resources = [.. frame.ResourceStores.Select(static store => store.ResourceId)];
        int missing = frame.Events.Count(simulationEvent =>
            simulationEvent.ActorIds.Any(actorId => !actors.Contains(actorId)) ||
            simulationEvent.ResourceIds.Any(resourceId => !resources.Contains(resourceId)));
        return Result(invariant, missing == 0, missing, "missingParticipantCount");
    }

    private static SimulationInvariantEvaluationResult VisualActionKnownNode(SimulationFrame frame, SimulationExpectedInvariantDefinition invariant)
    {
        int missing = int.TryParse(frame.Metadata.GetValueOrDefault("visualActionMissingNodeCount"), out int value) ? value : 0;
        return Result(invariant, missing == 0, missing, "missingVisualNodeCount");
    }

    private static SimulationInvariantEvaluationResult Result(SimulationExpectedInvariantDefinition invariant, bool passed, decimal value, string valueKey)
        => new()
        {
            InvariantId = invariant.InvariantId,
            Passed = passed,
            Severity = invariant.Severity,
            Message = passed ? $"Invariant '{invariant.InvariantId}' passed." : $"Invariant '{invariant.InvariantId}' failed.",
            Metadata =
            {
                [valueKey] = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };

    private static bool TryDecimal(IReadOnlyDictionary<string, string> values, string key, out decimal result)
        => decimal.TryParse(values.GetValueOrDefault(key), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);

    private static decimal? ReadInvariantDecimal(SimulationExpectedInvariantDefinition invariant, string key)
        => decimal.TryParse(invariant.Metadata.GetValueOrDefault(key), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal value)
            ? value
            : null;
}
