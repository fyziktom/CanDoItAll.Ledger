using System.Text.Json.Serialization;

namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationScenarioDefinition
{
    public string ScenarioId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ScenarioKind { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "TOKEN";
    public DateTimeOffset StartsAtUtc { get; set; }
    public int StepCount { get; set; }
    public TimeSpan StepDuration { get; set; } = TimeSpan.FromDays(1);
    public int Seed { get; set; }
    public SimulationExperimentMode ExperimentMode { get; set; } = SimulationExperimentMode.Exploratory;
    public string ExpansionProfileId { get; set; } = string.Empty;
    public List<string> AllowedWarningCodes { get; set; } = [];
    public List<SimulationScenarioEntityDefinition> Entities { get; set; } = [];
    public List<SimulationScenarioPlaceDefinition> Places { get; set; } = [];
    public List<SimulationScenarioActorDefinition> Actors { get; set; } = [];
    public List<SimulationScenarioResourceDefinition> Resources { get; set; } = [];
    public List<SimulationScenarioLocationDefinition> Locations { get; set; } = [];
    public List<SimulationScenarioObjectDefinition> Objects { get; set; } = [];
    public List<SimulationScenarioStoreDefinition> Stores { get; set; } = [];
    public List<SimulationScenarioStoreDefinition> InitialStores { get; set; } = [];
    public List<SimulationScenarioBehaviorDefinition> Behaviors { get; set; } = [];
    public List<SimulationScenarioRelationshipDefinition> Relationships { get; set; } = [];
    public List<SimulationScenarioEventTemplate> EventTemplates { get; set; } = [];
    public List<SimulationScheduledEventDefinition> ScheduledEvents { get; set; } = [];
    public List<SimulationRuleRef> Rules { get; set; } = [];
    public List<SimulationScenarioRuleDefinition> RuleDefinitions { get; set; } = [];
    public List<SimulationScenarioExtensionDefinition> Extensions { get; set; } = [];
    [JsonIgnore]
    public SimulationScenarioExtensionDefinition BoundaryFunctionRegistry
    {
        get => this.GetOrAddBoundaryFunctionRegistry();
        set => this.WithBoundaryFunctionRegistry(value);
    }
    public SimulationScenarioSchedule Schedule { get; set; } = new();
    public List<SimulationScenarioBackendHint> BackendHints { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public static class SimulationScenarioDefinitionExtensionHelpers
{
    public static SimulationScenarioExtensionDefinition GetBoundaryFunctionRegistryOrEmpty(this SimulationScenarioDefinition definition)
        => definition.TryGetBoundaryFunctionRegistry() ?? SimulationScenarioExtensionDefinition.CreateBoundaryFunctionRegistry();

    public static SimulationScenarioExtensionDefinition? TryGetBoundaryFunctionRegistry(this SimulationScenarioDefinition definition)
        => definition.TryGetExtension(SimulationScenarioExtensionIds.BoundaryFunctionRegistry);

    public static SimulationScenarioExtensionDefinition GetOrAddBoundaryFunctionRegistry(this SimulationScenarioDefinition definition)
        => definition.GetOrAddExtension(
            SimulationScenarioExtensionIds.BoundaryFunctionRegistry,
            SimulationScenarioExtensionDefinition.CreateBoundaryFunctionRegistry);

    public static SimulationScenarioDefinition WithBoundaryFunctionRegistry(
        this SimulationScenarioDefinition definition,
        SimulationScenarioExtensionDefinition registry)
        => definition.WithExtension(registry);

    public static SimulationScenarioExtensionDefinition? TryGetExtension(
        this SimulationScenarioDefinition definition,
        string extensionId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionId);
        return definition.Extensions.FirstOrDefault(extension => IsExtension(extension, extensionId));
    }

    public static SimulationScenarioExtensionDefinition GetOrAddExtension(
        this SimulationScenarioDefinition definition,
        string extensionId,
        Func<SimulationScenarioExtensionDefinition> createExtension)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionId);
        ArgumentNullException.ThrowIfNull(createExtension);
        SimulationScenarioExtensionDefinition? existing = definition.TryGetExtension(extensionId);
        if (existing is not null)
        {
            return existing;
        }

        SimulationScenarioExtensionDefinition extension = createExtension();
        if (!IsExtension(extension, extensionId))
        {
            throw new InvalidOperationException($"scenario-extension-id-mismatch:{extensionId}:{extension.ExtensionId}");
        }

        definition.Extensions.Add(extension);
        return extension;
    }

    public static SimulationScenarioDefinition WithExtension(
        this SimulationScenarioDefinition definition,
        SimulationScenarioExtensionDefinition extension)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension.ExtensionId);
        definition.Extensions.RemoveAll(item => IsExtension(item, extension.ExtensionId));
        definition.Extensions.Add(extension);
        return definition;
    }

    private static bool IsExtension(SimulationScenarioExtensionDefinition extension, string extensionId)
        => string.Equals(extension.ExtensionId, extensionId, StringComparison.Ordinal);
}

