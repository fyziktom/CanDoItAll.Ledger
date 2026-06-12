namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationEventStream
{
    public string ScenarioId { get; set; } = string.Empty;

    public List<SimulationEvent> Events { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];
}

public interface ISimulationScenarioEventCompiler
{
    SimulationEventStream Compile(SimulationScenarioDefinition definition);
}

public sealed class SimulationBehaviorExpansionRequest
{
    public SimulationScenarioDefinition Definition { get; set; } = new();
    public SimulationEventStream BaseEvents { get; set; } = new();
    public string ProfileId { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = [];
}

public sealed class SimulationBehaviorExpansionContext
{
    public SimulationScenarioDefinition Definition { get; set; } = new();
    public IReadOnlyList<SimulationEvent> Events { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationBehaviorExpansionResult
{
    public SimulationEventStream Events { get; set; } = new();
    public List<SimulationScenarioDefinitionValidationMessage> Diagnostics { get; set; } = [];
}

public interface ISimulationBehaviorExpander
{
    SimulationBehaviorExpansionResult Expand(SimulationBehaviorExpansionRequest request);
}

public interface ISimulationRuleEventExpander
{
    IReadOnlyList<SimulationEvent> ExpandRuleEvents(SimulationBehaviorExpansionContext context, SimulationRuleRef rule);
}

public sealed class SimulationScenarioEventCompiler : ISimulationScenarioEventCompiler
{
    private readonly SimulationScenarioDefinitionNormalizer normalizer = new();
    private readonly SimulationEventNormalizer eventNormalizer = new();
    private readonly ISimulationBehaviorExpander expander = new SimulationBehaviorExpander();

    public SimulationEventStream Compile(SimulationScenarioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition = normalizer.Normalize(definition).Definition;

        var baseEvents = definition.ScheduledEvents
            .OrderBy(static item => item.StepIndex)
            .ThenBy(static item => item.Offset)
            .ThenBy(static item => item.Order)
            .ThenBy(static item => item.ScheduledEventId, StringComparer.Ordinal)
            .Select(item => eventNormalizer.Normalize(CreateEvent(definition, item, SimulationEventKindRegistry.Normalize(item.EventKind), item.ScheduledEventId, item.Offset, item.Order)).Event)
            .ToList();

        SimulationBehaviorExpansionProfileDescriptor profile = SimulationBehaviorExpansionProfiles.ResolveDescriptor(
            definition.ExpansionProfileId,
            definition.ExperimentMode);
        var baseStream = new SimulationEventStream
        {
            ScenarioId = definition.ScenarioId,
            Events = baseEvents,
            Metadata =
            {
                ["definitionHash"] = SimulationDeterministicHash.HashDefinition(definition),
                ["experimentMode"] = SimulationExperimentModeNames.ToName(definition.ExperimentMode),
                ["expansionProfileId"] = profile.ProfileId,
                ["expansionProfileVersion"] = profile.Version,
                ["expansionProfileHash"] = profile.Hash
            }
        };

        SimulationBehaviorExpansionResult expanded = expander.Expand(new()
        {
            Definition = definition,
            BaseEvents = baseStream,
            ProfileId = baseStream.Metadata["expansionProfileId"]
        });
        expanded.Events.Metadata["definitionHash"] = baseStream.Metadata["definitionHash"];
        expanded.Events.Metadata["expander"] = nameof(SimulationBehaviorExpander);
        expanded.Events.Metadata["experimentMode"] = baseStream.Metadata["experimentMode"];
        expanded.Events.Metadata["expansionProfileId"] = baseStream.Metadata["expansionProfileId"];
        expanded.Events.Metadata["expansionProfileVersion"] = baseStream.Metadata["expansionProfileVersion"];
        expanded.Events.Metadata["expansionProfileHash"] = baseStream.Metadata["expansionProfileHash"];
        return expanded.Events;
    }

    private static SimulationEvent CreateEvent(SimulationScenarioDefinition definition, SimulationScheduledEventDefinition item, string eventKind, string eventId, TimeSpan offset, int order)
        => new()
        {
            EventId = eventId,
            EventKind = eventKind,
            ActorId = item.ActorIds.FirstOrDefault() ?? string.Empty,
            TargetActorId = item.ActorIds.Skip(1).FirstOrDefault() ?? item.TargetId,
            PlaceId = item.TargetLocationId,
            ResourceId = item.ResourceIds.FirstOrDefault() ?? string.Empty,
            ActorIds = [.. item.ActorIds],
            ResourceIds = [.. item.ResourceIds],
            SourceId = item.SourceId,
            TargetId = item.TargetId,
            OccurredAtUtc = definition.StartsAtUtc.AddTicks(definition.StepDuration.Ticks * item.StepIndex).Add(offset),
            Duration = item.Duration,
            Magnitude = item.Magnitude,
            Timing = new()
            {
                StepIndex = item.StepIndex,
                Offset = offset,
                Duration = item.Duration,
                Order = order
            },
            Effects = [.. item.Effects.Select(Clone)],
            TransactionLegs = [.. item.TransactionLegs.Select(Clone)],
            StoreResolutionPolicies = [.. item.StoreResolutionPolicies.Select(Clone)],
            Metadata = new Dictionary<string, string>(item.Metadata, StringComparer.Ordinal)
            {
                ["canonicalEventKind"] = eventKind,
                ["declaredEventKind"] = item.EventKind,
                ["sourceLocationId"] = item.SourceLocationId,
                ["targetLocationId"] = item.TargetLocationId
            }
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

    private static SimulationStoreResolutionPolicyDefinition Clone(SimulationStoreResolutionPolicyDefinition policy)
        => new()
        {
            Role = policy.Role,
            Policy = policy.Policy,
            StoreId = policy.StoreId,
            ActorId = policy.ActorId,
            LocationId = policy.LocationId,
            ResourceId = policy.ResourceId,
            PoolId = policy.PoolId,
            Metadata = new Dictionary<string, string>(policy.Metadata, StringComparer.Ordinal)
        };
}

public sealed class SimulationBehaviorExpander : ISimulationBehaviorExpander
{
    private readonly SimulationEventNormalizer eventNormalizer = new();

    public SimulationBehaviorExpansionResult Expand(SimulationBehaviorExpansionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseEvents);

        var result = new SimulationBehaviorExpansionResult
        {
            Events = new()
            {
                ScenarioId = request.BaseEvents.ScenarioId,
                Metadata = new Dictionary<string, string>(request.BaseEvents.Metadata, StringComparer.Ordinal)
            }
        };

        string profileId = SimulationBehaviorExpansionProfiles.Resolve(
            request.ProfileId,
            request.Definition.ExperimentMode);
        if (string.IsNullOrWhiteSpace(profileId))
        {
            result.Diagnostics.Add(new()
            {
                Severity = "error",
                Code = "missing-expansion-profile",
                Path = "scenario.expansionProfileId",
                Message = "Strict mode requires an explicit behavior expansion profile."
            });
            profileId = SimulationBehaviorExpansionProfiles.None;
        }

        SimulationBehaviorExpansionProfileDescriptor profile = SimulationBehaviorExpansionProfiles.Find(profileId)
            ?? SimulationBehaviorExpansionProfileDescriptor.Missing(profileId);
        result.Events.Metadata["expansionProfileId"] = profileId;
        result.Events.Metadata["expansionProfileVersion"] = profile.Version;
        result.Events.Metadata["expansionProfileHash"] = profile.Hash;
        foreach (SimulationEvent simulationEvent in Ordered(request.BaseEvents.Events.Select(item => eventNormalizer.Normalize(item).Event)))
        {
            result.Events.Events.AddRange(ExpandEvent(simulationEvent, profile));
        }

        result.Events.Events = [.. Ordered(result.Events.Events)];
        for (var index = 0; index < result.Events.Events.Count; index++)
        {
            result.Events.Events[index].Metadata["eventOrder"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static IEnumerable<SimulationEvent> ExpandEvent(SimulationEvent simulationEvent, SimulationBehaviorExpansionProfileDescriptor profile)
    {
        string canonicalKind = SimulationEventKindRegistry.Normalize(simulationEvent.EventKind);
        if (profile.ProfileId is SimulationBehaviorExpansionProfiles.None)
        {
            return [ClonePrimary(simulationEvent, canonicalKind, profile)];
        }

        if (canonicalKind is SimulationEventKind.ActorResourceNeed &&
            profile.ProfileId is SimulationBehaviorExpansionProfiles.SimpleNeed or SimulationBehaviorExpansionProfiles.AllV1)
        {
            return
            [
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTravel, "travel", 0, 0, "resource-need", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorResourceCollect, "collect", 1, 1, "resource-need", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorResourceUse, "use", 2, 2, "resource-need", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorReturnHome, "return", 3, 3, "resource-need", profile)
            ];
        }

        if (canonicalKind is SimulationEventKind.ActorResourceUse &&
            profile.ProfileId is SimulationBehaviorExpansionProfiles.SimpleNeed or SimulationBehaviorExpansionProfiles.AllV1)
        {
            return
            [
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTravel, "travel", 0, 0, "resource-use", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorResourceUse, "use", 1, 1, "resource-use", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorReturnHome, "return", 2, 2, "resource-use", profile)
            ];
        }

        if (canonicalKind is SimulationEventKind.ActorTradeSell &&
            profile.ProfileId is SimulationBehaviorExpansionProfiles.TradePolicyV1 or SimulationBehaviorExpansionProfiles.AllV1)
        {
            if (simulationEvent.TransactionLegs.Count > 0)
            {
                return
                [
                    Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorResourceCollect, "collect", 0, 0, "trade-policy", profile, suppressEconomicMutation: true, includeTransactionLegs: false),
                    Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTradeSell, "sell", 1, 1, "trade-policy", profile),
                    Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTradeBuy, "buy", 2, 2, "trade-policy", profile, suppressEconomicMutation: true, includeTransactionLegs: false),
                    Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorAdminPerform, "admin", 3, 3, "trade-policy", profile, suppressEconomicMutation: true, includeTransactionLegs: false),
                    Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTaxFeePay, "fee", 4, 4, "trade-policy", profile, suppressEconomicMutation: true, includeTransactionLegs: false),
                    Clone(simulationEvent, canonicalKind, SimulationEventKind.RuleCheckEvaluate, "rule-check", 5, 5, "trade-policy", profile, suppressEconomicMutation: true, includeTransactionLegs: false)
                ];
            }

