using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationScenarioDefinitionValidationResult
{
    public List<SimulationScenarioDefinitionValidationMessage> Messages { get; set; } = [];
    public bool IsValid => Messages.All(message => !string.Equals(message.Severity, "error", StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<SimulationScenarioDefinitionValidationMessage> Errors => [.. Messages.Where(message => string.Equals(message.Severity, "error", StringComparison.OrdinalIgnoreCase))];
}

public sealed class SimulationScenarioDefinitionValidationMessage
{
    public string Severity { get; set; } = "error";
    public string Code { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public interface ISimulationScenarioDefinitionStore
{
    ValueTask<IReadOnlyList<SimulationScenarioDefinition>> ListAsync(CancellationToken cancellationToken = default);
    ValueTask<SimulationScenarioDefinition?> GetAsync(string scenarioId, CancellationToken cancellationToken = default);
    ValueTask SaveAsync(SimulationScenarioDefinition definition, CancellationToken cancellationToken = default);
}

public interface ISimulationScenarioDefinitionSerializer
{
    string Serialize(SimulationScenarioDefinition definition);
    SimulationScenarioDefinition Deserialize(string json);
}

public interface ISimulationScenarioLoader
{
    SimulationScenarioDefinition LoadJson(string json);
    SimulationScenarioDefinition LoadFile(string path);
}

public interface ISimulationScenarioDefinitionValidator
{
    SimulationScenarioDefinitionValidationResult Validate(SimulationScenarioDefinition definition);
}

public sealed class SimulationScenarioLoader : ISimulationScenarioLoader
{
    private readonly ISimulationScenarioDefinitionSerializer serializer;
    private readonly ISimulationScenarioDefinitionValidator validator;

    public SimulationScenarioLoader()
        : this(new SimulationScenarioDefinitionSerializer(), new SimulationScenarioDefinitionValidator())
    {
    }

    public SimulationScenarioLoader(ISimulationScenarioDefinitionSerializer serializer, ISimulationScenarioDefinitionValidator validator)
    {
        this.serializer = serializer;
        this.validator = validator;
    }

    public SimulationScenarioDefinition LoadJson(string json)
    {
        SimulationScenarioDefinition definition = serializer.Deserialize(json);
        SimulationScenarioDefinitionValidationResult result = validator.Validate(definition);
        if (!result.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Path}: {error.Message}")));
        }

        return definition;
    }

    public SimulationScenarioDefinition LoadFile(string path)
        => LoadJson(File.ReadAllText(path));
}

public sealed class SimulationScenarioDefinitionSerializer : ISimulationScenarioDefinitionSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public string Serialize(SimulationScenarioDefinition definition)
        => JsonSerializer.Serialize(definition, JsonOptions);

    public SimulationScenarioDefinition Deserialize(string json)
        => JsonSerializer.Deserialize<SimulationScenarioDefinition>(json, JsonOptions)
            ?? throw new JsonException("Scenario definition JSON did not contain a document.");
}

public sealed class InMemorySimulationScenarioDefinitionStore : ISimulationScenarioDefinitionStore
{
    private readonly Dictionary<string, SimulationScenarioDefinition> definitions = new(StringComparer.Ordinal);

    public ValueTask<IReadOnlyList<SimulationScenarioDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<SimulationScenarioDefinition>>([.. definitions.Values.OrderBy(item => item.ScenarioId, StringComparer.Ordinal)]);
    }

    public ValueTask<SimulationScenarioDefinition?> GetAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        definitions.TryGetValue(scenarioId, out SimulationScenarioDefinition? definition);
        return ValueTask.FromResult(definition);
    }

    public ValueTask SaveAsync(SimulationScenarioDefinition definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        definitions[definition.ScenarioId] = definition;
        return ValueTask.CompletedTask;
    }
}

public sealed class SimulationScenarioDefinitionValidator : ISimulationScenarioDefinitionValidator
{
    private readonly SimulationScenarioDefinitionNormalizer normalizer = new();

