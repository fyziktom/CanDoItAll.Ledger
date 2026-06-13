namespace CanDoItAll.Economy.Simulator;

public sealed record SimulatorFunctionCatalog(
    IReadOnlyList<SimulatorFunctionMapItem> Functions,
    IReadOnlyList<SimulatorApiEndpointMapItem> Endpoints,
    IReadOnlyList<SimulatorCfoCoverageItem> CfoCoverage);

public sealed record SimulatorFunctionMapItem(
    string Group,
    string SourceType,
    string FunctionName,
    string Operation,
    string PrimaryData,
    string ApiGroup,
    string ImplementationStatus,
    string SourceFile,
    string CfoLens,
    string Notes);

public sealed record SimulatorApiEndpointMapItem(
    string Group,
    string Method,
    string Route,
    string Purpose,
    string BackingService,
    string PrimaryResponse,
    string CfoLens,
    string ImplementationStatus);

public sealed record SimulatorCfoCoverageItem(
    string Lens,
    string RequiredInformation,
    string SimulatorSource,
    string ApiExposure,
    string Status,
    string ProfessionalReviewNote);

public sealed record SimulationProjectUpsertResponse(
    SimulationProjectState Project,
    SimulationProjectValidationResult Validation,
    bool Stored,
    string StorageMode);

public sealed record SimulationProjectImportApiRequest(string ManifestJson);

public sealed record SimulationProjectImportApiResponse(
    SimulationProjectImportPreview Preview,
    SimulationProjectState? StoredProject);

public sealed record SimulationRunStartApiRequest(
    string? SetupId = null,
    DateTimeOffset? StartedAtUtc = null,
    SimulationRunMode Mode = SimulationRunMode.Automatic);

public sealed record SimulationRunStartApiResponse(
    SimulationRunCommandResult Command,
    SimulationRunConsoleState? Run);

public sealed record SimulationRunCommandApiRequest(SimulationRunCommandKind Command);

public sealed record SimulationRunCommandApiResponse(
    SimulationRunCommandResult Command,
    SimulationRunConsoleState? Run);

public sealed record SimulationRunStepApiRequest(
    SimulationRunStepRequestKind Step = SimulationRunStepRequestKind.Next,
    int? TargetStep = null);

public sealed record SimulationRunStepApiResponse(
    SimulationRunCommandResult Command,
    SimulationRunConsoleState? Run);

public sealed record SimulationRunInterventionApiResponse(
    SimulationRunCommandResult Command,
    SimulationRunConsoleState? Run,
    SimulationRunInterventionState? Intervention);

public enum SimulationPeriodSummaryGranularity
{
    Quarter = 0,
    Year = 1
}

public sealed record SimulationPeriodSummarySnapshot(
    string ProjectId,
    string RunId,
    SimulationPeriodSummaryGranularity Granularity,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CurrencyCode,
    decimal MinimumCashFloor,
    decimal TotalCashFlow,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal TotalProfit,
    decimal EndingOwnCapital,
    decimal EndingExternalCapital,
    IReadOnlyList<SimulationPeriodSummaryPeriod> Periods,
    IReadOnlyList<SimulationPeriodSummaryDistributionSlice> IncomeDistribution,
    IReadOnlyList<SimulationPeriodSummaryDistributionSlice> ExpenseDistribution);

public sealed record SimulationPeriodSummaryPeriod(
    string PeriodCode,
    string DisplayName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal CashInflow,
    decimal CashOutflow,
    decimal CashFlow,
    decimal Income,
    decimal Expenses,
    decimal Profit,
    decimal EndingCash,
    decimal OwnCapital,
    decimal ExternalCapital);

public sealed record SimulationPeriodSummaryDistributionSlice(
    string CategoryCode,
    string DisplayName,
    decimal Amount,
    decimal PercentOfTotal);

public sealed record SimulationScenarioTemplateSummary(
    string ProjectId,
    string ScenarioCode,
    string DisplayName,
    string BaseCurrencyCode,
    DateOnly PlanningStartDate,
    DateOnly PlanningEndDate,
    decimal OpeningCash,
    decimal MinimumCash,
    int BusinessUnitCount,
    int RevenueContractCount,
    int ProductOrOfferingCount,
    int WorkforcePlanCount,
    int CapexPlanCount,
    int CostPlanCount);

public sealed record SimulationScenarioCompileApiRequest(
    string ScenarioCode,
    string? ScenarioId = null,
    DateOnly? HorizonStartDate = null,
    DateOnly? HorizonEndDate = null);

public sealed record SimulationCalibrationRecommendationApiRequest(
    string ProjectId,
    string MetricCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal PlannedValue,
    decimal ActualValue,
    IReadOnlyList<string> EvidenceObjectIds);

public sealed record SimulationFinanceAnalysisSnapshot(
    string ProjectId,
    string RunId,
    DateTimeOffset GeneratedAtUtc,
    string SituationClassification,
    SimulationFinanceSummary Summary,
    SimulationFinanceDataQuality DataQuality,
    IReadOnlyList<SimulationFinanceLensState> CfoLenses,
    IReadOnlyList<string> ProfessionalReviewWarnings);

public sealed record SimulationFinanceSummary(
    decimal TotalCurrentCash,
    decimal MinimumCashFloor,
    decimal CashHeadroom,
    decimal Revenue,
    decimal Expense,
    decimal NetCashFlow,
    int WarningCount,
    int ErrorCount,
    int KpiCount,
    decimal SourceMapCoveragePercent);

public sealed record SimulationFinanceDataQuality(
    string StorageMode,
    bool UsesInMemoryOrSampleData,
    int ProjectValidationMessageCount,
    int OutputHashCount,
    int ArtifactCount,
    IReadOnlyList<string> LimitationNotes);

public sealed record SimulationFinanceLensState(
    string Lens,
    string Status,
    string Evidence,
    IReadOnlyList<string> DataFields,
    IReadOnlyList<string> Gaps);
