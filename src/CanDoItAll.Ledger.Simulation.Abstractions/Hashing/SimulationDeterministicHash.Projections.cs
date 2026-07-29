namespace CanDoItAll.Ledger.Simulation.Abstractions;

public static partial class SimulationDeterministicHash
{
    private static object ProjectRun(SimulationRunIdentity run)
        => new { run.RunId, run.ScenarioId, run.BackendId, run.CreatedAtUtc, Metadata = StableMetadata(run.Metadata) };

    private static object ProjectActor(SimulationActor actor)
        => new { actor.ActorId, actor.Kind, actor.DisplayName, actor.Role, Metadata = StableMetadata(actor.Metadata) };

    private static object ProjectStore(SimulationResourceStore store)
        => new { store.StoreId, store.OwnerActorId, store.ResourceId, store.ResourceKind, store.DisplayName, store.Quantity, store.Unit, store.Capacity, Metadata = StableMetadata(store.Metadata) };

    private static object ProjectFlow(SimulationResourceFlow flow)
        => new { flow.FlowId, flow.FromStoreId, flow.ToStoreId, flow.FromActorId, flow.ToActorId, flow.ResourceId, flow.Quantity, flow.Unit, flow.Reason, flow.OccurredAtUtc, Metadata = StableMetadata(flow.Metadata) };

    private static object ProjectRelationship(SimulationRelationship relationship)
        => new { relationship.RelationshipId, relationship.SourceActorId, relationship.TargetActorId, relationship.Kind, relationship.Strength, Metadata = StableMetadata(relationship.Metadata) };

    private static object ProjectEvent(SimulationEvent item)
        => new
        {
            item.EventId,
            item.EventKind,
            item.ActorId,
            item.TargetActorId,
            item.PlaceId,
            item.ResourceId,
            ActorIds = item.ActorIds.OrderBy(id => id, StringComparer.Ordinal),
            ResourceIds = item.ResourceIds.OrderBy(id => id, StringComparer.Ordinal),
            item.SourceId,
            item.TargetId,
            Source = ProjectRef(item.Source),
            Target = ProjectRef(item.Target),
            ParticipantRefs = item.ParticipantRefs.OrderBy(participant => participant.Kind, StringComparer.Ordinal).ThenBy(participant => participant.Id, StringComparer.Ordinal).Select(ProjectRef),
            item.OccurredAtUtc,
            item.Duration,
            item.Magnitude,
            item.Timing,
            Participants = item.Participants.OrderBy(participant => participant.ParticipantId, StringComparer.Ordinal),
            Effects = item.Effects.OrderBy(effect => effect.EffectKind, StringComparer.Ordinal).ThenBy(effect => effect.TargetId, StringComparer.Ordinal),
            TransactionLegs = item.TransactionLegs
                .OrderBy(leg => leg.LegId, StringComparer.Ordinal)
                .ThenBy(leg => leg.LegKind, StringComparer.Ordinal)
                .ThenBy(leg => leg.ResourceId, StringComparer.Ordinal)
                .Select(ProjectTransactionLeg),
            StoreResolutionPolicies = item.StoreResolutionPolicies
                .OrderBy(policy => policy.Role, StringComparer.Ordinal)
                .ThenBy(policy => policy.StoreId, StringComparer.Ordinal)
                .ThenBy(policy => policy.ResourceId, StringComparer.Ordinal)
                .Select(ProjectStoreResolutionPolicy),
            Metadata = StableMetadata(item.Metadata)
        };

    private static object ProjectTransactionLeg(SimulationTransactionLegDefinition leg)
        => new
        {
            leg.LegId,
            leg.LegKind,
            leg.Direction,
            leg.FromActorId,
            leg.ToActorId,
            leg.FromStoreId,
            leg.ToStoreId,
            leg.ResourceId,
            leg.Quantity,
            leg.UnitPrice,
            leg.Reason,
            Metadata = StableMetadata(leg.Metadata)
        };

    private static object ProjectStoreResolutionPolicy(SimulationStoreResolutionPolicyDefinition policy)
        => new
        {
            policy.Role,
            policy.Policy,
            policy.StoreId,
            policy.ActorId,
            policy.LocationId,
            policy.ResourceId,
            policy.PoolId,
            Metadata = StableMetadata(policy.Metadata)
        };

    private static object ProjectRule(SimulationRuleRef rule)
        => new { rule.RuleId, rule.DisplayName, rule.Category, Metadata = StableMetadata(rule.Metadata) };

    private static object ProjectIssue(SimulationIssue issue)
        => new
        {
            issue.IssueId,
            issue.Severity,
            issue.Category,
            issue.Title,
            issue.Description,
            issue.IsResolved,
            ActorIds = issue.ActorIds.OrderBy(id => id, StringComparer.Ordinal),
            ResourceIds = issue.ResourceIds.OrderBy(id => id, StringComparer.Ordinal),
            Metadata = StableMetadata(issue.Metadata)
        };

    private static object ProjectArtifact(SimulationArtifactRef artifact)
        => new { artifact.ArtifactId, artifact.Kind, artifact.Uri, artifact.DisplayName, Metadata = StableMetadata(artifact.Metadata) };

    private static object ProjectScenarioExtension(SimulationScenarioExtensionDefinition extension)
        => new
        {
            extension.ExtensionId,
            extension.ExtensionKind,
            extension.SchemaId,
            extension.SchemaVersion,
            extension.SchemaHash,
            extension.RegistryId,
            extension.Version,
            Functions = extension.Functions
                .OrderBy(function => function.FunctionId, StringComparer.Ordinal)
                .Select(function => new
                {
                    function.FunctionId,
                    function.FunctionKind,
                    function.Direction,
                    ResourceIds = function.ResourceIds.OrderBy(id => id, StringComparer.Ordinal),
                    function.MinQuantity,
                    function.MaxQuantity,
                    ActorIds = function.ActorIds.OrderBy(id => id, StringComparer.Ordinal),
                    LocationIds = function.LocationIds.OrderBy(id => id, StringComparer.Ordinal),
                    BoundaryIds = function.BoundaryIds.OrderBy(id => id, StringComparer.Ordinal),
                    function.OwnerKind,
                    function.OwnerId,
                    Metadata = StableMetadata(function.Metadata)
                }),
            Metadata = StableMetadata(extension.Metadata)
        };

    private static object ProjectBehaviorExpansionProfile(SimulationBehaviorExpansionProfileDescriptor profile)
        => new
        {
            profile.ProfileId,
            profile.Version,
            profile.RuleSetId,
            profile.Hash,
            RuleFingerprints = profile.RuleFingerprints.OrderBy(item => item, StringComparer.Ordinal)
        };

    private static object ProjectRef(SimulationRef item)
        => new { item.Kind, item.Id, Metadata = StableMetadata(item.Metadata) };

    private static object ProjectMetric(SimulationMetricValue metric)
        => new { metric.MetricId, metric.Value, metric.Unit, Metadata = StableMetadata(metric.Metadata) };

    private static object ProjectInvariant(SimulationInvariantEvaluationResult invariant)
        => new { invariant.InvariantId, invariant.Passed, invariant.Severity, invariant.Message, Metadata = StableMetadata(invariant.Metadata) };
}
