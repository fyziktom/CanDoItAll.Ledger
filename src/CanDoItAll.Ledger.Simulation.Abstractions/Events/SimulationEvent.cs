namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationEvent
{
    public string EventId { get; set; } = string.Empty;

    public string Kind
    {
        get => EventKind;
        set => EventKind = value;
    }

    public string EventKind { get; set; } = string.Empty;

    public int StepIndex
    {
        get => Timing.StepIndex;
        set => Timing.StepIndex = value;
    }

    public string ActorId { get; set; } = string.Empty;

    public string TargetActorId { get; set; } = string.Empty;

    public string PlaceId { get; set; } = string.Empty;

    public string ResourceId { get; set; } = string.Empty;

    public decimal Quantity
    {
        get => Magnitude;
        set => Magnitude = value;
    }

    public double DurationSeconds
    {
        get => Duration.TotalSeconds;
        set => Duration = TimeSpan.FromSeconds(value);
    }

    public List<string> ActorIds { get; set; } = [];
    public List<string> ResourceIds { get; set; } = [];
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public SimulationRef Source { get; set; } = new();
    public SimulationRef Target { get; set; } = new();
    public List<SimulationRef> ParticipantRefs { get; set; } = [];
    public DateTimeOffset OccurredAtUtc { get; set; }
    public TimeSpan Duration { get; set; }
    public decimal Magnitude { get; set; }
    public SimulationEventTiming Timing { get; set; } = new();
    public List<SimulationEventParticipant> Participants { get; set; } = [];
    public List<SimulationEventEffect> Effects { get; set; } = [];
    public List<SimulationTransactionLegDefinition> TransactionLegs { get; set; } = [];
    public List<SimulationStoreResolutionPolicyDefinition> StoreResolutionPolicies { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationTransactionLegDefinition
{
    public string LegId { get; set; } = string.Empty;
    public string LegKind { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string FromActorId { get; set; } = string.Empty;
    public string ToActorId { get; set; } = string.Empty;
    public string FromStoreId { get; set; } = string.Empty;
    public string ToStoreId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationEventEffect
{
    public string EffectKind { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationEventParticipant
{
    public string ParticipantId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationEventTiming
{
    public int StepIndex { get; set; }
    public TimeSpan Offset { get; set; }
    public TimeSpan Duration { get; set; }
    public int Order { get; set; }
}

public static class SimulationEventKind
{
    public const string ActorResourceCollect = "actor.resource.collect";
    public const string ActorResourceNeed = "actor.resource.need";
    public const string ActorResourceInventoryBuild = "actor.resource.inventory-build";
    public const string ActorResourceUse = "actor.resource.use";
    public const string ActorResourceTransfer = "actor.resource.transfer";
    public const string ActorResourceContribution = "actor.resource.contribution";
    public const string ActorObligationCreate = "actor.obligation.create";
    public const string ActorClaimIssue = "actor.claim.issue";
    public const string ActorReturnPay = "actor.return.pay";
    public const string ActorTradeSell = "actor.trade.sell";
    public const string ActorTradeBuy = "actor.trade.buy";
    public const string ActorTravel = "actor.travel";
    public const string ActorReturnHome = "actor.return-home";
    public const string ActorAdminPerform = "actor.admin.perform";
    public const string ActorWorkPerform = "actor.work.perform";
    public const string ActorTaxFeePay = "actor.tax-fee.pay";
    public const string RuleCheckEvaluate = "rule.check.evaluate";
    public const string RuleViolationDetect = "rule.violation.detect";
    public const string RuleEnforcementApply = "rule.enforcement.apply";
    public const string RelationshipTrustChange = "relationship.trust.change";
    public const string RelationshipConflictChange = "relationship.conflict.change";
    public const string ResourceStockChange = "resource.stock.change";
    public const string ResourceScarcitySignal = "resource.scarcity.signal";

    public const string ResourceUse = "resource-use";
    public const string ResourceTransfer = "resource-transfer";
    public const string ActorMoveIntention = "actor-move-intention";
    public const string ActorWork = "actor-work";
    public const string ActorAdmin = "actor-admin";
    public const string StoreChanged = "store-changed";
    public const string RelationshipChanged = "relationship-changed";
    public const string IssueRaised = "issue-raised";
    public const string IssueResolved = "issue-resolved";
    public const string RuleApplied = "rule-applied";
    public const string Travel = "travel";
    public const string ReturnHome = "return-home";
    public const string Work = "work";
    public const string Administration = "administration";
    public const string Trade = "trade";
    public const string Loan = "loan";
    public const string Repayment = "repayment";
    public const string RuleCheck = "rule-check";
    public const string RuleViolation = "rule-violation";
    public const string RuleEnforcement = "rule-enforcement";
    public const string RelationshipChange = "relationship-change";
    public const string Maintenance = "maintenance";
    public const string Production = "production";
    public const string Consumption = "consumption";
}

public static class SimulationEventKinds
{
    public const string ActorResourceCollect = SimulationEventKind.ActorResourceCollect;
    public const string ActorResourceNeed = SimulationEventKind.ActorResourceNeed;
    public const string ActorResourceInventoryBuild = SimulationEventKind.ActorResourceInventoryBuild;
    public const string ActorResourceUse = SimulationEventKind.ActorResourceUse;
    public const string ActorResourceTransfer = SimulationEventKind.ActorResourceTransfer;
    public const string ActorResourceContribution = SimulationEventKind.ActorResourceContribution;
    public const string ActorObligationCreate = SimulationEventKind.ActorObligationCreate;
    public const string ActorClaimIssue = SimulationEventKind.ActorClaimIssue;
    public const string ActorReturnPay = SimulationEventKind.ActorReturnPay;
    public const string ActorTradeSell = SimulationEventKind.ActorTradeSell;
    public const string ActorTradeBuy = SimulationEventKind.ActorTradeBuy;
    public const string ActorTravel = SimulationEventKind.ActorTravel;
    public const string ActorReturnHome = SimulationEventKind.ActorReturnHome;
    public const string ActorAdminPerform = SimulationEventKind.ActorAdminPerform;
    public const string ActorWorkPerform = SimulationEventKind.ActorWorkPerform;
    public const string ActorTaxFeePay = SimulationEventKind.ActorTaxFeePay;
    public const string RuleCheckEvaluate = SimulationEventKind.RuleCheckEvaluate;
    public const string RuleViolationDetect = SimulationEventKind.RuleViolationDetect;
    public const string RuleEnforcementApply = SimulationEventKind.RuleEnforcementApply;
    public const string RelationshipTrustChange = SimulationEventKind.RelationshipTrustChange;
    public const string RelationshipConflictChange = SimulationEventKind.RelationshipConflictChange;
    public const string ResourceStockChange = SimulationEventKind.ResourceStockChange;
    public const string ResourceScarcitySignal = SimulationEventKind.ResourceScarcitySignal;

    public const string ResourceUse = SimulationEventKind.ResourceUse;
    public const string ResourceTransfer = SimulationEventKind.ResourceTransfer;
    public const string ActorMoveIntention = SimulationEventKind.ActorMoveIntention;
    public const string ActorWork = SimulationEventKind.ActorWork;
    public const string ActorAdmin = SimulationEventKind.ActorAdmin;
    public const string StoreChanged = SimulationEventKind.StoreChanged;
    public const string RelationshipChanged = SimulationEventKind.RelationshipChanged;
    public const string IssueRaised = SimulationEventKind.IssueRaised;
    public const string IssueResolved = SimulationEventKind.IssueResolved;
    public const string RuleApplied = SimulationEventKind.RuleApplied;
    public const string Travel = SimulationEventKind.Travel;
    public const string ReturnHome = SimulationEventKind.ReturnHome;
    public const string Work = SimulationEventKind.Work;
    public const string Administration = SimulationEventKind.Administration;
    public const string Trade = SimulationEventKind.Trade;
    public const string Loan = SimulationEventKind.Loan;
    public const string Repayment = SimulationEventKind.Repayment;
    public const string RuleCheck = SimulationEventKind.RuleCheck;
    public const string RuleViolation = SimulationEventKind.RuleViolation;
    public const string RuleEnforcement = SimulationEventKind.RuleEnforcement;
    public const string RelationshipChange = SimulationEventKind.RelationshipChange;
    public const string Maintenance = SimulationEventKind.Maintenance;
    public const string Production = SimulationEventKind.Production;
    public const string Consumption = SimulationEventKind.Consumption;
}
