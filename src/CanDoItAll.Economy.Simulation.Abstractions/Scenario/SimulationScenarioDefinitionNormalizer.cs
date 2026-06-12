namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationScenarioDefinitionNormalizationResult
{
    public SimulationScenarioDefinition Definition { get; set; } = new();

    public List<SimulationScenarioDefinitionValidationMessage> Messages { get; set; } = [];
}

public interface ISimulationScenarioDefinitionNormalizer
{
    SimulationScenarioDefinitionNormalizationResult Normalize(SimulationScenarioDefinition definition);
}

public sealed class SimulationScenarioDefinitionNormalizer : ISimulationScenarioDefinitionNormalizer
{
    public SimulationScenarioDefinitionNormalizationResult Normalize(SimulationScenarioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var result = new SimulationScenarioDefinitionNormalizationResult
        {
            Definition = CloneHeader(definition)
        };

        result.Definition.Actors = NormalizeActors(definition, result.Messages);
        result.Definition.Locations = NormalizeLocations(definition, result.Messages);
        result.Definition.InitialStores = NormalizeStores(definition, result.Messages);
        result.Definition.ScheduledEvents = NormalizeEvents(definition, result.Messages);
        result.Definition.Resources = [.. definition.Resources.Select(Clone)];
        result.Definition.Objects = [.. definition.Objects.Select(Clone)];
        result.Definition.Behaviors = [.. definition.Behaviors.Select(Clone)];
        result.Definition.Relationships = [.. definition.Relationships.Select(Clone)];
        result.Definition.Rules = [.. definition.Rules];
        result.Definition.RuleDefinitions = [.. definition.RuleDefinitions.Select(Clone)];
        result.Definition.Extensions = NormalizeExtensions(definition.Extensions);
        result.Definition.BackendHints = [.. definition.BackendHints.Select(Clone)];
        result.Definition.Metadata["canonical.normalized"] = "true";
        return result;
    }

    private static SimulationScenarioDefinition CloneHeader(SimulationScenarioDefinition definition)
        => new()
        {
            ScenarioId = definition.ScenarioId,
            DisplayName = definition.DisplayName,
            ScenarioKind = definition.ScenarioKind,
            BaseCurrency = definition.BaseCurrency,
            StartsAtUtc = definition.StartsAtUtc,
            StepCount = definition.StepCount,
            StepDuration = definition.StepDuration,
            Seed = definition.Seed,
            ExperimentMode = definition.ExperimentMode,
            ExpansionProfileId = definition.ExpansionProfileId,
            AllowedWarningCodes = [.. definition.AllowedWarningCodes],
            Schedule = new()
            {
                ScheduleId = definition.Schedule.ScheduleId,
                StepDuration = definition.Schedule.StepDuration,
                Metadata = new Dictionary<string, string>(definition.Schedule.Metadata, StringComparer.Ordinal)
            },
            Metadata = new Dictionary<string, string>(definition.Metadata, StringComparer.Ordinal)
        };

    private static List<SimulationScenarioExtensionDefinition> NormalizeExtensions(IEnumerable<SimulationScenarioExtensionDefinition> extensions)
        => [.. extensions
            .OrderBy(static extension => extension.ExtensionId, StringComparer.Ordinal)
            .ThenBy(static extension => extension.RegistryId, StringComparer.Ordinal)
            .Select(Clone)];

