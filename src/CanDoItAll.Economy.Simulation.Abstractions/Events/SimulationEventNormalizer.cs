namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationEventNormalizationResult
{
    public SimulationEvent Event { get; set; } = new();

    public List<SimulationScenarioDefinitionValidationMessage> Messages { get; set; } = [];

    public bool IsValid => Messages.All(message => !string.Equals(message.Severity, "error", StringComparison.OrdinalIgnoreCase));
}

public interface ISimulationEventNormalizer
{
    SimulationEventNormalizationResult Normalize(SimulationEvent simulationEvent);
}

public interface ISimulationEventValidator
{
    SimulationEventNormalizationResult Validate(SimulationEvent simulationEvent);
}

public sealed class SimulationEventNormalizer : ISimulationEventNormalizer, ISimulationEventValidator
{
    public SimulationEventNormalizationResult Normalize(SimulationEvent simulationEvent)
    {
        ArgumentNullException.ThrowIfNull(simulationEvent);
        var result = new SimulationEventNormalizationResult
        {
            Event = Clone(simulationEvent)
        };

        SimulationEvent normalized = result.Event;
        normalized.EventKind = SimulationEventKindRegistry.Normalize(normalized.EventKind);
        normalized.Timing ??= new();
        NormalizeActors(normalized, result.Messages);
        NormalizeResources(normalized);
        NormalizeDuration(normalized, result.Messages);
        NormalizeMagnitude(normalized, result.Messages);
        NormalizeParticipants(normalized);
        NormalizeTypedRefs(normalized);
        normalized.Metadata["canonicalEventKind"] = normalized.EventKind;
        return result;
    }

    public SimulationEventNormalizationResult Validate(SimulationEvent simulationEvent)
        => Normalize(simulationEvent);

    private static void NormalizeActors(SimulationEvent simulationEvent, List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        List<string> actorIds = [.. simulationEvent.ActorIds.Where(static actorId => !string.IsNullOrWhiteSpace(actorId))];
        foreach (SimulationTransactionLegDefinition leg in simulationEvent.TransactionLegs)
        {
            AddIfPresent(actorIds, leg.FromActorId);
            AddIfPresent(actorIds, leg.ToActorId);
        }

        if (!string.IsNullOrWhiteSpace(simulationEvent.ActorId))
        {
            if (actorIds.Count > 0 && !string.Equals(actorIds[0], simulationEvent.ActorId, StringComparison.Ordinal))
            {
                Error(messages, "event.actorIds", $"ActorId '{simulationEvent.ActorId}' conflicts with ActorIds[0] '{actorIds[0]}'.");
            }

            actorIds.RemoveAll(actorId => string.Equals(actorId, simulationEvent.ActorId, StringComparison.Ordinal));
            actorIds.Insert(0, simulationEvent.ActorId);
        }
        else if (actorIds.Count > 0)
        {
            simulationEvent.ActorId = actorIds[0];
        }

        simulationEvent.ActorIds = [.. actorIds.Distinct(StringComparer.Ordinal)];
        if (string.IsNullOrWhiteSpace(simulationEvent.TargetActorId) && simulationEvent.ActorIds.Count > 1)
        {
            simulationEvent.TargetActorId = simulationEvent.ActorIds[1];
        }
    }

    private static void NormalizeResources(SimulationEvent simulationEvent)
    {
        List<string> resourceIds = [.. simulationEvent.ResourceIds.Where(static resourceId => !string.IsNullOrWhiteSpace(resourceId))];
        foreach (SimulationTransactionLegDefinition leg in simulationEvent.TransactionLegs)
        {
            AddIfPresent(resourceIds, leg.ResourceId);
        }

        if (!string.IsNullOrWhiteSpace(simulationEvent.ResourceId))
        {
            resourceIds.RemoveAll(resourceId => string.Equals(resourceId, simulationEvent.ResourceId, StringComparison.Ordinal));
            resourceIds.Insert(0, simulationEvent.ResourceId);
        }
        else if (resourceIds.Count > 0)
        {
            simulationEvent.ResourceId = resourceIds[0];
        }

        simulationEvent.ResourceIds = [.. resourceIds.Distinct(StringComparer.Ordinal)];
    }

