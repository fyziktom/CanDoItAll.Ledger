namespace CanDoItAll.Economy.Simulator;

public enum BusinessUnitKind
{
    Project = 0,
    Department = 1,
    CostCenter = 2,
    RevenueCenter = 3,
    ProductLine = 4,
    ServiceLine = 5,
    LegalEntity = 6,
    Counterparty = 7,
    TaxAuthority = 8,
    Investor = 9,
    Reserve = 10,
    ExternalBoundary = 11,
    Custom = 100
}

public enum BusinessUnitStatus
{
    Draft = 0,
    Active = 1,
    Suspended = 2,
    Closed = 3,
    Archived = 4
}

public enum BusinessUnitAddressRole
{
    Primary = 0,
    Treasury = 1,
    Revenue = 2,
    Expense = 3,
    Payroll = 4,
    Tax = 5,
    Capex = 6,
    Depreciation = 7,
    Reserve = 8,
    InternalClearing = 9,
    ExternalClearing = 10,
    Counterparty = 11,
    Escrow = 12,
    SimulationOnly = 13,
    Custom = 100
}

public enum BusinessUnitAddressBindingStatus
{
    Active = 0,
    Suspended = 1,
    Retired = 2,
    Rotated = 3
}

public enum EconomySimulatorTokenClass
{
    BudgetAllocation = 0,
    SpendingAuthority = 1,
    Commitment = 2,
    ResourceCapacity = 3,
    ServiceHour = 4,
    ProductUnit = 5,
    InventoryLot = 6,
    ReceivableClaim = 7,
    PayableObligation = 8,
    FinancingRight = 9,
    EquityShare = 10,
    DebtClaim = 11,
    AssetUnit = 12,
    ReserveUnit = 13,
    Custom = 100
}

public enum EconomicEventKind
{
    Revenue = 0,
    Expense = 1,
    Payroll = 2,
    EmployerContribution = 3,
    EmployeeWithholding = 4,
    Tax = 5,
    Capex = 6,
    Depreciation = 7,
    InvestmentInflow = 8,
    DebtDrawdown = 9,
    DebtRepayment = 10,
    DividendOrDistribution = 11,
    InventoryPurchase = 12,
    InventoryConsumption = 13,
    Cogs = 14,
    Transfer = 15,
    ReserveMovement = 16,
    Adjustment = 17,
    Custom = 100
}

public enum EconomicEventStatus
{
    Draft = 0,
    Planned = 1,
    Simulated = 2,
    Committed = 3,
    Invoiced = 4,
    Due = 5,
    PartiallySettled = 6,
    Settled = 7,
    Cancelled = 8,
    Failed = 9,
    Reconciled = 10,
    Actual = 11
}

public enum EconomicImpactKind
{
    CashOnly = 0,
    AccrualOnly = 1,
    CashAndAccrual = 2,
    NonCashPnl = 3,
    BalanceSheetOnly = 4,
    ControlTokenOnly = 5
}

public enum EconomySimulatorTemplateKind
{
    BusinessUnit = 0,
    Department = 1,
    RevenueModel = 2,
    ProductModel = 3,
    ServiceModel = 4,
    CostModel = 5,
    PayrollModel = 6,
    TaxModel = 7,
    InvestmentModel = 8,
    AssetModel = 9,
    Scenario = 10,
    Composite = 11,
    Custom = 100
}

public enum RevenueStreamKind
{
    Service = 0,
    Product = 1,
    Subscription = 2,
    Hybrid = 3,
    MarketplaceCommission = 4,
    Custom = 100
}

public enum DepreciationMethod
{
    StraightLine = 0,
    DecliningBalance = 1,
    UnitsOfProduction = 2
}

