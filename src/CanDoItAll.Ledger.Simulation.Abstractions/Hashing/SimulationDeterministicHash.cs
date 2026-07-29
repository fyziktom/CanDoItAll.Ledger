using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CanDoItAll.Ledger.Simulation.Abstractions;

public static partial class SimulationDeterministicHash
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] ExcludedMetadataKeys =
    [
        "playbackSpeed",
        "playback.speed",
        "ui.playbackSpeed",
        "ui.playback.speed"
    ];

    public static string HashManifest(SimulationScenarioManifest manifest)
        => HashObject(new
        {
            manifest.ScenarioId,
            manifest.DisplayName,
            manifest.ScenarioKind,
            manifest.BaseCurrency,
            manifest.StepCount,
            manifest.StartsAtUtc,
            manifest.StepDuration,
            manifest.ExperimentMode,
            manifest.ExpansionProfileId,
            manifest.ExpansionProfileVersion,
            manifest.ExpansionProfileHash,
            Rules = manifest.Rules.OrderBy(item => item.RuleId, StringComparer.Ordinal).Select(ProjectRule),
            Artifacts = manifest.Artifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).Select(ProjectArtifact),
            Metadata = StableMetadata(manifest.Metadata)
        });

    public static string HashBehaviorExpansionProfile(SimulationBehaviorExpansionProfileDescriptor profile)
        => HashObject(new
        {
            profile.ProfileId,
            profile.Version,
            profile.RuleSetId,
            RuleFingerprints = profile.RuleFingerprints.OrderBy(item => item, StringComparer.Ordinal)
        });

    public static string HashBehaviorExpansionRule(
        SimulationBehaviorExpansionProfileDescriptor profile,
        string sourceEventKind,
        string expansion,
        string phase,
        string eventKind)
        => HashObject(new
        {
            profile.ProfileId,
            profile.Version,
            profile.Hash,
            SourceEventKind = sourceEventKind,
            Expansion = expansion,
            Phase = phase,
            EventKind = eventKind
        });

    public static string HashDefinition(SimulationScenarioDefinition definition)
    {
        definition = new SimulationScenarioDefinitionNormalizer().Normalize(definition).Definition;
        SimulationBehaviorExpansionProfileDescriptor profile = SimulationBehaviorExpansionProfiles.ResolveDescriptor(definition.ExpansionProfileId, definition.ExperimentMode);
        return HashObject(new
        {
            definition.ScenarioId,
            definition.DisplayName,
            definition.ScenarioKind,
            definition.BaseCurrency,
            definition.ExperimentMode,
            ExpansionProfile = ProjectBehaviorExpansionProfile(profile),
            definition.StartsAtUtc,
            definition.StepCount,
            definition.StepDuration,
            definition.Seed,
            Actors = definition.Actors.OrderBy(item => item.ActorId, StringComparer.Ordinal),
            Resources = definition.Resources.OrderBy(item => item.ResourceId, StringComparer.Ordinal),
            Locations = definition.Locations.OrderBy(item => item.LocationId, StringComparer.Ordinal),
            Objects = definition.Objects.OrderBy(item => item.ObjectId, StringComparer.Ordinal),
            InitialStores = definition.InitialStores.OrderBy(item => item.StoreId, StringComparer.Ordinal),
            Behaviors = definition.Behaviors.OrderBy(item => item.BehaviorId, StringComparer.Ordinal),
            Relationships = definition.Relationships.OrderBy(item => item.RelationshipId, StringComparer.Ordinal),
            ScheduledEvents = definition.ScheduledEvents.OrderBy(item => item.ScheduledEventId, StringComparer.Ordinal),
            Rules = definition.Rules.OrderBy(item => item.RuleId, StringComparer.Ordinal).Select(ProjectRule),
            RuleDefinitions = definition.RuleDefinitions.OrderBy(item => item.RuleId, StringComparer.Ordinal),
            Extensions = definition.Extensions.Count == 0
                ? null
                : definition.Extensions
                    .OrderBy(item => item.ExtensionId, StringComparer.Ordinal)
                    .ThenBy(item => item.RegistryId, StringComparer.Ordinal)
                    .Select(ProjectScenarioExtension),
            definition.Schedule,
            BackendHints = definition.BackendHints.OrderBy(item => item.BackendId, StringComparer.Ordinal),
            Metadata = StableMetadata(definition.Metadata)
        });
    }

    public static string HashScenarioExtensionDefinition(SimulationScenarioExtensionDefinition extension)
        => HashObject(ProjectScenarioExtension(extension));

    public static string HashInputPack(SimulationExperimentInputPack pack)
        => HashObject(new
        {
            pack.SchemaVersion,
            pack.ExperimentId,
            pack.DisplayName,
            pack.Hypothesis,
            pack.Treatment,
            Inputs = pack.Inputs.OrderBy(input => input.Kind, StringComparer.Ordinal).ThenBy(input => input.Path, StringComparer.Ordinal).Select(input => new
            {
                input.Kind,
                input.Path,
                input.ContentHash,
                input.SchemaVersion,
                Metadata = StableMetadata(input.Metadata)
            }),
            InputHashes = new SortedDictionary<string, string>(pack.Hashes.InputHashes, StringComparer.Ordinal),
            Metadata = StableMetadata(pack.Metadata)
        });

    public static string HashPlacement(SimulationPlacementDefinition placement)
        => HashObject(new
        {
            placement.SchemaVersion,
            placement.PlacementId,
            placement.ScenarioId,
            placement.CoordinateSystem,
            Entities = placement.Entities.OrderBy(entity => entity.RefKind, StringComparer.Ordinal).ThenBy(entity => entity.RefId, StringComparer.Ordinal).Select(entity => new
            {
                entity.RefKind,
                entity.RefId,
                entity.X,
                entity.Y,
                entity.Z,
                Metadata = StableMetadata(entity.Metadata)
            }),
            Topology = placement.Topology.OrderBy(edge => edge.EdgeId, StringComparer.Ordinal).Select(edge => new
            {
                edge.EdgeId,
                edge.SourceRefKind,
                edge.SourceRefId,
                edge.TargetRefKind,
                edge.TargetRefId,
                edge.Distance,
                Metadata = StableMetadata(edge.Metadata)
            }),
            GeneratedBy = placement.GeneratedBy is null ? null : new
            {
                placement.GeneratedBy.GeneratorId,
                placement.GeneratedBy.GeneratorVersion,
                placement.GeneratedBy.Seed,
                placement.GeneratedBy.GeneratedAtUtc,
                placement.GeneratedBy.RequestHash
            },
            Metadata = StableMetadata(placement.Metadata)
        });

    public static string HashParameterSet(SimulationParameterSetDefinition parameters)
        => HashObject(new
        {
            parameters.SchemaVersion,
            parameters.ParameterSetId,
            parameters.ScenarioId,
            ActorParameters = parameters.ActorParameters.OrderBy(actor => actor.ActorId, StringComparer.Ordinal).Select(actor => new
            {
                actor.ActorId,
                ResourceRequirements = actor.ResourceRequirements.OrderBy(requirement => requirement.ResourceId, StringComparer.Ordinal).ThenBy(requirement => requirement.Period, StringComparer.Ordinal),
                actor.CarryCapacity,
                actor.TravelSpeed,
                actor.TravelCost,
                actor.StorageCapacity,
                actor.AdminTimeCost,
                Metadata = StableMetadata(actor.Metadata)
            }),
            ResourceParameters = parameters.ResourceParameters.OrderBy(resource => resource.ResourceId, StringComparer.Ordinal).Select(resource => new
            {
                resource.ResourceId,
                resource.StorageCapacity,
                resource.ExternalDemand,
                resource.ResaleMarkup,
                Metadata = StableMetadata(resource.Metadata)
            }),
            RuleParameters = parameters.RuleParameters.OrderBy(rule => rule.RuleId, StringComparer.Ordinal).Select(rule => new
            {
                rule.RuleId,
                ResourceLimits = rule.ResourceLimits.OrderBy(limit => limit.ResourceId, StringComparer.Ordinal).ThenBy(limit => limit.Period, StringComparer.Ordinal),
                rule.TaxRate,
                rule.FeeRate,
                rule.AdminMinutesPerTrade,
                rule.EnforcementThreshold,
                Metadata = StableMetadata(rule.Metadata)
            }),
            Metadata = StableMetadata(parameters.Metadata)
        });

    public static string HashInputGenerationRequest(SimulationInputGenerationRequest request)
        => HashObject(new
        {
            request.GeneratorId,
            request.GeneratorVersion,
            request.Seed,
            request.ScenarioId,
            Entities = request.Entities.OrderBy(entity => entity.Kind, StringComparer.Ordinal).ThenBy(entity => entity.Id, StringComparer.Ordinal).Select(ProjectRef),
            Metadata = StableMetadata(request.Metadata)
        });

    public static string HashFrame(SimulationFrame frame)
        => HashObject(new
        {
            frame.FrameId,
            Run = ProjectRun(frame.Run),
            Clock = frame.Clock,
            Actors = frame.Actors.OrderBy(item => item.ActorId, StringComparer.Ordinal).Select(ProjectActor),
            ResourceStores = frame.ResourceStores.OrderBy(item => item.StoreId, StringComparer.Ordinal).Select(ProjectStore),
            Flows = frame.Flows.OrderBy(item => item.FlowId, StringComparer.Ordinal).Select(ProjectFlow),
            Relationships = frame.Relationships.OrderBy(item => item.RelationshipId, StringComparer.Ordinal).Select(ProjectRelationship),
            Events = frame.Events.OrderBy(item => item.EventId, StringComparer.Ordinal).Select(ProjectEvent),
            Rules = frame.Rules.OrderBy(item => item.RuleId, StringComparer.Ordinal).Select(ProjectRule),
            Issues = frame.Issues.OrderBy(item => item.IssueId, StringComparer.Ordinal).Select(ProjectIssue),
            Artifacts = frame.Artifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).Select(ProjectArtifact),
            Metadata = StableMetadata(frame.Metadata)
        });

    public static string HashDelta(SimulationFrameDelta delta)
        => HashObject(new
        {
            delta.DeltaId,
            delta.RunId,
            delta.FromStepIndex,
            delta.ToStepIndex,
            UpdatedResourceStores = delta.UpdatedResourceStores.OrderBy(item => item.StoreId, StringComparer.Ordinal).Select(ProjectStore),
            AddedFlows = delta.AddedFlows.OrderBy(item => item.FlowId, StringComparer.Ordinal).Select(ProjectFlow),
            UpdatedRelationships = delta.UpdatedRelationships.OrderBy(item => item.RelationshipId, StringComparer.Ordinal).Select(ProjectRelationship),
            AddedEvents = delta.AddedEvents.OrderBy(item => item.EventId, StringComparer.Ordinal).Select(ProjectEvent),
            AddedIssues = delta.AddedIssues.OrderBy(item => item.IssueId, StringComparer.Ordinal).Select(ProjectIssue),
            AddedArtifacts = delta.AddedArtifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).Select(ProjectArtifact),
            ResolvedIssueIds = delta.ResolvedIssueIds.OrderBy(id => id, StringComparer.Ordinal),
            Metadata = StableMetadata(delta.Metadata)
        });

    public static string HashMetric(SimulationMetricValue metric)
        => HashObject(ProjectMetric(metric));

    public static string HashInvariant(SimulationInvariantEvaluationResult invariant)
        => HashObject(ProjectInvariant(invariant));

    private static string HashObject(object value)
    {
        string canonicalJson = ToCanonicalJson(value);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToCanonicalJson(object value)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(value, JsonOptions);
        return Canonicalize(node)?.ToJsonString(JsonOptions) ?? "null";
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var sorted = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> property in obj.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                sorted[property.Key] = Canonicalize(property.Value);
            }

            return sorted;
        }

        if (node is JsonArray array)
        {
            var sorted = new JsonArray();
            foreach (JsonNode? item in array)
            {
                sorted.Add(Canonicalize(item));
            }

            return sorted;
        }

        return node is null ? null : JsonNode.Parse(node.ToJsonString(JsonOptions));
    }

    private static SortedDictionary<string, string> StableMetadata(Dictionary<string, string> metadata)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> item in metadata.Where(item => !ExcludedMetadataKeys.Contains(item.Key, StringComparer.OrdinalIgnoreCase)))
        {
            sorted[item.Key] = item.Value;
        }

        return sorted;
    }

}