    private static void NormalizeDuration(SimulationEvent simulationEvent, List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        TimeSpan eventDuration = simulationEvent.Duration;
        TimeSpan timingDuration = simulationEvent.Timing.Duration;
        if (eventDuration != TimeSpan.Zero &&
            timingDuration != TimeSpan.Zero &&
            eventDuration != timingDuration)
        {
            Error(messages, "event.duration", $"Duration '{eventDuration}' conflicts with Timing.Duration '{timingDuration}'.");
        }

        TimeSpan canonical = timingDuration != TimeSpan.Zero ? timingDuration : eventDuration;
        simulationEvent.Duration = canonical;
        simulationEvent.Timing.Duration = canonical;
    }

    private static void NormalizeMagnitude(SimulationEvent simulationEvent, List<SimulationScenarioDefinitionValidationMessage> messages)
    {
        if (TryReadDecimal(simulationEvent.Metadata, "quantity", out decimal quantity) &&
            simulationEvent.Magnitude != 0 &&
            simulationEvent.Magnitude != quantity)
        {
            Error(messages, "event.quantity", $"Quantity '{quantity}' conflicts with Magnitude '{simulationEvent.Magnitude}'.");
        }

        if (simulationEvent.Magnitude == 0 && TryReadDecimal(simulationEvent.Metadata, "quantity", out quantity))
        {
            simulationEvent.Magnitude = quantity;
        }
    }

    private static void NormalizeParticipants(SimulationEvent simulationEvent)
    {
        var participants = new Dictionary<string, SimulationEventParticipant>(StringComparer.Ordinal);
        foreach (SimulationEventParticipant participant in simulationEvent.Participants)
        {
            string id = FirstNonEmpty(participant.ParticipantId, participant.ActorId, participant.ResourceId, participant.LocationId);
            if (!string.IsNullOrWhiteSpace(id))
            {
                participants[id] = Clone(participant, id);
            }
        }

        AddParticipant(participants, simulationEvent.ActorId, "actor", actorId: simulationEvent.ActorId);
        AddParticipant(participants, simulationEvent.TargetActorId, "target-actor", actorId: simulationEvent.TargetActorId);
        AddParticipant(participants, simulationEvent.ResourceId, "resource", resourceId: simulationEvent.ResourceId);
        foreach (SimulationTransactionLegDefinition leg in simulationEvent.TransactionLegs)
        {
            AddParticipant(participants, leg.FromActorId, "transaction-from", actorId: leg.FromActorId, resourceId: leg.ResourceId);
            AddParticipant(participants, leg.ToActorId, "transaction-to", actorId: leg.ToActorId, resourceId: leg.ResourceId);
            AddParticipant(participants, leg.ResourceId, "transaction-resource", resourceId: leg.ResourceId);
        }

        AddParticipant(participants, simulationEvent.SourceId, "source");
        AddParticipant(participants, simulationEvent.TargetId, "target");
        AddParticipant(participants, simulationEvent.PlaceId, "location", locationId: simulationEvent.PlaceId);
        string sourceLocationId = simulationEvent.Metadata.GetValueOrDefault("sourceLocationId") ?? string.Empty;
        string targetLocationId = simulationEvent.Metadata.GetValueOrDefault("targetLocationId") ?? string.Empty;
        AddParticipant(participants, sourceLocationId, "source-location", locationId: sourceLocationId);
        AddParticipant(participants, targetLocationId, "target-location", locationId: targetLocationId);

        simulationEvent.Participants = [.. participants.Values.OrderBy(static participant => participant.ParticipantId, StringComparer.Ordinal)];
    }