    private static SimulationScenarioExtensionDefinition Clone(SimulationScenarioExtensionDefinition registry)
        => new()
        {
            ExtensionId = registry.ExtensionId,
            ExtensionKind = registry.ExtensionKind,
            SchemaId = registry.SchemaId,
            SchemaVersion = registry.SchemaVersion,
            SchemaHash = registry.SchemaHash,
            RegistryId = registry.RegistryId,
            Version = registry.Version,
            Functions = [.. registry.Functions
                .OrderBy(static function => function.FunctionId, StringComparer.Ordinal)
                .Select(static function => new SimulationScenarioBoundaryFunctionDefinition
                {
                    FunctionId = function.FunctionId,
                    FunctionKind = function.FunctionKind,
                    Direction = function.Direction,
                    ResourceIds = [.. function.ResourceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                    MinQuantity = function.MinQuantity,
                    MaxQuantity = function.MaxQuantity,
                    ActorIds = [.. function.ActorIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                    LocationIds = [.. function.LocationIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                    BoundaryIds = [.. function.BoundaryIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                    OwnerKind = function.OwnerKind,
                    OwnerId = function.OwnerId,
                    Metadata = new Dictionary<string, string>(function.Metadata, StringComparer.Ordinal)
                })],
            Metadata = new Dictionary<string, string>(registry.Metadata, StringComparer.Ordinal)
        };

    private static List<SimulationScenarioActorDefinition> NormalizeActors(
        SimulationScenarioDefinition definition,
        List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        var actors = CanonicalById(definition.Actors, static actor => actor.ActorId, Clone, "actors", messages);
        foreach (SimulationScenarioEntityDefinition entity in definition.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.EntityId) || actors.ContainsKey(entity.EntityId))
            {
                if (actors.TryGetValue(entity.EntityId, out SimulationScenarioActorDefinition? actor) &&
                    !string.IsNullOrWhiteSpace(entity.HomePlaceId) &&
                    !string.Equals(actor.HomeLocationId, entity.HomePlaceId, StringComparison.Ordinal))
                {
                    Warn(messages, "aliases.entities", $"Entity '{entity.EntityId}' home alias differs from actor home; canonical Actors value was used.");
                }

                if (actors.TryGetValue(entity.EntityId, out actor) &&
                    !string.IsNullOrWhiteSpace(entity.DisplayName) &&
                    !string.IsNullOrWhiteSpace(actor.DisplayName) &&
                    !string.Equals(actor.DisplayName, entity.DisplayName, StringComparison.Ordinal))
                {
                    Warn(messages, "aliases.entities", $"Entity '{entity.EntityId}' display name differs from actor display name; canonical Actors value was used.");
                }

                continue;
            }

            actors[entity.EntityId] = new()
            {
                ActorId = entity.EntityId,
                Kind = string.IsNullOrWhiteSpace(entity.Kind) ? "actor" : entity.Kind,
                DisplayName = entity.DisplayName,
                Role = entity.Role,
                HomeLocationId = entity.HomePlaceId,
                Metadata = new Dictionary<string, string>(entity.Metadata, StringComparer.Ordinal)
            };
        }

        return [.. actors.Values.OrderBy(static actor => actor.ActorId, StringComparer.Ordinal)];
    }

    private static List<SimulationScenarioLocationDefinition> NormalizeLocations(
        SimulationScenarioDefinition definition,
        List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        var locations = CanonicalById(definition.Locations, static location => location.LocationId, Clone, "locations", messages);
        foreach (SimulationScenarioPlaceDefinition place in definition.Places)
        {
            if (string.IsNullOrWhiteSpace(place.PlaceId) || locations.ContainsKey(place.PlaceId))
            {
                if (locations.TryGetValue(place.PlaceId, out SimulationScenarioLocationDefinition? location) &&
                    (location.X != place.X || location.Y != place.Y || location.Z != place.Z))
                {
                    Warn(messages, "aliases.places", $"Place '{place.PlaceId}' coordinates differ from canonical Locations value.");
                }

                if (locations.TryGetValue(place.PlaceId, out location) &&
                    !string.IsNullOrWhiteSpace(place.DisplayName) &&
                    !string.IsNullOrWhiteSpace(location.DisplayName) &&
                    !string.Equals(location.DisplayName, place.DisplayName, StringComparison.Ordinal))
                {
                    Warn(messages, "aliases.places", $"Place '{place.PlaceId}' display name differs from canonical Locations value.");
                }

                continue;
            }

            locations[place.PlaceId] = new()
            {
                LocationId = place.PlaceId,
                Kind = place.Kind,
                DisplayName = place.DisplayName,
                X = place.X,
                Y = place.Y,
                Z = place.Z,
                Metadata = new Dictionary<string, string>(place.Metadata, StringComparer.Ordinal)
            };
        }

        return [.. locations.Values.OrderBy(static location => location.LocationId, StringComparer.Ordinal)];
    }

    private static List<SimulationScenarioStoreDefinition> NormalizeStores(
        SimulationScenarioDefinition definition,
        List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        var stores = CanonicalById(definition.InitialStores, static store => store.StoreId, Clone, "initialStores", messages);
        foreach (SimulationScenarioStoreDefinition store in definition.Stores)
        {
            if (string.IsNullOrWhiteSpace(store.StoreId) || stores.ContainsKey(store.StoreId))
            {
                if (stores.TryGetValue(store.StoreId, out SimulationScenarioStoreDefinition? initial) &&
                    initial.Quantity != store.Quantity)
                {
                    Warn(messages, "aliases.stores", $"Store '{store.StoreId}' quantity differs from canonical InitialStores value.");
                }

                continue;
            }

            stores[store.StoreId] = Clone(store);
        }

        return [.. stores.Values.OrderBy(static store => store.StoreId, StringComparer.Ordinal)];
    }

    private static List<SimulationScheduledEventDefinition> NormalizeEvents(
        SimulationScenarioDefinition definition,
        List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        var events = CanonicalById(definition.ScheduledEvents, static scheduled => scheduled.ScheduledEventId, Clone, "scheduledEvents", messages);
        foreach (SimulationScenarioEventTemplate template in definition.EventTemplates)
        {
            if (string.IsNullOrWhiteSpace(template.TemplateId) || events.ContainsKey(template.TemplateId))
            {
                if (events.TryGetValue(template.TemplateId, out SimulationScheduledEventDefinition? scheduled) &&
                    !string.Equals(SimulationEventKindRegistry.Normalize(template.EventKind), SimulationEventKindRegistry.Normalize(scheduled.EventKind), StringComparison.Ordinal))
                {
                    Warn(messages, "aliases.eventTemplates", $"Event template '{template.TemplateId}' kind differs from canonical ScheduledEvents value.");
                }

                continue;
            }

            events[template.TemplateId] = new()
            {
                ScheduledEventId = template.TemplateId,
                EventKind = SimulationEventKindRegistry.Normalize(template.EventKind),
                StepIndex = template.StepIndex,
                ActorIds = string.IsNullOrWhiteSpace(template.ActorId) ? [] : [template.ActorId],
                ResourceIds = string.IsNullOrWhiteSpace(template.ResourceId) ? [] : [template.ResourceId],
                SourceId = template.ActorId,
                TargetId = string.IsNullOrWhiteSpace(template.TargetActorId) ? template.ResourceId : template.TargetActorId,
                TargetLocationId = template.PlaceId,
                Magnitude = template.Quantity,
                Duration = template.Duration,
                Metadata = new Dictionary<string, string>(template.Metadata, StringComparer.Ordinal)
            };
        }

        foreach (SimulationScheduledEventDefinition scheduled in events.Values)
        {
            scheduled.EventKind = SimulationEventKindRegistry.Normalize(scheduled.EventKind);
        }

        return [.. events.Values
            .OrderBy(static scheduled => scheduled.StepIndex)
            .ThenBy(static scheduled => scheduled.Offset)
            .ThenBy(static scheduled => scheduled.Order)
            .ThenBy(static scheduled => scheduled.ScheduledEventId, StringComparer.Ordinal)];
    }

    private static SimulationScenarioActorDefinition Clone(SimulationScenarioActorDefinition actor)
        => new()
        {
            ActorId = actor.ActorId,
            Kind = actor.Kind,
            DisplayName = actor.DisplayName,
            Role = actor.Role,
            HomeLocationId = actor.HomeLocationId,
            Metadata = new Dictionary<string, string>(actor.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioLocationDefinition Clone(SimulationScenarioLocationDefinition location)
        => new()
        {
            LocationId = location.LocationId,
            Kind = location.Kind,
            DisplayName = location.DisplayName,
            X = location.X,
            Y = location.Y,
            Z = location.Z,
            Metadata = new Dictionary<string, string>(location.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioResourceDefinition Clone(SimulationScenarioResourceDefinition resource)
        => new()
        {
            ResourceId = resource.ResourceId,
            Kind = resource.Kind,
            DisplayName = resource.DisplayName,
            Unit = resource.Unit,
            Capacity = resource.Capacity,
            AllowsNegativeCapacity = resource.AllowsNegativeCapacity,
            Metadata = new Dictionary<string, string>(resource.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioObjectDefinition Clone(SimulationScenarioObjectDefinition item)
        => new()
        {
            ObjectId = item.ObjectId,
            Kind = item.Kind,
            DisplayName = item.DisplayName,
            LocationId = item.LocationId,
            Metadata = new Dictionary<string, string>(item.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioStoreDefinition Clone(SimulationScenarioStoreDefinition store)
        => new()
        {
            StoreId = store.StoreId,
            OwnerActorId = store.OwnerActorId,
            ResourceId = store.ResourceId,
            LocationId = store.LocationId,
            DisplayName = store.DisplayName,
            Quantity = store.Quantity,
            Unit = store.Unit,
            Capacity = store.Capacity,
            AllowsNegativeCapacity = store.AllowsNegativeCapacity,
            Metadata = new Dictionary<string, string>(store.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioBehaviorDefinition Clone(SimulationScenarioBehaviorDefinition behavior)
        => new()
        {
            BehaviorId = behavior.BehaviorId,
            EntityId = behavior.EntityId,
            Kind = behavior.Kind,
            TriggerEventKind = SimulationEventKindRegistry.Normalize(behavior.TriggerEventKind),
            Metadata = new Dictionary<string, string>(behavior.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioRelationshipDefinition Clone(SimulationScenarioRelationshipDefinition relationship)
        => new()
        {
            RelationshipId = relationship.RelationshipId,
            SourceActorId = relationship.SourceActorId,
            TargetActorId = relationship.TargetActorId,
            Kind = relationship.Kind,
            Strength = relationship.Strength,
            Metadata = new Dictionary<string, string>(relationship.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioRuleDefinition Clone(SimulationScenarioRuleDefinition rule)
        => new()
        {
            RuleId = rule.RuleId,
            DisplayName = rule.DisplayName,
            Category = rule.Category,
            Parameters = new Dictionary<string, string>(rule.Parameters, StringComparer.Ordinal),
            Metadata = new Dictionary<string, string>(rule.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScenarioBackendHint Clone(SimulationScenarioBackendHint hint)
        => new()
        {
            BackendId = hint.BackendId,
            Priority = hint.Priority,
            Metadata = new Dictionary<string, string>(hint.Metadata, StringComparer.Ordinal)
        };

    private static SimulationScheduledEventDefinition Clone(SimulationScheduledEventDefinition scheduled)
        => new()
        {
            ScheduledEventId = scheduled.ScheduledEventId,
            EventKind = SimulationEventKindRegistry.Normalize(scheduled.EventKind),
            StepIndex = scheduled.StepIndex,
            ActorIds = [.. scheduled.ActorIds],
            ResourceIds = [.. scheduled.ResourceIds],
            SourceId = scheduled.SourceId,
            TargetId = scheduled.TargetId,
            SourceLocationId = scheduled.SourceLocationId,
            TargetLocationId = scheduled.TargetLocationId,
            Magnitude = scheduled.Magnitude,
            Duration = scheduled.Duration,
            Offset = scheduled.Offset,
            Order = scheduled.Order,
            Effects = [.. scheduled.Effects.Select(Clone)],
            TransactionLegs = [.. scheduled.TransactionLegs.Select(Clone)],
            StoreResolutionPolicies = [.. scheduled.StoreResolutionPolicies],
            Metadata = new Dictionary<string, string>(scheduled.Metadata, StringComparer.Ordinal)
        };

    private static SimulationEventEffect Clone(SimulationEventEffect effect)
        => new()
        {
            EffectKind = effect.EffectKind,
            TargetId = effect.TargetId,
            Value = effect.Value,
            Metadata = new Dictionary<string, string>(effect.Metadata, StringComparer.Ordinal)
        };

    private static SimulationTransactionLegDefinition Clone(SimulationTransactionLegDefinition leg)
        => new()
        {
            LegId = leg.LegId,
            LegKind = leg.LegKind,
            Direction = leg.Direction,
            FromActorId = leg.FromActorId,
            ToActorId = leg.ToActorId,
            FromStoreId = leg.FromStoreId,
            ToStoreId = leg.ToStoreId,
            ResourceId = leg.ResourceId,
            Quantity = leg.Quantity,
            UnitPrice = leg.UnitPrice,
            Reason = leg.Reason,
            Metadata = new Dictionary<string, string>(leg.Metadata, StringComparer.Ordinal)
        };

    private static Dictionary<string, T> CanonicalById<T>(
        IEnumerable<T> items,
        Func<T, string> idSelector,
        Func<T, T> clone,
        string path,
        List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        var values = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (T item in items)
        {
            string id = idSelector(item);
            if (!values.TryAdd(id, clone(item)))
            {
                Duplicate(messages, path, id);
            }
        }

        return values;
    }

    private static void Warn(List<SimulationScenarioDefinitionValidationMessage> messages, string path, string message)
        => messages.Add(new()
        {
            Severity = "warning",
            Code = "alias-conflict",
            Path = path,
            Message = message
        });

    private static void Duplicate(List<SimulationScenarioDefinitionValidationMessage> messages, string path, string id)
        => messages.Add(new()
        {
            Severity = "error",
            Code = "duplicate-id",
            Path = path,
            Message = $"Duplicate id '{id}'."
        });
}
