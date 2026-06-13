namespace CanDoItAll.Economy.Simulator;

public enum SimulationProjectStatus
{
    Draft = 0,
    Active = 1,
    Running = 2,
    NeedsAttention = 3,
    Archived = 4
}

public enum SimulationProjectStorageMode
{
    InMemory = 0,
    PostgreSql = 1,
    Imported = 2,
    Sample = 3
}

public enum SimulationProjectFolderKind
{
    ProjectConfiguration = 0,
    BusinessUnits = 1,
    FinancialInputs = 2,
    RulePack = 3,
    TriggerTemplate = 4,
    EvidenceTemplate = 5,
    ImportedObjects = 6,
    Custom = 100
}

public enum SimulationBusinessObjectDefinitionKind
{
    ProjectConfiguration = 0,
    BusinessUnitDefinition = 1,
    FinancialInput = 2,
    RuleDefinition = 3,
    TriggerTemplate = 4,
    EvidenceTemplate = 5,
    ImportedObject = 6,
    Custom = 100
}

public enum SimulationDefinitionMaterializationStatus
{
    Draft = 0,
    Validated = 1,
    Materialized = 2,
    Blocked = 3
}

public enum SimulationFinancialInputKind
{
    TaxRate = 0,
    InflationRate = 1,
    CustomerAcquisitionRate = 2,
    CollectionDelayDays = 3,
    SupplierPaymentTermDays = 4,
    WageAssumption = 5,
    PriceGrowthRate = 6,
    CostGrowthRate = 7,
    Custom = 100
}

public enum SimulationFinancialInputUnit
{
    Percent = 0,
    Amount = 1,
    Days = 2,
    Count = 3,
    Text = 4
}

public enum SimulationRunStepPeriod
{
    Day = 0,
    Week = 1,
    Month = 2,
    Quarter = 3,
    HalfYear = 4,
    Year = 5
}

public enum SimulationUiRunSpeed
{
    Paused = 0,
    Slow = 1,
    Normal = 2,
    Fast = 3,
    Max = 4
}

public enum SimulationRunMode
{
    Automatic = 0,
    Manual = 1
}

public enum SimulationRunStepRequestKind
{
    Select = 0,
    Previous = 1,
    Next = 2
}

public enum SimulationRunInterventionKind
{
    DemandChange = 0,
    CapacityExpansion = 1,
    FinancialInputOverride = 2,
    Custom = 100
}

public enum SimulationRunInterventionStatus
{
    Applied = 0,
    Rejected = 1
}

public enum SimulationValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public enum SimulationRunLifecycleStatus
{
    Queued = 0,
    Running = 1,
    Paused = 2,
    Completed = 3,
    CompletedWithWarnings = 4,
    Failed = 5,
    Stopped = 6
}

public enum SimulationRunCommandKind
{
    Start = 0,
    Pause = 1,
    Resume = 2,
    Stop = 3,
    Restart = 4
}

public enum SimulationRunPipelineStepStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4
}

public sealed record SimulationProjectSummary(
    string ProjectId,
    string DisplayName,
    SimulationProjectStatus Status,
    string BaseCurrencyCode,
    DateOnly PlanningStartDate,
    DateOnly PlanningEndDate,
    DateTimeOffset? LastRunAtUtc,
    int IssueCount,
    SimulationProjectStorageMode StorageMode,
    DateTimeOffset UpdatedAtUtc);

public sealed record SimulationProjectValidationMessage(
    string Code,
    SimulationValidationSeverity Severity,
    string Message,
    string? Path = null,
    string? RelatedId = null);

public sealed record SimulationProjectValidationResult(
    IReadOnlyList<SimulationProjectValidationMessage> Messages)
{
    public bool IsValid => Messages.All(message => message.Severity != SimulationValidationSeverity.Error);
}