public static class SimulationScenarioExtensionIds
{
    public const string BoundaryFunctionRegistry = "simulation.boundary-function-registry";
}

public static class SimulationScenarioExtensionSchemas
{
    public const string BoundaryFunctionRegistry = "can-do-it-all.simulation.boundary-function-registry";
    public const string BoundaryFunctionRegistryVersion = "1.0";
    public const string BoundaryFunctionRegistryHash = "sha256:58c984ae2d0e7eb9b7268baaed7d4eb451997e441219ad5519a387fb31f16afb";
}

public sealed class SimulationScenarioExtensionDefinition
{
    public string ExtensionId { get; set; } = SimulationScenarioExtensionIds.BoundaryFunctionRegistry;
    public string ExtensionKind { get; set; } = "boundary-function-registry";
    public string SchemaId { get; set; } = SimulationScenarioExtensionSchemas.BoundaryFunctionRegistry;
    public string SchemaVersion { get; set; } = SimulationScenarioExtensionSchemas.BoundaryFunctionRegistryVersion;
    public string SchemaHash { get; set; } = SimulationScenarioExtensionSchemas.BoundaryFunctionRegistryHash;
    public string RegistryId { get; set; } = "scenario-boundary-function-registry";
    public string Version { get; set; } = "1.0";
    public List<SimulationScenarioBoundaryFunctionDefinition> Functions { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];

    public static SimulationScenarioExtensionDefinition CreateBoundaryFunctionRegistry()
        => new();
}

public sealed class SimulationScenarioBoundaryFunctionDefinition
{
    public string FunctionId { get; set; } = string.Empty;
    public string FunctionKind { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public List<string> ResourceIds { get; set; } = [];
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public List<string> ActorIds { get; set; } = [];
    public List<string> LocationIds { get; set; } = [];
    public List<string> BoundaryIds { get; set; } = [];
    public string OwnerKind { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioEntityDefinition
{
    public string EntityId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string HomePlaceId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioActorDefinition
{
    public string ActorId { get; set; } = string.Empty;
    public string Kind { get; set; } = "actor";
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string HomeLocationId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioPlaceDefinition
{
    public string PlaceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Z { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioResourceDefinition
{
    public string ResourceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? Capacity { get; set; }
    public bool AllowsNegativeCapacity { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioLocationDefinition
{
    public string LocationId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Z { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioObjectDefinition
{
    public string ObjectId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioRuleDefinition
{
    public string RuleId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioSchedule
{
    public string ScheduleId { get; set; } = string.Empty;
    public TimeSpan StepDuration { get; set; } = TimeSpan.FromDays(1);
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioStoreDefinition
{
    public string StoreId { get; set; } = string.Empty;
    public string OwnerActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? Capacity { get; set; }
    public bool AllowsNegativeCapacity { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioBehaviorDefinition
{
    public string BehaviorId { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string TriggerEventKind { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioRelationshipDefinition
{
    public string RelationshipId { get; set; } = string.Empty;
    public string SourceActorId { get; set; } = string.Empty;
    public string TargetActorId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public decimal Strength { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioEventTemplate
{
    public string TemplateId { get; set; } = string.Empty;
    public string EventKind { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string TargetActorId { get; set; } = string.Empty;
    public string PlaceId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScheduledEventDefinition
{
    public string ScheduledEventId { get; set; } = string.Empty;
    public string EventKind { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public List<string> ActorIds { get; set; } = [];
    public List<string> ResourceIds { get; set; } = [];
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string SourceLocationId { get; set; } = string.Empty;
    public string TargetLocationId { get; set; } = string.Empty;
    public decimal Magnitude { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Offset { get; set; }
    public int Order { get; set; }
    public List<SimulationEventEffect> Effects { get; set; } = [];
    public List<SimulationTransactionLegDefinition> TransactionLegs { get; set; } = [];
    public List<SimulationStoreResolutionPolicyDefinition> StoreResolutionPolicies { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationStoreResolutionPolicyDefinition
{
    public string Role { get; set; } = string.Empty;
    public string Policy { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string PoolId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationScenarioBackendHint
{
    public string BackendId { get; set; } = string.Empty;
    public int Priority { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
