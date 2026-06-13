using CanDoItAll.Economy.Simulator;

namespace CanDoItAll.Economy.Simulator.Components.Models;

public sealed record EconomySimulatorViewModel(
    string ProjectId,
    string ModelName,
    string StorageMode,
    IReadOnlyList<EconomyKpiItem> Kpis,
    IReadOnlyList<BusinessUnitNodeItem> BusinessUnits,
    IReadOnlyList<AddressBindingItem> AddressBindings,
    IReadOnlyList<TemplateOptionItem> Templates,
    IReadOnlyList<EventTimelineItem> Events,
    IReadOnlyList<CashFlowProjectionPoint> CashFlow,
    IReadOnlyList<EconomyGraphData> Graphs,
    IReadOnlyList<ScenarioIssueItem> Issues,
    IReadOnlyList<ActualEntryItem> ActualEntries,
    IReadOnlyList<ReconciliationItem> Reconciliation,
    IReadOnlyList<CalibrationRecommendationState> Recommendations,
    IReadOnlyList<ObjectDetailItem> ObjectDetails);

public sealed record SimulationProjectManagementViewModel(
    IReadOnlyList<SimulationProjectSummary> Projects,
    SimulationProjectState SelectedProject,
    SimulationProjectExportManifest ExportManifest,
    SimulationProjectImportPreview ImportPreview);

public sealed record SimulationRunObservationViewModel(
    SimulationProjectState Project,
    SimulationRunSetupState Setup,
    SimulationRunObservationState Observation);

public sealed record SimulationRunConsoleViewModel(
    IReadOnlyList<SimulationProjectSummary> Projects,
    IReadOnlyList<SimulationRunConsoleState> Runs,
    SimulationRunConsoleState SelectedRun,
    IReadOnlyList<SimulationProjectValidationMessage> CommandMessages);

public sealed record SimulationProjectRunRequest(
    SimulationProjectState Project,
    string? SetupId,
    SimulationRunMode Mode = SimulationRunMode.Automatic);

public sealed record SimulationRunStepRequest(
    SimulationRunStepRequestKind Step,
    int? TargetStep = null);

public sealed record EconomyKpiItem(string Label, string Value, string Tone, string Detail);

public sealed record BusinessUnitNodeItem(string Path, string DisplayName, string Role, string Status, Amount Balance);

public sealed record AddressBindingItem(string BusinessUnit, string Role, string Address, string Status, string Validity);

public sealed record TemplateOptionItem(string TemplateId, string DisplayName, string Kind, string Status, string OutputSummary);

public sealed record EventTimelineItem(DateOnly Date, string EventId, string Kind, string Status, string Category, Amount Amount, string Confidence);

public sealed record ScenarioIssueItem(string Severity, string Code, DateOnly Date, string BusinessUnit, string Message);

public sealed record ActualEntryItem(string EntryId, DateOnly Date, string Source, string Status, Amount Amount);

public sealed record ReconciliationItem(string MatchId, string ActualEvent, string PlannedEvent, string Variance, string Status);

public sealed record ObjectDetailItem(string Type, string Id, string Status, string Summary);