            return
            [
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorResourceCollect, "collect", 0, 0, "trade-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTradeSell, "sell", 1, 1, "trade-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTradeBuy, "buy", 2, 2, "trade-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorAdminPerform, "admin", 3, 3, "trade-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorTaxFeePay, "fee", 4, 4, "trade-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.RuleCheckEvaluate, "rule-check", 5, 5, "trade-policy", profile)
            ];
        }

        if (canonicalKind is SimulationEventKind.RuleViolationDetect &&
            profile.ProfileId is SimulationBehaviorExpansionProfiles.TradePolicyV1 or SimulationBehaviorExpansionProfiles.AllV1)
        {
            return
            [
                Clone(simulationEvent, canonicalKind, SimulationEventKind.RuleViolationDetect, "violation", 0, 0, "rule-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.ActorAdminPerform, "admin", 1, 1, "rule-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.RuleEnforcementApply, "enforcement", 2, 2, "rule-policy", profile),
                Clone(simulationEvent, canonicalKind, SimulationEventKind.RelationshipConflictChange, "conflict", 3, 3, "rule-policy", profile)
            ];
        }

        return [ClonePrimary(simulationEvent, canonicalKind, profile)];
    }

    private static SimulationEvent ClonePrimary(SimulationEvent source, string eventKind, SimulationBehaviorExpansionProfileDescriptor profile)
        => new()
        {
            EventId = source.EventId,
            EventKind = eventKind,
            ActorId = source.ActorId,
            TargetActorId = source.TargetActorId,
            PlaceId = source.PlaceId,
            ResourceId = source.ResourceId,
            ActorIds = [.. source.ActorIds],
            ResourceIds = [.. source.ResourceIds],
            SourceId = source.SourceId,
            TargetId = source.TargetId,
            OccurredAtUtc = source.OccurredAtUtc,
            Duration = source.Duration,
            Magnitude = source.Magnitude,
            Timing = new()
            {
                StepIndex = source.Timing.StepIndex,
                Offset = source.Timing.Offset,
                Duration = source.Timing.Duration,
                Order = source.Timing.Order
            },
            Participants = [.. source.Participants],
            Effects = [.. source.Effects.Select(Clone)],
            TransactionLegs = [.. source.TransactionLegs.Select(Clone)],
            StoreResolutionPolicies = [.. source.StoreResolutionPolicies],
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal)
            {
                ["canonicalEventKind"] = eventKind,
                ["expansion"] = "explicit",
                ["phase"] = "primary",
                ["expansionProfileId"] = profile.ProfileId,
                ["expansionProfileVersion"] = profile.Version,
                ["expansionProfileHash"] = profile.Hash,
                ["parentEventId"] = source.EventId,
                ["expansionRuleHash"] = SimulationDeterministicHash.HashBehaviorExpansionRule(profile, eventKind, "explicit", "primary", eventKind)
            }
        };

    private static SimulationEvent Clone(
        SimulationEvent source,
        string sourceEventKind,
        string eventKind,
        string phase,
        int offsetMinutes,
        int orderOffset,
        string expansion,
        SimulationBehaviorExpansionProfileDescriptor profile,
        bool suppressEconomicMutation = false,
        bool includeTransactionLegs = true)
    {
        TimeSpan offset = source.Timing.Offset.Add(TimeSpan.FromMinutes(offsetMinutes));
        string placeId = phase is "return" && source.Metadata.TryGetValue("sourceLocationId", out string? sourceLocationId)
            ? sourceLocationId
            : source.PlaceId;
        string targetId = phase is "return" && !string.IsNullOrWhiteSpace(source.ActorId)
            ? source.ActorId
            : source.TargetId;

        var clone = new SimulationEvent
        {
            EventId = $"{source.EventId}.{phase}",
            EventKind = eventKind,
            ActorId = source.ActorId,
            TargetActorId = source.TargetActorId,
            PlaceId = placeId,
            ResourceId = source.ResourceId,
            ActorIds = [.. source.ActorIds],
            ResourceIds = [.. source.ResourceIds],
            SourceId = source.SourceId,
            TargetId = targetId,
            OccurredAtUtc = source.OccurredAtUtc.Add(offset - source.Timing.Offset),
            Duration = source.Duration,
            Magnitude = source.Magnitude,
            Timing = new()
            {
                StepIndex = source.Timing.StepIndex,
                Offset = offset,
                Duration = source.Timing.Duration,
                Order = source.Timing.Order + orderOffset
            },
            Participants = [.. source.Participants],
            Effects = [.. source.Effects.Select(Clone)],
            TransactionLegs = includeTransactionLegs ? [.. source.TransactionLegs.Select(Clone)] : [],
            StoreResolutionPolicies = [.. source.StoreResolutionPolicies],
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal)
            {
                ["canonicalEventKind"] = eventKind,
                ["expansion"] = expansion,
                ["phase"] = phase,
                ["sourceEventId"] = source.EventId,
                ["parentEventId"] = source.EventId,
                ["expansionProfileId"] = profile.ProfileId,
                ["expansionProfileVersion"] = profile.Version,
                ["expansionProfileHash"] = profile.Hash,
                ["expansionRuleHash"] = SimulationDeterministicHash.HashBehaviorExpansionRule(profile, sourceEventKind, expansion, phase, eventKind)
            }
        };

        if (suppressEconomicMutation)
        {
            clone.Metadata["economicMutationSuppressed"] = "true";
            clone.Metadata["transactionLegsOwnerPhase"] = "sell";
        }

        return clone;
    }

    private static IOrderedEnumerable<SimulationEvent> Ordered(IEnumerable<SimulationEvent> events)
        => events
            .OrderBy(static item => item.Timing.StepIndex)
            .ThenBy(static item => item.Timing.Offset)
            .ThenBy(static item => item.Timing.Order)
            .ThenBy(static item => SimulationEventKindRegistry.OrderKey(item.EventKind))
            .ThenBy(static item => item.EventId, StringComparer.Ordinal);

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
}