    public SimulationScenarioDefinitionValidationResult Validate(SimulationScenarioDefinition definition)
    {
        var result = new SimulationScenarioDefinitionValidationResult();
        SimulationScenarioDefinitionNormalizationResult normalized = normalizer.Normalize(definition);
        result.Messages.AddRange(normalized.Messages);
        definition = normalized.Definition;
        Require(!string.IsNullOrWhiteSpace(definition.ScenarioId), "scenario.id", "Scenario id is required.", result);
        Require(definition.StepCount > 0, "scenario.stepCount", "Step count must be positive.", result);
        if (definition.ExperimentMode is SimulationExperimentMode.Strict or SimulationExperimentMode.ResearchGrade)
        {
            Require(!string.IsNullOrWhiteSpace(definition.ExpansionProfileId), "scenario.expansionProfileId", "Strict mode requires an explicit behavior expansion profile.", result);
        }

        if (!string.IsNullOrWhiteSpace(definition.ExpansionProfileId))
        {
            Require(SimulationBehaviorExpansionProfiles.IsKnown(definition.ExpansionProfileId), "scenario.expansionProfileId", $"Unknown behavior expansion profile '{definition.ExpansionProfileId}'.", result);
        }

        CheckDuplicates(definition.Entities.Select(item => item.EntityId), "entities", result);
        CheckDuplicates(definition.Places.Select(item => item.PlaceId), "places", result);
        CheckDuplicates(definition.Actors.Select(item => item.ActorId), "actors", result);
        CheckDuplicates(definition.Resources.Select(item => item.ResourceId), "resources", result);
        CheckDuplicates(definition.Locations.Select(item => item.LocationId), "locations", result);
        CheckDuplicates(definition.Objects.Select(item => item.ObjectId), "objects", result);
        CheckDuplicates(definition.Stores.Select(item => item.StoreId), "stores", result);
        CheckDuplicates(definition.InitialStores.Select(item => item.StoreId), "initialStores", result);
        CheckDuplicates(definition.Behaviors.Select(item => item.BehaviorId), "behaviors", result);
        CheckDuplicates(definition.Relationships.Select(item => item.RelationshipId), "relationships", result);
        CheckDuplicates(definition.EventTemplates.Select(item => item.TemplateId), "eventTemplates", result);
        CheckDuplicates(definition.ScheduledEvents.Select(item => item.ScheduledEventId), "scheduledEvents", result);
        CheckDuplicates(definition.RuleDefinitions.Select(item => item.RuleId), "ruleDefinitions", result);
        CheckDuplicates(definition.Extensions.Select(item => item.ExtensionId), "extensions", result);
        SimulationScenarioExtensionDefinition? boundaryFunctionRegistry = definition.TryGetBoundaryFunctionRegistry();
        if (boundaryFunctionRegistry is not null)
        {
            CheckDuplicates(boundaryFunctionRegistry.Functions.Select(item => item.FunctionId), "extensions.boundaryFunctionRegistry.functions", result);
        }

        HashSet<string> actors = [.. definition.Actors.Select(item => item.ActorId)];
        HashSet<string> resources = [.. definition.Resources.Select(item => item.ResourceId)];
        HashSet<string> locations = [.. definition.Locations.Select(item => item.LocationId)];
        HashSet<string> entities = [.. definition.Entities.Select(item => item.EntityId)];
        HashSet<string> places = [.. definition.Places.Select(item => item.PlaceId)];
        HashSet<string> stores = [.. definition.InitialStores.Select(item => item.StoreId)];
        HashSet<string> objects = [.. definition.Objects.Select(item => item.ObjectId)];
        HashSet<string> rules = [.. definition.Rules.Select(item => item.RuleId).Concat(definition.RuleDefinitions.Select(item => item.RuleId))];
        HashSet<string> anyReference = [.. actors.Concat(resources).Concat(locations).Concat(stores).Concat(entities).Concat(places).Concat(objects).Concat(rules)];

        foreach (SimulationScenarioActorDefinition actor in definition.Actors)
        {
            RequireReference(actor.HomeLocationId, locations, $"actors.{actor.ActorId}.homeLocationId", result);
        }

        foreach (SimulationScenarioEntityDefinition entity in definition.Entities)
        {
            RequireReference(entity.HomePlaceId, places, $"entities.{entity.EntityId}.homePlaceId", result);
        }

        foreach (SimulationScenarioResourceDefinition resource in definition.Resources)
        {
            Require(resource.Capacity is null or >= 0 || resource.AllowsNegativeCapacity, $"resources.{resource.ResourceId}.capacity", "Negative capacity is not allowed for this resource.", result);
        }

        foreach (SimulationScenarioStoreDefinition store in definition.InitialStores)
        {
            RequireReference(store.OwnerActorId, actors, $"initialStores.{store.StoreId}.ownerActorId", result);
            RequireReference(store.ResourceId, resources, $"initialStores.{store.StoreId}.resourceId", result);
            RequireReference(store.LocationId, locations, $"initialStores.{store.StoreId}.locationId", result);
            Require(store.Capacity is null or >= 0 || store.AllowsNegativeCapacity, $"initialStores.{store.StoreId}.capacity", "Negative capacity is not allowed for this store.", result);
        }

        foreach (SimulationScenarioStoreDefinition store in definition.Stores)
        {
            RequireReference(store.OwnerActorId, anyReference, $"stores.{store.StoreId}.ownerActorId", result);
            RequireReference(store.ResourceId, resources, $"stores.{store.StoreId}.resourceId", result);
            RequireReference(store.LocationId, anyReference, $"stores.{store.StoreId}.locationId", result);
            Require(store.Capacity is null or >= 0 || store.AllowsNegativeCapacity, $"stores.{store.StoreId}.capacity", "Negative capacity is not allowed for this store.", result);
        }

        foreach (SimulationScenarioObjectDefinition item in definition.Objects)
        {
            RequireReference(item.LocationId, locations, $"objects.{item.ObjectId}.locationId", result);
        }

        foreach (SimulationScenarioRelationshipDefinition relationship in definition.Relationships)
        {
            RequireReference(relationship.SourceActorId, actors, $"relationships.{relationship.RelationshipId}.sourceActorId", result);
            RequireReference(relationship.TargetActorId, actors, $"relationships.{relationship.RelationshipId}.targetActorId", result);
        }

        foreach (SimulationScheduledEventDefinition scheduledEvent in definition.ScheduledEvents)
        {
            string canonicalEventKind = SimulationEventKindRegistry.Normalize(scheduledEvent.EventKind);
            Require(SimulationEventKindRegistry.IsKnown(scheduledEvent.EventKind), $"scheduledEvents.{scheduledEvent.ScheduledEventId}.eventKind", $"Unknown event kind '{scheduledEvent.EventKind}'.", result);
            Require(scheduledEvent.StepIndex >= 0 && scheduledEvent.StepIndex < definition.StepCount, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.stepIndex", "Scheduled event step index is outside the scenario range.", result);
            if (definition.ExperimentMode is SimulationExperimentMode.Strict or SimulationExperimentMode.ResearchGrade &&
                canonicalEventKind is SimulationEventKind.ActorTradeSell or SimulationEventKind.ActorTradeBuy &&
                scheduledEvent.ResourceIds.Distinct(StringComparer.Ordinal).Count() > 1 &&
                scheduledEvent.TransactionLegs.Count == 0)
            {
                Require(false, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.transactionLegs", "Strict multi-resource trade events require explicit transaction legs.", result);
            }

            foreach (string actorId in scheduledEvent.ActorIds)
            {
                RequireReference(actorId, actors, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.actorIds", result);
            }

            foreach (string resourceId in scheduledEvent.ResourceIds)
            {
                RequireReference(resourceId, resources, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.resourceIds", result);
            }

            RequireReference(scheduledEvent.SourceId, anyReference, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.sourceId", result);
            RequireReference(scheduledEvent.TargetId, anyReference, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.targetId", result);
            RequireReference(scheduledEvent.SourceLocationId, locations, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.sourceLocationId", result);
            RequireReference(scheduledEvent.TargetLocationId, locations, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.targetLocationId", result);
            for (var effectIndex = 0; effectIndex < scheduledEvent.Effects.Count; effectIndex++)
            {
                SimulationEventEffect effect = scheduledEvent.Effects[effectIndex];
                string effectPath = $"scheduledEvents.{scheduledEvent.ScheduledEventId}.effects[{effectIndex}]";
                Require(!string.IsNullOrWhiteSpace(effect.EffectKind), $"{effectPath}.effectKind", "Effect kind is required.", result);
                RequireReference(effect.TargetId, anyReference, $"{effectPath}.targetId", result);
            }

            for (var legIndex = 0; legIndex < scheduledEvent.TransactionLegs.Count; legIndex++)
            {
                SimulationTransactionLegDefinition leg = scheduledEvent.TransactionLegs[legIndex];
                string legPath = $"scheduledEvents.{scheduledEvent.ScheduledEventId}.transactionLegs[{legIndex}]";
                Require(!string.IsNullOrWhiteSpace(leg.ResourceId), $"{legPath}.resourceId", "Transaction leg resource id is required.", result);
                Require(leg.Quantity != 0, $"{legPath}.quantity", "Transaction leg quantity must be non-zero.", result);
                RequireReference(leg.ResourceId, resources, $"{legPath}.resourceId", result);
                RequireReference(leg.FromActorId, actors, $"{legPath}.fromActorId", result);
                RequireReference(leg.ToActorId, actors, $"{legPath}.toActorId", result);
                RequireReference(leg.FromStoreId, stores, $"{legPath}.fromStoreId", result);
                RequireReference(leg.ToStoreId, stores, $"{legPath}.toStoreId", result);
            }

            foreach (SimulationStoreResolutionPolicyDefinition policy in scheduledEvent.StoreResolutionPolicies)
            {
                Require(!string.IsNullOrWhiteSpace(policy.Role), $"scheduledEvents.{scheduledEvent.ScheduledEventId}.storeResolutionPolicies.role", "Store resolution policy role is required.", result);
                Require(!string.IsNullOrWhiteSpace(policy.Policy), $"scheduledEvents.{scheduledEvent.ScheduledEventId}.storeResolutionPolicies.{policy.Role}.policy", "Store resolution policy is required.", result);
                RequireReference(policy.StoreId, stores, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.storeResolutionPolicies.{policy.Role}.storeId", result);
                RequireReference(policy.ActorId, actors, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.storeResolutionPolicies.{policy.Role}.actorId", result);
                RequireReference(policy.LocationId, locations, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.storeResolutionPolicies.{policy.Role}.locationId", result);
                RequireReference(policy.ResourceId, resources, $"scheduledEvents.{scheduledEvent.ScheduledEventId}.storeResolutionPolicies.{policy.Role}.resourceId", result);
            }
        }

        foreach (SimulationScenarioEventTemplate template in definition.EventTemplates)
        {
            Require(SimulationEventKindRegistry.IsKnown(template.EventKind), $"eventTemplates.{template.TemplateId}.eventKind", $"Unknown event kind '{template.EventKind}'.", result);
            Require(template.StepIndex >= 0 && template.StepIndex < definition.StepCount, $"eventTemplates.{template.TemplateId}.stepIndex", "Event template step index is outside the scenario range.", result);
            RequireReference(template.ActorId, anyReference, $"eventTemplates.{template.TemplateId}.actorId", result);
            RequireReference(template.TargetActorId, anyReference, $"eventTemplates.{template.TemplateId}.targetActorId", result);
            RequireReference(template.PlaceId, anyReference, $"eventTemplates.{template.TemplateId}.placeId", result);
            RequireReference(template.ResourceId, resources, $"eventTemplates.{template.TemplateId}.resourceId", result);
        }

        if (boundaryFunctionRegistry is not null)
        {
            ValidateBoundaryFunctionRegistry(boundaryFunctionRegistry, resources, actors, locations, result);
        }

        return result;
    }

    private static void ValidateBoundaryFunctionRegistry(
        SimulationScenarioExtensionDefinition registry,
        HashSet<string> resources,
        HashSet<string> actors,
        HashSet<string> locations,
        SimulationScenarioDefinitionValidationResult result)
    {
        Require(
            string.Equals(registry.ExtensionId, SimulationScenarioExtensionIds.BoundaryFunctionRegistry, StringComparison.Ordinal),
            "boundaryFunctionRegistry.extensionId",
            "Boundary function registry extension id is not recognized.",
            result);
        Require(
            string.Equals(registry.SchemaId, SimulationScenarioExtensionSchemas.BoundaryFunctionRegistry, StringComparison.Ordinal),
            "boundaryFunctionRegistry.schemaId",
            "Boundary function registry schema id is not recognized.",
            result);
        Require(
            string.Equals(registry.SchemaHash, SimulationScenarioExtensionSchemas.BoundaryFunctionRegistryHash, StringComparison.Ordinal),
            "boundaryFunctionRegistry.schemaHash",
            "Boundary function registry schema hash must match the approved extension schema.",
            result);

        foreach (SimulationScenarioBoundaryFunctionDefinition function in registry.Functions)
        {
            Require(!string.IsNullOrWhiteSpace(function.FunctionId), "boundaryFunctionRegistry.functions.functionId", "Boundary function id is required.", result);
            Require(!string.IsNullOrWhiteSpace(function.FunctionKind), $"boundaryFunctionRegistry.functions.{function.FunctionId}.functionKind", "Boundary function kind is required.", result);
            Require(function.Direction is "source" or "sink" or "both", $"boundaryFunctionRegistry.functions.{function.FunctionId}.direction", "Boundary function direction must be source, sink, or both.", result);
            Require(function.ResourceIds.Count > 0, $"boundaryFunctionRegistry.functions.{function.FunctionId}.resourceIds", "Boundary function must list at least one resource.", result);
            foreach (string resourceId in function.ResourceIds)
            {
                RequireReference(resourceId, resources, $"boundaryFunctionRegistry.functions.{function.FunctionId}.resourceIds", result);
            }

            foreach (string actorId in function.ActorIds)
            {
                RequireReference(actorId, actors, $"boundaryFunctionRegistry.functions.{function.FunctionId}.actorIds", result);
            }

            foreach (string locationId in function.LocationIds)
            {
                RequireReference(locationId, locations, $"boundaryFunctionRegistry.functions.{function.FunctionId}.locationIds", result);
            }

            if (function.MinQuantity is not null && function.MaxQuantity is not null)
            {
                Require(function.MinQuantity <= function.MaxQuantity, $"boundaryFunctionRegistry.functions.{function.FunctionId}.bounds", "Boundary function minimum quantity must not exceed maximum quantity.", result);
            }
        }
    }

    private static void CheckDuplicates(IEnumerable<string> ids, string path, SimulationScenarioDefinitionValidationResult result)
    {
        foreach (string id in ids.Where(id => !string.IsNullOrWhiteSpace(id)).GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            result.Messages.Add(new() { Code = "duplicate-id", Path = path, Message = $"Duplicate id '{id}'." });
        }
    }

    private static void RequireReference(string value, HashSet<string> validIds, string path, SimulationScenarioDefinitionValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(value) && !validIds.Contains(value))
        {
            result.Messages.Add(new() { Code = "missing-reference", Path = path, Message = $"Reference '{value}' was not found." });
        }
    }

    private static void Require(bool condition, string path, string message, SimulationScenarioDefinitionValidationResult result)
    {
        if (!condition)
        {
            result.Messages.Add(new() { Code = "invalid-value", Path = path, Message = message });
        }
    }
}
