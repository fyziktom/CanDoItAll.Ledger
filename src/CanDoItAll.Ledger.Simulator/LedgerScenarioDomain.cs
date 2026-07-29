namespace CanDoItAll.Ledger.Simulator;

public enum PaymentTermKind
{
    Immediate = 0,
    NetDays = 1,
    Milestone = 2
}

public enum ScenarioCadence
{
    Once = 0,
    Monthly = 1,
    Quarterly = 2,
    HalfYearly = 3,
    Annual = 4
}

public enum FinancialStatementSection
{
    Revenue = 0,
    CashReceipts = 1,
    Cogs = 2,
    Opex = 3,
    Payroll = 4,
    Capex = 5,
    Depreciation = 6,
    WorkingCapital = 7,
    Inventory = 8,
    Wip = 9,
    Financing = 10,
    Tax = 11,
    Transfer = 12,
    NonCashAdjustment = 13
}

public enum ScenarioFinancialScheduleKind
{
    Revenue = 0,
    Expense = 1,
    Payroll = 2,
    Capex = 3,
    Funding = 4,
    Reserve = 5,
    Custom = 100
}

public enum ScenarioApprovalStatus
{
    NotRequired = 0,
    Draft = 1,
    Requested = 2,
    BusinessUnitApproved = 3,
    ManagementApproved = 4,
    Rejected = 5,
    Dispatched = 6
}

public enum ScenarioApprovalScope
{
    None = 0,
    BusinessUnitSelfApproval = 1,
    Management = 2
}

public enum ScenarioGraphMetric
{
    CashFlow = 0,
    Revenue = 1,
    Expense = 2
}

public enum ScenarioRunMode
{
    DraftOnly = 0,
    ScenarioPosting = 1,
    DraftAndScenarioPosting = 2
}

public enum ScenarioWorkOrderKind
{
    ExternalContract = 0,
    SupportSla = 1,
    Implementation = 2,
    InternalProject = 3,
    ProductionEngineering = 4,
    Custom = 100
}

public enum ScenarioProductFlowStepKind
{
    MaterialIssue = 0,
    Production = 1,
    Assembly = 2,
    Quality = 3,
    WarehouseTransfer = 4,
    CustomerDelivery = 5,
    Custom = 100
}

