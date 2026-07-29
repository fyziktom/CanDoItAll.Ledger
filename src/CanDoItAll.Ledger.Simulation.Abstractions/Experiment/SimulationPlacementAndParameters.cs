namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationPlacementDefinition
{
    public string SchemaVersion { get; set; } = "placement/v1";
    public string PlacementId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string CoordinateSystem { get; set; } = "cartesian-2d";
    public List<SimulationPlacedEntity> Entities { get; set; } = [];
    public List<SimulationTopologyEdge> Topology { get; set; } = [];
    public SimulationGeneratedInputProvenance? GeneratedBy { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationPlacedEntity
{
    public string RefKind { get; set; } = string.Empty;
    public string RefId { get; set; } = string.Empty;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Z { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationTopologyEdge
{
    public string EdgeId { get; set; } = string.Empty;
    public string SourceRefKind { get; set; } = string.Empty;
    public string SourceRefId { get; set; } = string.Empty;
    public string TargetRefKind { get; set; } = string.Empty;
    public string TargetRefId { get; set; } = string.Empty;
    public decimal? Distance { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationParameterSetDefinition
{
    public string SchemaVersion { get; set; } = "parameters/v1";
    public string ParameterSetId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public List<SimulationActorParameter> ActorParameters { get; set; } = [];
    public List<SimulationResourceParameter> ResourceParameters { get; set; } = [];
    public List<SimulationRuleParameter> RuleParameters { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationActorParameter
{
    public string ActorId { get; set; } = string.Empty;
    public List<SimulationActorResourceRequirement> ResourceRequirements { get; set; } = [];
    public decimal CarryCapacity { get; set; }
    public decimal TravelSpeed { get; set; }
    public decimal TravelCost { get; set; }
    public decimal StorageCapacity { get; set; }
    public decimal AdminTimeCost { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationActorResourceRequirement
{
    public string ResourceId { get; set; } = string.Empty;
    public decimal RequiredQuantity { get; set; }
    public string Period { get; set; } = "day";
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationResourceParameter
{
    public string ResourceId { get; set; } = string.Empty;
    public decimal StorageCapacity { get; set; }
    public decimal ExternalDemand { get; set; }
    public decimal ResaleMarkup { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationRuleParameter
{
    public string RuleId { get; set; } = string.Empty;
    public List<SimulationRuleResourceLimit> ResourceLimits { get; set; } = [];
    public decimal TaxRate { get; set; }
    public decimal FeeRate { get; set; }
    public decimal AdminMinutesPerTrade { get; set; }
    public decimal EnforcementThreshold { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationRuleResourceLimit
{
    public string ResourceId { get; set; } = string.Empty;
    public decimal MaxQuantity { get; set; }
    public string Period { get; set; } = "day";
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationGeneratedInputProvenance
{
    public string GeneratorId { get; set; } = string.Empty;
    public string GeneratorVersion { get; set; } = string.Empty;
    public int Seed { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string OutputHash { get; set; } = string.Empty;
}

public sealed class SimulationPlacementParameterValidator
{
    public SimulationScenarioDefinitionValidationResult ValidatePlacement(SimulationPlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var result = new SimulationScenarioDefinitionValidationResult();
        Require(!string.IsNullOrWhiteSpace(placement.PlacementId), "placement.id", "Placement id is required.", result);
        Require(!string.IsNullOrWhiteSpace(placement.ScenarioId), "placement.scenarioId", "Scenario id is required.", result);
        foreach (SimulationPlacedEntity entity in placement.Entities)
        {
            Require(!string.IsNullOrWhiteSpace(entity.RefKind), $"placement.entities.{entity.RefId}.refKind", "Reference kind is required.", result);
            Require(!string.IsNullOrWhiteSpace(entity.RefId), "placement.entities.refId", "Reference id is required.", result);
        }

        return result;
    }

    public SimulationScenarioDefinitionValidationResult ValidateParameters(SimulationParameterSetDefinition parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var result = new SimulationScenarioDefinitionValidationResult();
        Require(!string.IsNullOrWhiteSpace(parameters.ParameterSetId), "parameters.id", "Parameter set id is required.", result);
        Require(!string.IsNullOrWhiteSpace(parameters.ScenarioId), "parameters.scenarioId", "Scenario id is required.", result);
        foreach (SimulationActorParameter actor in parameters.ActorParameters)
        {
            Require(!string.IsNullOrWhiteSpace(actor.ActorId), "parameters.actor.actorId", "Actor id is required.", result);
            Require(actor.CarryCapacity >= 0, $"parameters.actor.{actor.ActorId}.carryCapacity", "Carry capacity must be non-negative.", result);
            Require(actor.TravelSpeed >= 0, $"parameters.actor.{actor.ActorId}.travelSpeed", "Travel speed must be non-negative.", result);
            foreach (SimulationActorResourceRequirement requirement in actor.ResourceRequirements)
            {
                Require(!string.IsNullOrWhiteSpace(requirement.ResourceId), $"parameters.actor.{actor.ActorId}.resourceRequirements.resourceId", "Resource requirement id is required.", result);
                Require(requirement.RequiredQuantity >= 0, $"parameters.actor.{actor.ActorId}.resourceRequirements.{requirement.ResourceId}.requiredQuantity", "Required quantity must be non-negative.", result);
            }
        }

        foreach (SimulationRuleParameter rule in parameters.RuleParameters)
        {
            Require(!string.IsNullOrWhiteSpace(rule.RuleId), "parameters.rule.ruleId", "Rule id is required.", result);
            Require(rule.TaxRate is >= 0 and <= 1, $"parameters.rule.{rule.RuleId}.taxRate", "Tax rate must be between 0 and 1.", result);
            Require(rule.FeeRate is >= 0 and <= 1, $"parameters.rule.{rule.RuleId}.feeRate", "Fee rate must be between 0 and 1.", result);
            foreach (SimulationRuleResourceLimit limit in rule.ResourceLimits)
            {
                Require(!string.IsNullOrWhiteSpace(limit.ResourceId), $"parameters.rule.{rule.RuleId}.resourceLimits.resourceId", "Rule resource limit id is required.", result);
                Require(limit.MaxQuantity >= 0, $"parameters.rule.{rule.RuleId}.resourceLimits.{limit.ResourceId}.maxQuantity", "Rule resource limit quantity must be non-negative.", result);
            }
        }

        return result;
    }

    public SimulationScenarioDefinition ApplyPlacementAndParameters(
        SimulationScenarioDefinition definition,
        SimulationPlacementDefinition placement,
        SimulationParameterSetDefinition parameters)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(parameters);

        var normalized = new SimulationScenarioDefinitionNormalizer().Normalize(definition).Definition;
        foreach (SimulationPlacedEntity placed in placement.Entities.Where(static entity => string.Equals(entity.RefKind, "location", StringComparison.OrdinalIgnoreCase)))
        {
            SimulationScenarioLocationDefinition? existing = normalized.Locations.FirstOrDefault(location => string.Equals(location.LocationId, placed.RefId, StringComparison.Ordinal));
            if (existing is null)
            {
                normalized.Locations.Add(new()
                {
                    LocationId = placed.RefId,
                    Kind = placed.Metadata.GetValueOrDefault("kind", "location"),
                    DisplayName = placed.Metadata.GetValueOrDefault("displayName", placed.RefId),
                    X = placed.X,
                    Y = placed.Y,
                    Z = placed.Z,
                    Metadata = new Dictionary<string, string>(placed.Metadata, StringComparer.Ordinal)
                });
            }
            else
            {
                existing.X = placed.X;
                existing.Y = placed.Y;
                existing.Z = placed.Z;
            }
        }

        foreach (SimulationActorParameter actorParameter in parameters.ActorParameters)
        {
            SimulationScenarioActorDefinition? actor = normalized.Actors.FirstOrDefault(item => string.Equals(item.ActorId, actorParameter.ActorId, StringComparison.Ordinal));
            if (actor is null)
            {
                continue;
            }

            actor.Metadata["carryCapacity"] = actorParameter.CarryCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            actor.Metadata["travelSpeed"] = actorParameter.TravelSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            actor.Metadata["storageCapacity"] = actorParameter.StorageCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            actor.Metadata["travelCost"] = actorParameter.TravelCost.ToString(System.Globalization.CultureInfo.InvariantCulture);
            actor.Metadata["adminTimeCost"] = actorParameter.AdminTimeCost.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (SimulationActorResourceRequirement requirement in actorParameter.ResourceRequirements)
            {
                actor.Metadata[$"resourceRequirement.{requirement.ResourceId}.quantity"] = requirement.RequiredQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
                actor.Metadata[$"resourceRequirement.{requirement.ResourceId}.period"] = requirement.Period;
            }
        }

        foreach (SimulationRuleParameter ruleParameter in parameters.RuleParameters)
        {
            SimulationScenarioRuleDefinition rule = normalized.RuleDefinitions.FirstOrDefault(item => string.Equals(item.RuleId, ruleParameter.RuleId, StringComparison.Ordinal))
                ?? new()
                {
                    RuleId = ruleParameter.RuleId,
                    DisplayName = ruleParameter.RuleId
                };
            if (!normalized.RuleDefinitions.Contains(rule))
            {
                normalized.RuleDefinitions.Add(rule);
            }

            SetRuleParameter(rule, "taxRate", ruleParameter.TaxRate);
            SetRuleParameter(rule, "feeRate", ruleParameter.FeeRate);
            SetRuleParameter(rule, "adminMinutesPerTrade", ruleParameter.AdminMinutesPerTrade);
            SetRuleParameter(rule, "enforcementThreshold", ruleParameter.EnforcementThreshold);
            foreach (SimulationRuleResourceLimit limit in ruleParameter.ResourceLimits)
            {
                rule.Parameters[$"resourceLimit.{limit.ResourceId}.maxQuantity"] = limit.MaxQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
                rule.Parameters[$"resourceLimit.{limit.ResourceId}.period"] = limit.Period;
            }
        }

        normalized.Metadata["placementHash"] = SimulationDeterministicHash.HashPlacement(placement);
        normalized.Metadata["parameterSetHash"] = SimulationDeterministicHash.HashParameterSet(parameters);
        return normalized;
    }

    private static void Require(bool condition, string path, string message, SimulationScenarioDefinitionValidationResult result)
    {
        if (!condition)
        {
            result.Messages.Add(new() { Code = "invalid-input", Path = path, Message = message });
        }
    }

    private static void SetRuleParameter(SimulationScenarioRuleDefinition rule, string key, decimal value)
    {
        if (value != 0)
        {
            rule.Parameters[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