public sealed record EconomySimulatorModelState(
    string ProjectId,
    string Name,
    string SchemaVersion,
    string BaseCurrencyCode,
    DateOnly PlanningStartDate,
    DateOnly PlanningEndDate,
    string CalendarCode,
    string? DefaultTaxRuleSetId,
    string? DefaultPayrollRuleSetId,
    string? DefaultDepreciationRuleSetId,
    BusinessUnitId TreasuryBusinessUnitId,
    BusinessUnitId ExternalClearingBusinessUnitId,
    IReadOnlyList<BusinessUnitId> RootBusinessUnits,
    IReadOnlyDictionary<string, string> Assumptions,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record BusinessUnitState(
    string ProjectId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    BusinessUnitKind Kind,
    BusinessUnitId? ParentBusinessUnitId,
    Address PrimaryAddress,
    string Path,
    string? CostCenterCode,
    string? RevenueCenterCode,
    string? LegalEntityCode,
    string? CurrencyCode,
    bool IsExternal,
    bool IsSimulationOnly,
    BusinessUnitStatus Status,
    IReadOnlyList<Address> KnownAddresses,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record BusinessUnitAddressBindingState(
    string ProjectId,
    BusinessUnitId BusinessUnitId,
    Address Address,
    BusinessUnitAddressRole Role,
    string? RoleCode,
    bool IsPrimary,
    BusinessUnitAddressBindingStatus Status,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset? ValidToUtc,
    Address? ParentAddress,
    BusinessUnitId? ParentBusinessUnitId,
    string? DisplayName,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EconomySimulatorTokenDefinitionState(
    string ProjectId,
    TokenId TokenId,
    EconomySimulatorTokenClass TokenClass,
    string DisplayName,
    string UnitCode,
    BusinessUnitId IssuerBusinessUnitId,
    string Namespace,
    bool IsCashEquivalent,
    bool IsFungible,
    bool RequiresBusinessObjectReference,
    decimal? CashConversionRate,
    string? CurrencyCode,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record RecurrenceSpec(
    string Frequency,
    int Interval,
    DateOnly? Until,
    int? Count,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EconomicEventState(
    string ProjectId,
    string EventId,
    EconomicEventKind Kind,
    EconomicEventStatus Status,
    EconomicImpactKind ImpactKind,
    BusinessUnitId SourceBusinessUnitId,
    BusinessUnitId? DestinationBusinessUnitId,
    Address? SourceAddress,
    Address? DestinationAddress,
    string CategoryCode,
    string? SubcategoryCode,
    Amount Amount,
    string CurrencyCode,
    TokenTransfer? TokenEffect,
    DateOnly EconomicDate,
    DateOnly? DueDate,
    DateOnly? ExpectedSettlementDate,
    DateOnly? ActualSettlementDate,
    RecurrenceSpec? Recurrence,
    decimal Confidence,
    int Priority,
    bool CanBeDelayed,
    string? SourceTemplateId,
    string? SourceObjectId,
    string? CorrelationId,
    IReadOnlyDictionary<string, string> Assumptions,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EconomySimulatorTemplateState(
    string TemplateId,
    string Version,
    string DisplayName,
    EconomySimulatorTemplateKind Kind,
    string Description,
    IReadOnlyList<EconomySimulatorTemplateParameter> Parameters,
    IReadOnlyList<EconomySimulatorTemplateOutput> Outputs,
    IReadOnlyList<string> RequiredRuleProviders,
    IReadOnlyDictionary<string, string> Defaults,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EconomySimulatorTemplateParameter(
    string Name,
    string DisplayName,
    string ValueType,
    bool Required,
    string? DefaultValue,
    string? UnitCode,
    string? ValidationExpression,
    string Description);

public sealed record EconomySimulatorTemplateOutput(
    string ObjectType,
    string Role,
    string? ObjectIdPattern,
    IReadOnlyDictionary<string, string> FieldMappings);

public sealed record RevenueStreamState(
    string ProjectId,
    string RevenueStreamId,
    RevenueStreamKind Kind,
    BusinessUnitId RevenueBusinessUnitId,
    string DisplayName,
    decimal UnitPrice,
    decimal UnitCost,
    int UnitsPerPeriod,
    string PeriodCode,
    int CollectionDelayDays,
    decimal Confidence,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CostPolicyState(
    string ProjectId,
    string CostPolicyId,
    BusinessUnitId BusinessUnitId,
    string CategoryCode,
    Amount AmountPerPeriod,
    string PeriodCode,
    EconomicImpactKind ImpactKind,
    int PaymentDelayDays,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PayrollPlanState(
    string ProjectId,
    string PayrollPlanId,
    BusinessUnitId BusinessUnitId,
    string RuleProviderId,
    Amount GrossWagesPerPeriod,
    decimal EmployeeWithholdingRate,
    decimal EmployerContributionRate,
    string PeriodCode,
    int PaymentDay,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record TaxRuleSetState(
    string ProjectId,
    string TaxRuleSetId,
    string JurisdictionCode,
    string TaxCode,
    decimal Rate,
    int DueDelayDays,
    BusinessUnitId AuthorityBusinessUnitId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record AssetState(
    string ProjectId,
    string AssetId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    Amount AcquisitionCost,
    DateOnly AcquisitionDate,
    int UsefulLifeMonths,
    Amount SalvageValue,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record DepreciationScheduleState(
    string ProjectId,
    string ScheduleId,
    string AssetId,
    DepreciationMethod Method,
    DateOnly StartDate,
    int Periods,
    Amount PeriodAmount,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ExternalSettlementState(
    string ProjectId,
    string SettlementId,
    BusinessUnitId BoundaryBusinessUnitId,
    Address BoundaryAddress,
    BusinessUnitId InternalBusinessUnitId,
    Address InternalAddress,
    Amount Amount,
    string CurrencyCode,
    bool IsInflow,
    string Policy,
    string ExternalReference,
    DateOnly SettlementDate,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ScenarioRunState(
    string ProjectId,
    ScenarioId ScenarioId,
    string RunId,
    string Name,
    string? ParentSnapshotId,
    ScenarioId? ParentScenarioId,
    DateTimeOffset StartedAtUtc,
    DateOnly HorizonStartDate,
    DateOnly HorizonEndDate,
    string SimulationMode,
    int Seed,
    IReadOnlyDictionary<string, string> AssumptionOverrides,
    IReadOnlyList<string> IncludedTemplateIds,
    IReadOnlyList<BusinessUnitId> IncludedBusinessUnits,
    string Status,
    string? ResultObjectId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationResultState(
    string ProjectId,
    ScenarioId ScenarioId,
    string RunId,
    DateTimeOffset CompletedAtUtc,
    DateOnly HorizonStartDate,
    DateOnly HorizonEndDate,
    Amount OpeningCash,
    Amount ClosingCash,
    Amount MinimumCash,
    DateOnly? FirstDeficitDate,
    BusinessUnitId? FirstDeficitBusinessUnitId,
    Amount RequiredMinimumFunding,
    Amount SuggestedReserve,
    IReadOnlyList<CashFlowProjectionPoint> CashFlow,
    IReadOnlyList<SimulationIssue> Issues,
    IReadOnlyDictionary<string, decimal> Kpis,
    IReadOnlyDictionary<string, string> OutputHashes,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CashFlowProjectionPoint(
    DateOnly Date,
    BusinessUnitId BusinessUnitId,
    string CategoryCode,
    Amount PlannedInflow,
    Amount PlannedOutflow,
    Amount CommittedInflow,
    Amount CommittedOutflow,
    Amount ActualInflow,
    Amount ActualOutflow,
    Amount SimulatedInflow,
    Amount SimulatedOutflow,
    Amount BalanceAfter,
    decimal Confidence);

public sealed record SimulationIssue(
    string IssueId,
    string Code,
    string Severity,
    DateOnly Date,
    BusinessUnitId? BusinessUnitId,
    SimulationObjectReference? RelatedObjectReference,
    TxId? FailedTransactionId,
    Amount? Amount,
    string Message,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ActualEconomicEntryState(
    string ProjectId,
    string EntryId,
    string Source,
    EconomicEventState Event,
    string ValidationStatus,
    IReadOnlyList<string> ValidationMessages,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ReconciliationMatch(
    string MatchId,
    string ActualEventId,
    string? PlannedEventId,
    decimal AmountVariance,
    int DateVarianceDays,
    string Status,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CalibrationRecommendationState(
    string ProjectId,
    string RecommendationId,
    string MetricCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string PreviousAssumption,
    string SuggestedAssumption,
    decimal Confidence,
    IReadOnlyList<string> EvidenceObjectIds,
    string Status,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EconomySimulatorBusinessObjectCommandDraft(
    string ObjectType,
    string SchemaVersion,
    string StateJson,
    Address? OwnerAddress = null,
    Amount? MainAmount = null,
    TokenTransfer? Token = null,
    string? ExternalRef = null);

public sealed record EconomySimulatorMaterializationRequest(
    string ProjectId,
    string TemplateId,
    Address PrimaryAddress,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record EconomySimulatorMaterializationPlan(
    string ProjectId,
    string TemplateId,
    IReadOnlyList<EconomySimulatorBusinessObjectCommandDraft> CommandDrafts,
    IReadOnlyList<EconomySimulatorTokenDefinitionState> TokenDefinitions,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, string> PreviewMetrics);

public sealed record ScenarioRunRequest(
    SimulationAccountContext Context,
    string ProjectId,
    string Name,
    DateOnly HorizonStartDate,
    DateOnly HorizonEndDate,
    Amount OpeningCash,
    IReadOnlyList<EconomicEventState> Events,
    IReadOnlyDictionary<string, string>? AssumptionOverrides = null,
    IReadOnlyList<string>? IncludedTemplateIds = null,
    IReadOnlyList<BusinessUnitId>? IncludedBusinessUnits = null,
    int Seed = 0,
    ScenarioRunMode RunMode = ScenarioRunMode.DraftOnly);

public sealed record ScenarioRunResult(
    ScenarioRunState Run,
    SimulationResultState Result,
    IReadOnlyList<EconomySimulatorBusinessObjectCommandDraft> ResultDrafts);

public sealed record ProjectionRequest(
    string ProjectId,
    Amount OpeningCash,
    IReadOnlyList<EconomicEventState> Events);

public sealed record EconomyProjection(
    IReadOnlyList<CashFlowProjectionPoint> CashFlow,
    IReadOnlyDictionary<string, decimal> Kpis,
    IReadOnlyList<SimulationIssue> Issues);

public sealed record ActualEntryRequest(
    SimulationAccountContext Context,
    string ProjectId,
    string EntryId,
    string Source,
    EconomicEventState Event,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ExternalSettlementRequest(
    string ProjectId,
    string SettlementId,
    BusinessUnitId BoundaryBusinessUnitId,
    Address BoundaryAddress,
    BusinessUnitId InternalBusinessUnitId,
    Address InternalAddress,
    Amount Amount,
    string CurrencyCode,
    bool IsInflow,
    string ExternalReference,
    DateOnly SettlementDate,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ExternalSettlementPlan(
    ExternalSettlementState Settlement,
    EconomySimulatorBusinessObjectCommandDraft SettlementDraft,
    PaymentRequest? ClearingTransfer,
    string Policy);