public sealed record PaymentTermState(
    string PaymentTermId,
    PaymentTermKind Kind,
    int DueDays,
    decimal SamePeriodCollectionRate,
    int ExtraCollectionDelayDays,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CounterpartyState(
    string ProjectId,
    string CounterpartyId,
    string DisplayName,
    string CounterpartyType,
    BusinessUnitId? BusinessUnitId,
    string? DefaultPaymentTermId,
    string CurrencyCode,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CustomerContractState(
    string ProjectId,
    string CustomerContractId,
    string CounterpartyId,
    string DisplayName,
    string PaymentTermId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    BusinessUnitId RevenueBusinessUnitId,
    string CurrencyCode,
    IReadOnlyList<string> ProductOrServiceIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record OfferingState(
    string ProjectId,
    string OfferingId,
    string DisplayName,
    RevenueStreamKind Kind,
    string BusinessUnitPath,
    string RevenueCategoryCode,
    string DirectCostCategoryCode,
    IReadOnlyList<PriceTierState> PriceTiers,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PriceTierState(
    string PriceTierId,
    string DisplayName,
    Amount UnitPrice,
    Amount UnitCost,
    string PeriodCode,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProductSkuState(
    string ProjectId,
    string ProductSkuId,
    string DisplayName,
    string UnitCode,
    Amount UnitPrice,
    Amount StandardUnitCost,
    string RevenueCategoryCode,
    string CogsCategoryCode,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record DemandScheduleState(
    string ProjectId,
    string DemandScheduleId,
    string CustomerContractId,
    string ProductOrServiceId,
    BusinessUnitId BusinessUnitId,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal InitialQuantity,
    ScenarioCadence Cadence,
    decimal MonthlyGrowthRate,
    IReadOnlyList<DemandChangeState> DemandChanges,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record DemandChangeState(
    DateOnly EffectiveDate,
    decimal QuantityDelta,
    decimal Multiplier,
    string Reason,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ImplementationProjectState(
    string ProjectId,
    string ImplementationProjectId,
    string CustomerContractId,
    string DisplayName,
    Amount ContractValue,
    Amount DirectCost,
    DateOnly StartDate,
    IReadOnlyList<MilestoneBillingState> Milestones,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MilestoneBillingState(
    string MilestoneId,
    DateOnly MilestoneDate,
    decimal RevenuePercent,
    decimal CostPercent,
    string Description);

public sealed record DepartmentStaffingState(
    string DepartmentStaffingId,
    BusinessUnitId BusinessUnitId,
    string RoleGroup,
    decimal StartingFte,
    Amount MonthlyGrossSalaryPerFte,
    decimal EmployerBurdenRate,
    IReadOnlyList<HeadcountEventState> HeadcountEvents,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record HeadcountEventState(
    DateOnly EffectiveDate,
    decimal FteDelta,
    string Reason,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record WorkforcePlanState(
    string ProjectId,
    string WorkforcePlanId,
    IReadOnlyList<DepartmentStaffingState> Departments,
    IReadOnlyList<EquipmentPolicyState> EquipmentPolicies,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EquipmentPolicyState(
    string EquipmentPolicyId,
    string RoleGroup,
    Amount CapexPerNewHire,
    Amount OpexPerNewHire,
    int DepreciationMonths,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record IngredientState(
    string ProjectId,
    string IngredientId,
    string DisplayName,
    string UnitCode,
    Amount UnitCost,
    int SupplierPaymentTermDays,
    int SafetyStockDays,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record RecipeState(
    string ProjectId,
    string RecipeId,
    string ProductSkuId,
    decimal SpoilageRate,
    IReadOnlyList<RecipeIngredientQuantity> Ingredients,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record RecipeIngredientQuantity(
    string IngredientId,
    decimal QuantityPerFinishedUnit);

public sealed record BillOfMaterialState(
    string ProjectId,
    string BillOfMaterialId,
    string ProductSkuId,
    decimal StandardYieldRate,
    IReadOnlyList<BillOfMaterialLineState> Lines,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record BillOfMaterialLineState(
    string MaterialGroup,
    Amount UnitCost,
    int SupplierPaymentTermDays,
    int SafetyStockDays,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record InventoryLocationState(
    string ProjectId,
    string InventoryLocationId,
    string DisplayName,
    BusinessUnitId BusinessUnitId,
    string LocationType,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record InventoryMovementState(
    string ProjectId,
    string InventoryMovementId,
    string ProductOrMaterialId,
    string FromLocationId,
    string ToLocationId,
    DateOnly MovementDate,
    decimal Quantity,
    Amount UnitCost,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProductionRouteState(
    string ProjectId,
    string ProductionRouteId,
    string ProductSkuId,
    IReadOnlyList<ProductionRouteStepState> Steps,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProductionRouteStepState(
    string StepId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    decimal StandardLaborMinutes,
    string InputLocationId,
    string OutputLocationId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record WorkInProgressState(
    string ProjectId,
    string WorkInProgressId,
    string ProductSkuId,
    BusinessUnitId BusinessUnitId,
    Amount Value,
    DateOnly AsOfDate,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record WorkOrderState(
    string ProjectId,
    string WorkOrderId,
    string? CustomerContractId,
    string ProductOrServiceId,
    string DisplayName,
    ScenarioWorkOrderKind Kind,
    BusinessUnitId RequestingBusinessUnitId,
    BusinessUnitId DeliveryBusinessUnitId,
    string? DemandScheduleId,
    string? ImplementationProjectId,
    DateOnly StartDate,
    DateOnly? EndDate,
    ScenarioCadence Cadence,
    decimal FixedHoursPerCadence,
    decimal HoursPerDemandUnit,
    string InvoicePolicyCode,
    IReadOnlyList<WorkAllocationRuleState> AllocationRules,
    IReadOnlyList<WorkDeliveryState> DeliveryOverrides,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record WorkAllocationRuleState(
    string WorkAllocationRuleId,
    BusinessUnitId WorkSourceBusinessUnitId,
    decimal AllocationPercent,
    string StrategyCode,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record WorkDeliveryState(
    string WorkDeliveryId,
    DateOnly DeliveryDate,
    decimal Hours,
    string Description,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProductFlowPlanState(
    string ProjectId,
    string ProductFlowPlanId,
    string ProductSkuId,
    string DisplayName,
    string? DemandScheduleId,
    string? CustomerContractId,
    decimal GoodUnitsPerDemandUnit,
    IReadOnlyList<ProductFlowStepState> Steps,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProductFlowStepState(
    string ProductFlowStepId,
    ScenarioProductFlowStepKind Kind,
    BusinessUnitId BusinessUnitId,
    string FromLocationId,
    string ToLocationId,
    decimal QuantityMultiplier,
    decimal ValuePercent,
    string Description,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MachineAssetState(
    string ProjectId,
    string MachineAssetId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    Amount AcquisitionCost,
    DateOnly AcquisitionDate,
    int UsefulLifeMonths,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record FacilityLeaseState(
    string ProjectId,
    string FacilityLeaseId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    Amount MonthlyLeaseAndUtilities,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MaintenancePlanState(
    string ProjectId,
    string MaintenancePlanId,
    BusinessUnitId BusinessUnitId,
    Amount MonthlyAmount,
    Amount? QuarterlyServiceAmount,
    Amount? AnnualCalibrationAmount,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record QualityGateState(
    string ProjectId,
    string QualityGateId,
    string ProductSkuId,
    BusinessUnitId BusinessUnitId,
    decimal FailureReserveRate,
    decimal ScrapRate,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record TestLabPlanState(
    string ProjectId,
    string TestLabPlanId,
    BusinessUnitId BusinessUnitId,
    Amount MonthlyAmount,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SupplierContractState(
    string ProjectId,
    string SupplierContractId,
    string CounterpartyId,
    string MaterialGroup,
    int LeadTimeDays,
    string PaymentTermId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CapitalAssetState(
    string ProjectId,
    string CapitalAssetId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    Amount AcquisitionCost,
    DateOnly AcquisitionDate,
    int UsefulLifeMonths,
    string DepreciationCategoryCode,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CapexPlanState(
    string ProjectId,
    string CapexPlanId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    Amount Amount,
    DateOnly CashOutflowDate,
    int DepreciationMonths,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ScenarioCostPlanState(
    string ProjectId,
    string CostPlanId,
    BusinessUnitId BusinessUnitId,
    string CategoryCode,
    FinancialStatementSection Section,
    Amount MonthlyAmount,
    EconomicImpactKind ImpactKind,
    int PaymentDelayDays,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ScenarioApprovalState(
    ScenarioApprovalStatus Status,
    ScenarioApprovalScope RequiredScope,
    Amount SelfApprovalLimit,
    BusinessUnitId RequestingBusinessUnitId,
    BusinessUnitId ApprovingBusinessUnitId,
    DateOnly? RequestedDate,
    DateOnly? ApprovedDate,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ScenarioFinancialScheduleAmountState(
    DateOnly EconomicDate,
    Amount Amount,
    string? PeriodCode,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ScenarioFinancialScheduleState(
    string ProjectId,
    string ScheduleId,
    string? ParentScheduleId,
    BusinessUnitId BusinessUnitId,
    string DisplayName,
    ScenarioFinancialScheduleKind Kind,
    FinancialStatementSection Section,
    EconomicEventKind EventKind,
    EconomicImpactKind ImpactKind,
    string CategoryCode,
    bool RequiresApproval,
    bool PostsToSimulation,
    ScenarioApprovalState? Approval,
    IReadOnlyList<ScenarioFinancialScheduleAmountState> Amounts,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ScenarioBusinessModelState(
    string ProjectId,
    string ScenarioCode,
    string DisplayName,
    string BaseCurrencyCode,
    DateOnly PlanningStartDate,
    DateOnly PlanningEndDate,
    Amount OpeningCash,
    Amount MinimumCash,
    IReadOnlyList<BusinessUnitState> BusinessUnits,
    IReadOnlyList<PaymentTermState> PaymentTerms,
    IReadOnlyList<CounterpartyState> Counterparties,
    IReadOnlyList<CustomerContractState> CustomerContracts,
    IReadOnlyList<OfferingState> Offerings,
    IReadOnlyList<ProductSkuState> ProductSkus,
    IReadOnlyList<DemandScheduleState> DemandSchedules,
    IReadOnlyList<ImplementationProjectState> ImplementationProjects,
    IReadOnlyList<WorkforcePlanState> WorkforcePlans,
    IReadOnlyList<IngredientState> Ingredients,
    IReadOnlyList<RecipeState> Recipes,
    IReadOnlyList<BillOfMaterialState> BillsOfMaterial,
    IReadOnlyList<InventoryLocationState> InventoryLocations,
    IReadOnlyList<ProductionRouteState> ProductionRoutes,
    IReadOnlyList<WorkOrderState> WorkOrders,
    IReadOnlyList<ProductFlowPlanState> ProductFlowPlans,
    IReadOnlyList<FacilityLeaseState> FacilityLeases,
    IReadOnlyList<MaintenancePlanState> MaintenancePlans,
    IReadOnlyList<QualityGateState> QualityGates,
    IReadOnlyList<TestLabPlanState> TestLabPlans,
    IReadOnlyList<SupplierContractState> SupplierContracts,
    IReadOnlyList<CapitalAssetState> CapitalAssets,
    IReadOnlyList<CapexPlanState> CapexPlans,
    IReadOnlyList<ScenarioCostPlanState> CostPlans,
    IReadOnlyList<ScenarioFinancialScheduleState> FinancialSchedules,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record LedgerScenarioEventCompileRequest(
    ScenarioBusinessModelState Model,
    ScenarioId ScenarioId,
    DateOnly HorizonStartDate,
    DateOnly HorizonEndDate);

public sealed record LedgerScenarioEventCompileResult(
    ScenarioBusinessModelState Model,
    IReadOnlyList<EconomicEventState> Events,
    IReadOnlyDictionary<string, decimal> ExpectedTotals,
    string ScheduleHash);

public sealed record LedgerGraphQueryFilter(
    string ProjectId,
    string? ScenarioId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    ScenarioGraphMetric Metric,
    string? BusinessUnitId,
    IReadOnlyList<string> AccountAddresses,
    IReadOnlyList<string> CategoryCodes,
    int Limit = 500);

public sealed record LedgerGraphPoint(
    DateOnly PeriodStart,
    string BusinessUnitId,
    string? AccountAddress,
    string CategoryCode,
    string FinancialStatementSection,
    decimal PlannedInflow,
    decimal PlannedOutflow,
    decimal CommittedInflow,
    decimal CommittedOutflow,
    decimal ActualInflow,
    decimal ActualOutflow,
    decimal SimulatedInflow,
    decimal SimulatedOutflow,
    decimal NonCashIncome,
    decimal NonCashExpense,
    decimal BalanceAfter);

public sealed record LedgerGraphSeries(
    string Name,
    string CategoryCode,
    IReadOnlyList<LedgerGraphPoint> Points);

public sealed record LedgerGraphDrilldownRow(
    DateOnly Date,
    string EventId,
    string BusinessUnitId,
    string? AccountAddress,
    string CategoryCode,
    string FinancialStatementSection,
    string Status,
    decimal Amount,
    string SourceObjectId,
    string Description);

public sealed record LedgerGraphData(
    LedgerGraphQueryFilter Filter,
    IReadOnlyList<LedgerGraphSeries> Series,
    IReadOnlyList<LedgerGraphDrilldownRow> DrilldownRows,
    IReadOnlyDictionary<string, decimal> Totals);
