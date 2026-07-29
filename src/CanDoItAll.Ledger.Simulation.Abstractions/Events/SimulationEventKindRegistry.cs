namespace CanDoItAll.Ledger.Simulation.Abstractions;

public static class SimulationEventKindRegistry
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [SimulationEventKind.ResourceUse] = SimulationEventKind.ActorResourceUse,
        [SimulationEventKind.ResourceTransfer] = SimulationEventKind.ActorResourceTransfer,
        [SimulationCanonicalEventKind.ResourceCollect] = SimulationEventKind.ActorResourceCollect,
        [SimulationCanonicalEventKind.ResourceUse] = SimulationEventKind.ActorResourceUse,
        [SimulationCanonicalEventKind.ResourceTransfer] = SimulationEventKind.ActorResourceTransfer,
        [SimulationCanonicalEventKind.ActorTravel] = SimulationEventKind.ActorTravel,
        [SimulationCanonicalEventKind.ActorReturn] = SimulationEventKind.ActorReturnHome,
        [SimulationCanonicalEventKind.ActorWork] = SimulationEventKind.ActorWorkPerform,
        [SimulationCanonicalEventKind.ActorAdmin] = SimulationEventKind.ActorAdminPerform,
        [SimulationCanonicalEventKind.MarketTrade] = SimulationEventKind.ActorTradeSell,
        [SimulationCanonicalEventKind.MarketLoan] = SimulationEventKind.Loan,
        [SimulationCanonicalEventKind.MarketRepayment] = SimulationEventKind.Repayment,
        [SimulationCanonicalEventKind.RuleCheck] = SimulationEventKind.RuleCheckEvaluate,
        [SimulationCanonicalEventKind.RuleViolation] = SimulationEventKind.RuleViolationDetect,
        [SimulationCanonicalEventKind.RuleEnforcement] = SimulationEventKind.RuleEnforcementApply,
        [SimulationCanonicalEventKind.RelationshipChange] = SimulationEventKind.RelationshipTrustChange,
        [SimulationEventKind.ActorMoveIntention] = SimulationEventKind.ActorTravel,
        [SimulationEventKind.Travel] = SimulationEventKind.ActorTravel,
        [SimulationEventKind.ReturnHome] = SimulationEventKind.ActorReturnHome,
        [SimulationEventKind.ActorWork] = SimulationEventKind.ActorWorkPerform,
        [SimulationEventKind.Work] = SimulationEventKind.ActorWorkPerform,
        [SimulationEventKind.Maintenance] = SimulationEventKind.ActorWorkPerform,
        [SimulationEventKind.ActorAdmin] = SimulationEventKind.ActorAdminPerform,
        [SimulationEventKind.Administration] = SimulationEventKind.ActorAdminPerform,
        [SimulationEventKind.Trade] = SimulationEventKind.ActorTradeSell,
        [SimulationEventKind.RuleCheck] = SimulationEventKind.RuleCheckEvaluate,
        [SimulationEventKind.RuleViolation] = SimulationEventKind.RuleViolationDetect,
        [SimulationEventKind.RuleEnforcement] = SimulationEventKind.RuleEnforcementApply,
        [SimulationEventKind.RelationshipChange] = SimulationEventKind.RelationshipTrustChange,
        [SimulationEventKind.RelationshipChanged] = SimulationEventKind.RelationshipTrustChange,
        [SimulationEventKind.Consumption] = SimulationEventKind.ActorResourceUse,
        [SimulationEventKind.Production] = SimulationEventKind.ActorResourceCollect,
        ["need"] = SimulationEventKind.ActorResourceNeed,
        ["daily-need"] = SimulationEventKind.ActorResourceNeed,
        ["inventory-build"] = SimulationEventKind.ActorResourceInventoryBuild,
        ["tax-fee"] = SimulationEventKind.ActorTaxFeePay,
        ["contribution"] = SimulationEventKind.ActorResourceContribution,
        ["resource-contribution"] = SimulationEventKind.ActorResourceContribution,
        ["obligation"] = SimulationEventKind.ActorObligationCreate,
        ["obligation-create"] = SimulationEventKind.ActorObligationCreate,
        ["claim"] = SimulationEventKind.ActorClaimIssue,
        ["claim-issue"] = SimulationEventKind.ActorClaimIssue,
        ["return-pay"] = SimulationEventKind.ActorReturnPay,
        ["return-payment"] = SimulationEventKind.ActorReturnPay
    };

    public static string Normalize(string eventKind)
    {
        if (string.IsNullOrWhiteSpace(eventKind))
        {
            return string.Empty;
        }

        if (Aliases.TryGetValue(eventKind, out string? alias))
        {
            return alias;
        }

        string dotted = eventKind.Replace('-', '.');
        return Known.Contains(dotted) ? dotted : eventKind;
    }

    public static bool IsKnown(string eventKind)
        => Known.Contains(Normalize(eventKind));

    public static int OrderKey(string eventKind)
        => Normalize(eventKind) switch
        {
            SimulationEventKind.ActorTravel => 10,
            SimulationEventKind.ActorResourceNeed => 20,
            SimulationEventKind.ActorResourceCollect => 30,
            SimulationEventKind.ActorResourceInventoryBuild => 35,
            SimulationEventKind.ActorResourceUse => 40,
            SimulationEventKind.ActorResourceTransfer => 45,
            SimulationEventKind.ActorResourceContribution => 47,
            SimulationEventKind.ActorTradeSell => 50,
            SimulationEventKind.ActorTradeBuy => 55,
            SimulationEventKind.ActorClaimIssue => 57,
            SimulationEventKind.ActorReturnPay => 58,
            SimulationEventKind.ActorAdminPerform => 60,
            SimulationEventKind.ActorTaxFeePay => 65,
            SimulationEventKind.RuleCheckEvaluate => 70,
            SimulationEventKind.RuleViolationDetect => 80,
            SimulationEventKind.RuleEnforcementApply => 90,
            SimulationEventKind.ActorObligationCreate or SimulationEventKind.RelationshipTrustChange or SimulationEventKind.RelationshipConflictChange => 100,
            SimulationEventKind.ActorReturnHome => 110,
            _ => 500
        };

    public static IReadOnlySet<string> Known { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SimulationEventKind.ActorResourceCollect,
        SimulationEventKind.ActorResourceNeed,
        SimulationEventKind.ActorResourceInventoryBuild,
        SimulationEventKind.ActorResourceUse,
        SimulationEventKind.ActorResourceTransfer,
        SimulationEventKind.ActorResourceContribution,
        SimulationEventKind.ActorObligationCreate,
        SimulationEventKind.ActorClaimIssue,
        SimulationEventKind.ActorReturnPay,
        SimulationEventKind.ActorTradeSell,
        SimulationEventKind.ActorTradeBuy,
        SimulationEventKind.ActorTravel,
        SimulationEventKind.ActorReturnHome,
        SimulationEventKind.ActorAdminPerform,
        SimulationEventKind.ActorWorkPerform,
        SimulationEventKind.ActorTaxFeePay,
        SimulationEventKind.RuleCheckEvaluate,
        SimulationEventKind.RuleViolationDetect,
        SimulationEventKind.RuleEnforcementApply,
        SimulationEventKind.RelationshipTrustChange,
        SimulationEventKind.RelationshipConflictChange,
        SimulationEventKind.ResourceStockChange,
        SimulationEventKind.ResourceScarcitySignal,
        SimulationEventKind.StoreChanged,
        SimulationEventKind.IssueRaised,
        SimulationEventKind.IssueResolved,
        SimulationEventKind.RuleApplied,
        SimulationEventKind.Loan,
        SimulationEventKind.Repayment
    };
}
