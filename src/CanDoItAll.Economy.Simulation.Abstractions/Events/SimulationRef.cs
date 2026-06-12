namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationRef
{
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];

    public static SimulationRef Create(string kind, string id)
        => new() { Kind = kind, Id = id };
}

public static class SimulationRefKinds
{
    public const string Actor = "actor";
    public const string Resource = "resource";
    public const string Store = "store";
    public const string Location = "location";
    public const string Object = "object";
    public const string Rule = "rule";
    public const string Market = "market";
    public const string Institution = "institution";
}

public static class SimulationCanonicalEventKind
{
    public const string ResourceCollect = "resource.collect";
    public const string ResourceUse = "resource.use";
    public const string ResourceTransfer = "resource.transfer";
    public const string ActorTravel = "actor.travel";
    public const string ActorReturn = "actor.return";
    public const string ActorWork = "actor.work";
    public const string ActorAdmin = "actor.admin";
    public const string MarketTrade = "market.trade";
    public const string MarketLoan = "market.loan";
    public const string MarketRepayment = "market.repayment";
    public const string RuleCheck = "rule.check";
    public const string RuleViolation = "rule.violation";
    public const string RuleEnforcement = "rule.enforcement";
    public const string RelationshipChange = "relationship.change";
}