    private static void NormalizeTypedRefs(SimulationEvent simulationEvent)
    {
        if (string.IsNullOrWhiteSpace(simulationEvent.Source.Id))
        {
            simulationEvent.Source = InferRef(simulationEvent.SourceId, simulationEvent);
        }

        if (string.IsNullOrWhiteSpace(simulationEvent.Target.Id))
        {
            simulationEvent.Target = InferRef(simulationEvent.TargetId, simulationEvent);
        }

        var refs = new Dictionary<string, SimulationRef>(StringComparer.Ordinal);
        AddRef(refs, simulationEvent.Source);
        AddRef(refs, simulationEvent.Target);
        AddRef(refs, InferRef(simulationEvent.ActorId, simulationEvent, SimulationRefKinds.Actor));
        AddRef(refs, InferRef(simulationEvent.TargetActorId, simulationEvent, SimulationRefKinds.Actor));
        AddRef(refs, InferRef(simulationEvent.ResourceId, simulationEvent, SimulationRefKinds.Resource));
        AddRef(refs, InferRef(simulationEvent.PlaceId, simulationEvent, SimulationRefKinds.Location));
        foreach (SimulationRef participantRef in simulationEvent.ParticipantRefs)
        {
            AddRef(refs, participantRef);
        }

        foreach (SimulationEventParticipant participant in simulationEvent.Participants)
        {
            AddRef(refs, InferRef(participant.ActorId, simulationEvent, SimulationRefKinds.Actor));
            AddRef(refs, InferRef(participant.ResourceId, simulationEvent, SimulationRefKinds.Resource));
            AddRef(refs, InferRef(participant.LocationId, simulationEvent, SimulationRefKinds.Location));
        }

        foreach (SimulationTransactionLegDefinition leg in simulationEvent.TransactionLegs)
        {
            AddRef(refs, InferRef(leg.FromActorId, simulationEvent, SimulationRefKinds.Actor));
            AddRef(refs, InferRef(leg.ToActorId, simulationEvent, SimulationRefKinds.Actor));
            AddRef(refs, InferRef(leg.ResourceId, simulationEvent, SimulationRefKinds.Resource));
        }

        simulationEvent.ParticipantRefs = [.. refs.Values.OrderBy(static item => item.Kind, StringComparer.Ordinal).ThenBy(static item => item.Id, StringComparer.Ordinal)];
    }

    private static SimulationRef InferRef(string id, SimulationEvent simulationEvent, string preferredKind = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new();
        }

        if (!string.IsNullOrWhiteSpace(preferredKind))
        {
            return SimulationRef.Create(preferredKind, id);
        }

        if (simulationEvent.ActorIds.Contains(id, StringComparer.Ordinal) ||
            string.Equals(simulationEvent.ActorId, id, StringComparison.Ordinal) ||
            string.Equals(simulationEvent.TargetActorId, id, StringComparison.Ordinal))
        {
            return SimulationRef.Create(SimulationRefKinds.Actor, id);
        }

        if (simulationEvent.ResourceIds.Contains(id, StringComparer.Ordinal) ||
            string.Equals(simulationEvent.ResourceId, id, StringComparison.Ordinal))
        {
            return SimulationRef.Create(SimulationRefKinds.Resource, id);
        }

        if (string.Equals(simulationEvent.PlaceId, id, StringComparison.Ordinal) ||
            string.Equals(simulationEvent.Metadata.GetValueOrDefault("sourceLocationId"), id, StringComparison.Ordinal) ||
            string.Equals(simulationEvent.Metadata.GetValueOrDefault("targetLocationId"), id, StringComparison.Ordinal))
        {
            return SimulationRef.Create(SimulationRefKinds.Location, id);
        }

