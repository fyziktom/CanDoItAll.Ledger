namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationMetricEvaluationResult
{
    public List<SimulationMetricValue> Metrics { get; set; } = [];
    public List<SimulationScenarioDefinitionValidationMessage> Diagnostics { get; set; } = [];
    public bool IsValid => Diagnostics.All(static message => !string.Equals(message.Severity, "error", StringComparison.OrdinalIgnoreCase));
}

public sealed class SimulationMetricValue
{
    public string MetricId { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationInvariantEvaluationResult
{
    public string InvariantId { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Severity { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationMetricEvaluationOptions
{
    public SimulationExperimentMode Mode { get; set; }
    public bool Strict => Mode is SimulationExperimentMode.Strict or SimulationExperimentMode.ResearchGrade;
}

public sealed class SimulationFrameMetricEvaluator
{
    public const string EvaluatorVersion = "simulation-metric-evaluator/v2";

    public SimulationMetricEvaluationResult Evaluate(SimulationFrame frame, IEnumerable<SimulationMetricDefinition> definitions)
        => Evaluate(frame, definitions, new SimulationMetricEvaluationOptions());

    public SimulationMetricEvaluationResult Evaluate(SimulationFrame frame, IEnumerable<SimulationMetricDefinition> definitions, SimulationMetricEvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(options);

        var result = new SimulationMetricEvaluationResult();
        foreach (SimulationMetricDefinition definition in definitions)
        {
            SimulationMetricValue? value = EvaluateMetric(frame, definition, options, result.Diagnostics);
            if (value is not null)
            {
                result.Metrics.Add(value);
            }
        }

        return result;
    }

    private static SimulationMetricValue? EvaluateMetric(
        SimulationFrame frame,
        SimulationMetricDefinition definition,
        SimulationMetricEvaluationOptions options,
        List<SimulationScenarioDefinitionValidationMessage> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(definition.MetricId))
        {
            Error(diagnostics, "missing-metric-id", "metrics", "Metric id is required.");
            return null;
        }

        string declaredKind = definition.Metadata.GetValueOrDefault("kind", string.Empty);
        string kind = string.IsNullOrWhiteSpace(declaredKind) && !options.Strict
            ? InferKind(definition.MetricId)
            : declaredKind;
        if (!SimulationMetricKindRegistry.IsKnown(kind))
        {
            string severity = options.Strict ? "error" : "warning";
            diagnostics.Add(new()
            {
                Severity = severity,
                Code = "unknown-metric-kind",
                Path = $"metrics.{definition.MetricId}.kind",
                Message = $"Unknown metric kind '{kind}'."
            });
            if (options.Strict)
            {
                return null;
            }
        }

        SimulationMetricKindDescriptor? descriptor = SimulationMetricKindRegistry.Find(kind);
        if (options.Strict && descriptor is not null)
        {
            bool missingRequiredMetadata = false;
            foreach (string requiredKey in descriptor.RequiredMetadataKeys.Where(requiredKey => string.IsNullOrWhiteSpace(definition.Metadata.GetValueOrDefault(requiredKey))))
            {
                Error(
                    diagnostics,
                    "missing-metric-metadata",
                    $"metrics.{definition.MetricId}.{requiredKey}",
                    $"Metric '{definition.MetricId}' kind '{kind}' requires metadata key '{requiredKey}'.");
                missingRequiredMetadata = true;
            }

            if (missingRequiredMetadata)
            {
                return null;
            }
        }

        string resourceId = definition.Metadata.GetValueOrDefault("resourceId", string.Empty);
        decimal value = kind switch
        {
            "resource-total" => frame.ResourceStores
                .Where(store => ResourceMatches(store, resourceId))
                .Sum(static store => store.Quantity),
            "resource-owner-hhi" => CalculateOwnerHhi(frame.ResourceStores.Where(store => ResourceMatches(store, resourceId))),
            "top-owner-share" => CalculateTopOwnerShare(frame.ResourceStores.Where(store => ResourceMatches(store, resourceId))),
            "issue-count" or "rule-violation-count" => CountIssues(frame, definition),
            "flow-count" => frame.Flows.Count,
            "average-access-cost" => AverageAccessCost(frame, definition),
            "stock-depletion-rate" => StockDepletionRate(frame, definition, resourceId),
            "relationship-aggregate" or "relationship-trust-aggregate" or "relationship-conflict-aggregate" => RelationshipAggregate(frame, definition),
            "admin-burden" or "admin-burden-time" or "admin-burden-cost" => AdminBurden(frame, definition),
            "transfer-volume" or "trade-volume" => TransferTradeVolume(frame, definition, resourceId),
            _ => 0
        };
        int precision = descriptor?.Precision ?? 6;
        value = decimal.Round(value, precision, MidpointRounding.AwayFromZero);
        string unit = FirstNonEmpty(definition.Unit, descriptor?.DefaultUnit, "unit");
        var metadata = new Dictionary<string, string>(definition.Metadata, StringComparer.Ordinal)
        {
            ["kind"] = kind,
            ["unit"] = unit,
            ["precision"] = precision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sourceFrameHash"] = SimulationDeterministicHash.HashFrame(frame),
            ["evaluatorVersion"] = EvaluatorVersion,
            ["metricRegistryVersion"] = SimulationMetricKindRegistry.RegistryVersion,
            ["registeredDescriptorId"] = descriptor?.Kind ?? string.Empty
        };

        return new()
        {
            MetricId = definition.MetricId,
            Value = value,
            Unit = unit,
            Metadata = metadata
        };
    }

    private static string InferKind(string metricId)
    {
        if (metricId.Contains("hhi", StringComparison.OrdinalIgnoreCase) ||
            metricId.Contains("concentration", StringComparison.OrdinalIgnoreCase))
        {
            return "resource-owner-hhi";
        }

        if (metricId.Contains("top", StringComparison.OrdinalIgnoreCase) &&
            metricId.Contains("share", StringComparison.OrdinalIgnoreCase))
        {
            return "top-owner-share";
        }

        if (metricId.Contains("violation", StringComparison.OrdinalIgnoreCase) ||
            metricId.Contains("issue", StringComparison.OrdinalIgnoreCase))
        {
            return "issue-count";
        }

        if (metricId.Contains("access", StringComparison.OrdinalIgnoreCase) &&
            metricId.Contains("cost", StringComparison.OrdinalIgnoreCase))
        {
            return "average-access-cost";
        }

        if (metricId.Contains("depletion", StringComparison.OrdinalIgnoreCase))
        {
            return "stock-depletion-rate";
        }

        if (metricId.Contains("trust", StringComparison.OrdinalIgnoreCase) ||
            metricId.Contains("conflict", StringComparison.OrdinalIgnoreCase))
        {
            return "relationship-aggregate";
        }

        if (metricId.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
            metricId.Contains("burden", StringComparison.OrdinalIgnoreCase))
        {
            return "admin-burden";
        }

        if (metricId.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
            metricId.Contains("trade", StringComparison.OrdinalIgnoreCase))
        {
            return "transfer-volume";
        }

        return "resource-total";
    }

    private static bool ResourceMatches(SimulationResourceStore store, string resourceId)
        => string.IsNullOrWhiteSpace(resourceId) || string.Equals(store.ResourceId, resourceId, StringComparison.Ordinal);

    private static decimal CalculateOwnerHhi(IEnumerable<SimulationResourceStore> stores)
    {
        decimal[] quantities = [.. stores
            .GroupBy(static store => store.OwnerActorId, StringComparer.Ordinal)
            .Select(static group => group.Sum(store => Math.Max(0, store.Quantity)))];
        decimal total = quantities.Sum();
        return total <= 0 ? 0 : quantities.Sum(quantity => (quantity / total) * (quantity / total));
    }

    private static decimal CalculateTopOwnerShare(IEnumerable<SimulationResourceStore> stores)
    {
        decimal[] quantities = [.. stores
            .GroupBy(static store => store.OwnerActorId, StringComparer.Ordinal)
            .Select(static group => group.Sum(store => Math.Max(0, store.Quantity)))];
        decimal total = quantities.Sum();
        return total <= 0 ? 0 : quantities.Max() / total;
    }

    private static decimal CountIssues(SimulationFrame frame, SimulationMetricDefinition definition)
    {
        string category = definition.Metadata.GetValueOrDefault("category", string.Empty);
        return frame.Issues.Count(issue => string.IsNullOrWhiteSpace(category) || string.Equals(issue.Category, category, StringComparison.Ordinal));
    }

    private static decimal AverageAccessCost(SimulationFrame frame, SimulationMetricDefinition definition)
    {
        string actorGroup = definition.Metadata.GetValueOrDefault("actorGroup", string.Empty);
        decimal[] values = [.. frame.Flows
            .Where(flow => string.IsNullOrWhiteSpace(actorGroup) || string.Equals(flow.Metadata.GetValueOrDefault("actorGroup"), actorGroup, StringComparison.Ordinal))
            .Select(flow => ReadDecimal(flow.Metadata, "accessCost"))
            .Where(static value => value is not null)
            .Select(static value => value!.Value)];
        if (values.Length > 0)
        {
            return values.Average();
        }

        values = [.. frame.Events
            .Where(simulationEvent => string.IsNullOrWhiteSpace(actorGroup) || string.Equals(simulationEvent.Metadata.GetValueOrDefault("actorGroup"), actorGroup, StringComparison.Ordinal))
            .Select(simulationEvent => ReadDecimal(simulationEvent.Metadata, "accessCost"))
            .Where(static value => value is not null)
            .Select(static value => value!.Value)];
        return values.Length == 0 ? 0 : values.Average();
    }

    private static decimal StockDepletionRate(SimulationFrame frame, SimulationMetricDefinition definition, string resourceId)
    {
        decimal current = frame.ResourceStores.Where(store => ResourceMatches(store, resourceId)).Sum(static store => store.Quantity);
        decimal? baseline = ReadDecimal(definition.Metadata, "baselineTotal") ?? ReadDecimal(definition.Metadata, "initialTotal");
        if (baseline is null or <= 0)
        {
            return frame.Flows
                .Where(flow => ResourceMatches(flow, resourceId) && string.IsNullOrWhiteSpace(flow.ToStoreId))
                .Sum(static flow => flow.Quantity);
        }

        return Math.Max(0, (baseline.Value - current) / baseline.Value);
    }

    private static decimal RelationshipAggregate(SimulationFrame frame, SimulationMetricDefinition definition)
    {
        string kind = definition.Metadata.GetValueOrDefault("relationshipKind", definition.Metadata.GetValueOrDefault("kindFilter", string.Empty));
        decimal[] values = [.. frame.Relationships
            .Where(relationship => string.IsNullOrWhiteSpace(kind) || string.Equals(relationship.Kind, kind, StringComparison.Ordinal))
            .Select(static relationship => relationship.Strength)];
        return Aggregate(values, definition.Metadata.GetValueOrDefault("aggregation", "average"));
    }

    private static decimal AdminBurden(SimulationFrame frame, SimulationMetricDefinition definition)
    {
        string unit = definition.Metadata.GetValueOrDefault("unit", definition.Unit);
        string metadataKey = unit.Contains("cost", StringComparison.OrdinalIgnoreCase) ? "adminCost" : "adminMinutes";
        decimal eventValue = frame.Events
            .Where(static simulationEvent => SimulationEventKindRegistry.Normalize(simulationEvent.EventKind) == SimulationEventKind.ActorAdminPerform ||
                                             string.Equals(simulationEvent.Metadata.GetValueOrDefault("category"), "admin", StringComparison.Ordinal))
            .Sum(simulationEvent => ReadDecimal(simulationEvent.Metadata, metadataKey) ?? (decimal)simulationEvent.Duration.TotalMinutes);
        decimal flowValue = frame.Flows.Sum(flow => ReadDecimal(flow.Metadata, metadataKey) ?? 0);
        return eventValue + flowValue;
    }

    private static decimal TransferTradeVolume(SimulationFrame frame, SimulationMetricDefinition definition, string resourceId)
    {
        string reason = definition.Metadata.GetValueOrDefault("reason", string.Empty);
        decimal flowValue = frame.Flows
            .Where(flow => ResourceMatches(flow, resourceId) &&
                           (string.IsNullOrWhiteSpace(reason) || flow.Reason.Contains(reason, StringComparison.OrdinalIgnoreCase)) &&
                           (flow.Reason.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
                            flow.Reason.Contains("trade", StringComparison.OrdinalIgnoreCase) ||
                            flow.Reason.Contains("fee", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(reason)))
            .Sum(static flow => flow.Quantity);
        decimal eventValue = frame.Events
            .Where(simulationEvent => ResourceMatches(simulationEvent, resourceId) &&
                                      (SimulationEventKindRegistry.Normalize(simulationEvent.EventKind) is SimulationEventKind.ActorResourceTransfer or SimulationEventKind.ActorTradeSell or SimulationEventKind.ActorTradeBuy))
            .Sum(static simulationEvent => Math.Abs(simulationEvent.Magnitude));
        return flowValue > 0 ? flowValue : eventValue;
    }

    private static bool ResourceMatches(SimulationResourceFlow flow, string resourceId)
        => string.IsNullOrWhiteSpace(resourceId) || string.Equals(flow.ResourceId, resourceId, StringComparison.Ordinal);

    private static bool ResourceMatches(SimulationEvent simulationEvent, string resourceId)
        => string.IsNullOrWhiteSpace(resourceId) || string.Equals(simulationEvent.ResourceId, resourceId, StringComparison.Ordinal) || simulationEvent.ResourceIds.Contains(resourceId, StringComparer.Ordinal);

    private static decimal Aggregate(decimal[] values, string aggregation)
        => values.Length == 0
            ? 0
            : aggregation.ToLowerInvariant() switch
            {
                "sum" => values.Sum(),
                "min" => values.Min(),
                "max" => values.Max(),
                _ => values.Average()
            };

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, string> metadata, string key)
        => decimal.TryParse(metadata.GetValueOrDefault(key), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal value)
            ? value
            : null;

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static void Error(List<SimulationScenarioDefinitionValidationMessage> diagnostics, string code, string path, string message)
        => diagnostics.Add(new()
        {
            Severity = "error",
            Code = code,
            Path = path,
            Message = message
        });
}