public sealed record SimulationProjectFolderState(
    string FolderId,
    string? ParentFolderId,
    string DisplayName,
    SimulationProjectFolderKind Kind,
    int SortOrder,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationBusinessObjectDefinitionState(
    string DefinitionId,
    string DisplayName,
    SimulationBusinessObjectDefinitionKind Kind,
    string ObjectType,
    string SchemaVersion,
    string? FolderId,
    BusinessUnitId? OwnerBusinessUnitId,
    Address? OwnerAddress,
    string StateJson,
    AccountRuleDefinition? RuleDefinition,
    SimulationDefinitionMaterializationStatus MaterializationStatus,
    IReadOnlyList<string> EvidenceObjectIds,
    IReadOnlyList<SimulationProjectValidationMessage> ValidationMessages,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationFinancialInputState(
    string InputId,
    string DisplayName,
    SimulationFinancialInputKind Kind,
    SimulationFinancialInputUnit Unit,
    string UnitCode,
    decimal? NumericValue,
    string? TextValue,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    BusinessUnitId? BusinessUnitId,
    string? Source,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationFinancialInputProfileState(
    string ProfileId,
    string DisplayName,
    IReadOnlyList<SimulationFinancialInputState> Inputs,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunSetupState(
    string SetupId,
    ScenarioId ScenarioId,
    string ScenarioName,
    SimulationRunStepPeriod StepPeriod,
    int StepCount,
    DateOnly HorizonStartDate,
    DateOnly HorizonEndDate,
    SimulationUiRunSpeed UiSpeed,
    int Seed,
    IReadOnlyList<BusinessUnitId> IncludedBusinessUnits,
    IReadOnlyList<string> IncludedTemplateIds,
    bool RuleExecutionEnabled,
    IReadOnlyDictionary<string, string> AssumptionOverrides,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationProjectState(
    SimulationProjectSummary Summary,
    EconomySimulatorModelState Model,
    IReadOnlyList<BusinessUnitState> BusinessUnits,
    IReadOnlyList<BusinessUnitAddressBindingState> AddressBindings,
    IReadOnlyList<SimulationProjectFolderState> Folders,
    IReadOnlyList<SimulationBusinessObjectDefinitionState> BusinessObjectDefinitions,
    IReadOnlyList<SimulationFinancialInputProfileState> FinancialInputProfiles,
    IReadOnlyList<SimulationRunSetupState> RunSetups,
    IReadOnlyList<SimulationProjectValidationMessage> ValidationMessages,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationProjectExportManifest(
    string SchemaVersion,
    string ProjectId,
    DateTimeOffset ExportedAtUtc,
    SimulationProjectState Project,
    IReadOnlyDictionary<string, string> ContentHashes);

public sealed record SimulationProjectImportPreview(
    SimulationProjectExportManifest? Manifest,
    SimulationProjectValidationResult Validation,
    bool CanImport,
    IReadOnlyList<string> Warnings);

public sealed record SimulationRunSummary(
    string ProjectId,
    string ProjectName,
    ScenarioId ScenarioId,
    string ScenarioName,
    string RunId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateOnly HorizonStartDate,
    DateOnly HorizonEndDate,
    int CurrentStep,
    int TotalSteps,
    DateOnly CurrentDate,
    string OutputHash);

public sealed record SimulationRunRegistryRecord(
    string ProjectId,
    string ProjectName,
    string SetupId,
    ScenarioId ScenarioId,
    string ScenarioName,
    string RunId,
    SimulationRunLifecycleStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    DateOnly CurrentDate,
    int CurrentStep,
    int TotalSteps,
    SimulationRunMode RunMode,
    int SelectedStep,
    int AvailableStep,
    bool SelectedStepHasData,
    string OutputHash,
    int WarningCount,
    int ErrorCount,
    TimeSpan WallClockElapsed,
    TimeSpan? EstimatedRemaining,
    string StartedBy,
    DateTimeOffset LastUpdatedAtUtc,
    string? FailureSummary,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunCommandResult(
    bool Succeeded,
    string? RunId,
    SimulationRunLifecycleStatus? Status,
    IReadOnlyList<SimulationProjectValidationMessage> Messages)
{
    public bool HasErrors => Messages.Any(message => message.Severity == SimulationValidationSeverity.Error);
}

public sealed record SimulationRunStepSnapshotState(
    int Step,
    DateOnly Date,
    DateTimeOffset MaterializedAtUtc,
    string OutputHash,
    SimulationRunLifecycleStatus Status,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunInterventionRequest(
    SimulationRunInterventionKind Kind,
    string TargetId,
    DateOnly? EffectiveDate = null,
    decimal? QuantityDelta = null,
    decimal? Multiplier = null,
    decimal? Amount = null,
    int? DepreciationMonths = null,
    string? DisplayName = null,
    string? Reason = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record SimulationRunInterventionState(
    string InterventionId,
    SimulationRunInterventionKind Kind,
    SimulationRunInterventionStatus Status,
    string TargetId,
    string DisplayName,
    DateOnly EffectiveDate,
    decimal? QuantityDelta,
    decimal? Multiplier,
    decimal? Amount,
    int? DepreciationMonths,
    string Reason,
    int AppliedAtStep,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset AppliedAtUtc,
    string BusinessObjectDefinitionId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunInterventionCommandResult(
    SimulationRunCommandResult Command,
    SimulationRunInterventionState? Intervention);

public sealed record SimulationRunClockState(
    DateOnly CurrentDate,
    int CurrentStep,
    int TotalSteps,
    SimulationRunStepPeriod StepPeriod,
    SimulationUiRunSpeed UiSpeed);

public sealed record SimulationRunKpiSnapshot(
    string Code,
    string DisplayName,
    string Value,
    string Unit,
    string Tone,
    string DeltaText,
    string SourceLabel,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunEventState(
    string EventId,
    DateOnly SimulationDate,
    DateTimeOffset ObservedAtUtc,
    SimulationValidationSeverity Level,
    string Source,
    string Message,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunTopIssueState(
    string IssueId,
    SimulationValidationSeverity Severity,
    string Source,
    string Issue,
    DateOnly FirstSeen,
    int Count,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunPipelineStepState(
    string StepId,
    int SortOrder,
    string DisplayName,
    SimulationRunPipelineStepStatus Status,
    TimeSpan? Duration,
    string Detail,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunResourceMetricState(
    string MetricId,
    string DisplayName,
    decimal Percent,
    string Detail,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunResourceSnapshot(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<SimulationRunResourceMetricState> Metrics,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunAssumptionItem(
    string Key,
    string DisplayName,
    string Value,
    string Source,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationOutputSourceRef(
    string OutputCode,
    string DisplayName,
    string SourceKind,
    BusinessUnitId? BusinessUnitId,
    string? BusinessUnitName,
    string? BusinessObjectDefinitionId,
    string? RuleId,
    string? EvidenceId,
    string Notes,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationBusinessUnitAccountSnapshot(
    string SnapshotId,
    BusinessUnitId BusinessUnitId,
    string BusinessUnitName,
    Address AccountAddress,
    BusinessUnitAddressRole AccountRole,
    BusinessUnitAddressBindingStatus BindingStatus,
    Amount Balance,
    IReadOnlyList<string> CurrentBusinessObjectIds,
    bool HasRuleToken,
    int ProcessedTriggerCount,
    int FailedActionCount,
    int SkippedActionCount,
    IReadOnlyList<string> RelatedIssueIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunArtifactState(
    string ArtifactId,
    string DisplayName,
    string ArtifactType,
    string Hash,
    string? Path,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SimulationRunObservationState(
    SimulationRunSummary Summary,
    SimulationRunClockState Clock,
    IReadOnlyList<EconomyGraphData> Graphs,
    IReadOnlyList<SimulationBusinessUnitAccountSnapshot> AccountSnapshots,
    IReadOnlyList<SimulationBusinessObjectDefinitionState> CurrentBusinessObjects,
    IReadOnlyList<RuleExecutionEvidenceState> RuleEvidence,
    IReadOnlyList<SimulationIssue> Issues,
    IReadOnlyList<SimulationRunArtifactState> Artifacts,
    IReadOnlyDictionary<string, string> OutputHashes);

public sealed record SimulationRunConsoleState(
    SimulationProjectState Project,
    SimulationRunSetupState Setup,
    SimulationRunRegistryRecord Run,
    SimulationRunObservationState Observation,
    IReadOnlyList<SimulationRunKpiSnapshot> Kpis,
    IReadOnlyList<SimulationRunEventState> Events,
    IReadOnlyList<SimulationRunTopIssueState> TopIssues,
    IReadOnlyList<SimulationRunPipelineStepState> Pipeline,
    SimulationRunResourceSnapshot ResourceUsage,
    IReadOnlyList<SimulationRunAssumptionItem> Assumptions,
    IReadOnlyList<SimulationOutputSourceRef> OutputSources,
    IReadOnlyList<SimulationRunStepSnapshotState> StepSnapshots);