        return SimulationRef.Create(SimulationRefKinds.Object, id);
    }

    private static void AddRef(Dictionary<string, SimulationRef> refs, SimulationRef simulationRef)
    {
        if (string.IsNullOrWhiteSpace(simulationRef.Kind) || string.IsNullOrWhiteSpace(simulationRef.Id))
        {
            return;
        }

        refs[$"{simulationRef.Kind}:{simulationRef.Id}"] = new()
        {
            Kind = simulationRef.Kind,
            Id = simulationRef.Id,
            Metadata = new Dictionary<string, string>(simulationRef.Metadata, StringComparer.Ordinal)
        };
    }

    private static void AddParticipant(
        Dictionary<string, SimulationEventParticipant> participants,
        string? id,
        string role,
        string actorId = "",
        string resourceId = "",
        string locationId = "")
    {
        if (string.IsNullOrWhiteSpace(id) || participants.ContainsKey(id))
        {
            return;
        }

        participants[id] = new()
        {
            ParticipantId = id,
            Role = role,
            ActorId = actorId,
            ResourceId = resourceId,
            LocationId = locationId
        };
    }

    private static SimulationEvent Clone(SimulationEvent simulationEvent)
        => new()
        {
            EventId = simulationEvent.EventId,
            EventKind = simulationEvent.EventKind,
            ActorId = simulationEvent.ActorId,
            TargetActorId = simulationEvent.TargetActorId,
            PlaceId = simulationEvent.PlaceId,
            ResourceId = simulationEvent.ResourceId,
            ActorIds = [.. simulationEvent.ActorIds],
            ResourceIds = [.. simulationEvent.ResourceIds],
            SourceId = simulationEvent.SourceId,
            TargetId = simulationEvent.TargetId,
            Source = Clone(simulationEvent.Source),
            Target = Clone(simulationEvent.Target),
            ParticipantRefs = [.. simulationEvent.ParticipantRefs.Select(Clone)],
            OccurredAtUtc = simulationEvent.OccurredAtUtc,
            Duration = simulationEvent.Duration,
            Magnitude = simulationEvent.Magnitude,
            Timing = new()
            {
                StepIndex = simulationEvent.Timing.StepIndex,
                Offset = simulationEvent.Timing.Offset,
                Duration = simulationEvent.Timing.Duration,
                Order = simulationEvent.Timing.Order
            },
            Participants = [.. simulationEvent.Participants.Select(participant => Clone(participant, FirstNonEmpty(participant.ParticipantId, participant.ActorId, participant.ResourceId, participant.LocationId)))],
            Effects = [.. simulationEvent.Effects.Select(static effect => new SimulationEventEffect
            {
                EffectKind = effect.EffectKind,
                TargetId = effect.TargetId,
                Value = effect.Value,
                Metadata = new Dictionary<string, string>(effect.Metadata, StringComparer.Ordinal)
            })],
            TransactionLegs = [.. simulationEvent.TransactionLegs.Select(Clone)],
            StoreResolutionPolicies = [.. simulationEvent.StoreResolutionPolicies.Select(static policy => new SimulationStoreResolutionPolicyDefinition
            {
                Role = policy.Role,
                Policy = policy.Policy,
                StoreId = policy.StoreId,
                ActorId = policy.ActorId,
                LocationId = policy.LocationId,
                ResourceId = policy.ResourceId,
                PoolId = policy.PoolId,
                Metadata = new Dictionary<string, string>(policy.Metadata, StringComparer.Ordinal)
            })],
            Metadata = new Dictionary<string, string>(simulationEvent.Metadata, StringComparer.Ordinal)
        };

    private static SimulationEventParticipant Clone(SimulationEventParticipant participant, string participantId)
        => new()
        {
            ParticipantId = participantId,
            Role = participant.Role,
            ActorId = participant.ActorId,
            ResourceId = participant.ResourceId,
            LocationId = participant.LocationId,
            Metadata = new Dictionary<string, string>(participant.Metadata, StringComparer.Ordinal)
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

    private static SimulationRef Clone(SimulationRef simulationRef)
        => new()
        {
            Kind = simulationRef.Kind,
            Id = simulationRef.Id,
            Metadata = new Dictionary<string, string>(simulationRef.Metadata, StringComparer.Ordinal)
        };

    private static void Error(List<SimulationScenarioDefinitionValidationMessage> messages, string path, string message)
        => messages.Add(new()
        {
            Severity = "error",
            Code = "event-alias-conflict",
            Path = path,
            Message = message
        });

    private static bool TryReadDecimal(IReadOnlyDictionary<string, string> metadata, string key, out decimal value)
    {
        value = 0;
        return metadata.TryGetValue(key, out string? text) &&
               decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static void AddIfPresent(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
