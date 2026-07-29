namespace CanDoItAll.Ledger.Simulation.Abstractions;

public enum SimulationExperimentMode
{
    Exploratory = 0,
    Strict = 1,
    ResearchGrade = 2
}

public static class SimulationExperimentModeNames
{
    public const string Exploratory = "exploratory";
    public const string Strict = "strict";
    public const string ResearchGrade = "research-grade";

    public static string ToName(SimulationExperimentMode mode)
        => mode switch
        {
            SimulationExperimentMode.Strict => Strict,
            SimulationExperimentMode.ResearchGrade => ResearchGrade,
            _ => Exploratory
        };

    public static SimulationExperimentMode Parse(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            Strict => SimulationExperimentMode.Strict,
            "research" or ResearchGrade => SimulationExperimentMode.ResearchGrade,
            _ => SimulationExperimentMode.Exploratory
        };
}

public sealed class SimulationExperimentPolicy
{
    public string PolicyId { get; init; } = "exploratory-demo";
    public SimulationExperimentMode Mode { get; init; } = SimulationExperimentMode.Exploratory;
    public bool StrictMode { get; init; }
    public bool VerifyDocumentReferences { get; init; } = true;
    public bool VerifyDocumentContentHashes { get; init; }
    public bool VerifyPackHash { get; init; }
    public bool FailOnUnknownEvent { get; init; }
    public bool FailOnInsufficientStock { get; init; }
    public bool FailOnMissingReference { get; init; }
    public bool FailOnAmbiguousStore { get; init; }
    public bool FailOnUnknownMetric { get; init; }
    public bool FailOnUnknownInvariant { get; init; }
    public bool ResearchClaimsAllowed { get; init; }
    public int WarningBudget { get; init; } = int.MaxValue;
    public List<string> AllowlistedWarningCodes { get; init; } = [];

    public bool StrictSemanticMode => Mode is SimulationExperimentMode.Strict or SimulationExperimentMode.ResearchGrade;

    public static SimulationExperimentPolicy ExploratoryDemo => new()
    {
        PolicyId = "exploratory-demo",
        Mode = SimulationExperimentMode.Exploratory,
        VerifyDocumentReferences = true,
        WarningBudget = int.MaxValue
    };

    public static SimulationExperimentPolicy Strict => new()
    {
        PolicyId = "strict",
        Mode = SimulationExperimentMode.Strict,
        StrictMode = true,
        VerifyDocumentReferences = true,
        VerifyDocumentContentHashes = true,
        VerifyPackHash = true,
        FailOnUnknownEvent = true,
        FailOnInsufficientStock = true,
        FailOnMissingReference = true,
        FailOnAmbiguousStore = true,
        FailOnUnknownMetric = true,
        FailOnUnknownInvariant = true,
        WarningBudget = 0
    };

    public static SimulationExperimentPolicy ResearchStrict => new()
    {
        PolicyId = "research-strict",
        Mode = SimulationExperimentMode.ResearchGrade,
        StrictMode = true,
        VerifyDocumentReferences = true,
        VerifyDocumentContentHashes = true,
        VerifyPackHash = true,
        FailOnUnknownEvent = true,
        FailOnInsufficientStock = true,
        FailOnMissingReference = true,
        FailOnAmbiguousStore = true,
        FailOnUnknownMetric = true,
        FailOnUnknownInvariant = true,
        ResearchClaimsAllowed = true,
        WarningBudget = 0
    };

    public static SimulationExperimentPolicy FromLegacyOptions(SimulationExperimentMode mode, bool strictMode)
    {
        if (mode is SimulationExperimentMode.ResearchGrade)
        {
            return ResearchStrict;
        }

        if (mode is SimulationExperimentMode.Strict)
        {
            return Strict;
        }

        SimulationExperimentPolicy exploratory = ExploratoryDemo;
        if (!strictMode)
        {
            return exploratory;
        }

        return new()
        {
            PolicyId = "strict-pack-validation",
            Mode = SimulationExperimentMode.Exploratory,
            StrictMode = true,
            VerifyDocumentReferences = true,
            VerifyDocumentContentHashes = true,
            VerifyPackHash = true,
            WarningBudget = int.MaxValue
        };
    }
}

public static class SimulationBehaviorExpansionProfiles
{
    public const string None = "none";
    public const string SimpleNeed = "simple-need";
    public const string TradePolicyV1 = "trade-policy-v1";
    public const string AllV1 = "all-v1";

    private static readonly Dictionary<string, SimulationBehaviorExpansionProfileDescriptor> Descriptors = new(StringComparer.Ordinal)
    {
        [None] = new(None, "v1", "explicit-only/v1", ["primary:explicit"]),
        [SimpleNeed] = new(SimpleNeed, "v1", "simple-need/v1",
        [
            $"{SimulationEventKind.ActorResourceNeed}:travel,collect,use,return",
            $"{SimulationEventKind.ActorResourceUse}:travel,use,return"
        ]),
        [TradePolicyV1] = new(TradePolicyV1, "v1", "trade-policy/v1",
        [
            $"{SimulationEventKind.ActorTradeSell}:collect,sell,buy,admin,fee,rule-check",
            $"{SimulationEventKind.RuleViolationDetect}:violation,admin,enforcement,conflict"
        ]),
        [AllV1] = new(AllV1, "v1", "all/v1",
        [
            $"{SimulationEventKind.ActorResourceNeed}:travel,collect,use,return",
            $"{SimulationEventKind.ActorResourceUse}:travel,use,return",
            $"{SimulationEventKind.ActorTradeSell}:collect,sell,buy,admin,fee,rule-check",
            $"{SimulationEventKind.RuleViolationDetect}:violation,admin,enforcement,conflict"
        ])
    };

    public static bool IsKnown(string? profileId)
        => !string.IsNullOrWhiteSpace(profileId) && Descriptors.ContainsKey(profileId);

    public static string Resolve(string? profileId, SimulationExperimentMode mode)
        => !string.IsNullOrWhiteSpace(profileId)
            ? profileId
            : mode is SimulationExperimentMode.Exploratory ? AllV1 : string.Empty;

    public static SimulationBehaviorExpansionProfileDescriptor? Find(string? profileId)
        => !string.IsNullOrWhiteSpace(profileId) && Descriptors.TryGetValue(profileId, out SimulationBehaviorExpansionProfileDescriptor? descriptor)
            ? descriptor
            : null;

    public static SimulationBehaviorExpansionProfileDescriptor ResolveDescriptor(string? profileId, SimulationExperimentMode mode)
    {
        string resolved = Resolve(profileId, mode);
        return Find(resolved) ?? SimulationBehaviorExpansionProfileDescriptor.Missing(resolved);
    }
}

public sealed record SimulationBehaviorExpansionProfileDescriptor(
    string ProfileId,
    string Version,
    string RuleSetId,
    IReadOnlyList<string> RuleFingerprints)
{
    public string Hash => SimulationDeterministicHash.HashBehaviorExpansionProfile(this);

    public static SimulationBehaviorExpansionProfileDescriptor Missing(string profileId)
        => new(profileId, string.Empty, string.Empty, []);
}
