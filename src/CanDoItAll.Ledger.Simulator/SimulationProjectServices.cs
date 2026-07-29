using System.Text.Json;

namespace CanDoItAll.Ledger.Simulator;

public interface ISimulationProjectValidationService
{
    SimulationProjectValidationResult Validate(SimulationProjectState project);
}

public interface ISimulationProjectImportExportService
{
    SimulationProjectExportManifest CreateManifest(SimulationProjectState project, DateTimeOffset exportedAtUtc);

    string SerializeManifest(SimulationProjectExportManifest manifest);

    SimulationProjectImportPreview PreviewImport(string manifestJson);

    string BuildDeterministicRunSetupHash(SimulationProjectState project, SimulationRunSetupState setup);
}

public interface ISimulationRunObservationService
{
    SimulationRunObservationState BuildObservation(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        ScenarioRunState run,
        SimulationResultState result,
        IReadOnlyList<LedgerGraphData> graphs,
        IReadOnlyList<RuleExecutionEvidenceState> ruleEvidence);
}

public sealed class SimulationProjectValidationService : ISimulationProjectValidationService
{
    public SimulationProjectValidationResult Validate(SimulationProjectState project)
    {
        var messages = new List<SimulationProjectValidationMessage>();
        ValidateRoot(project, messages);
        ValidateFolders(project, messages);
        ValidateBusinessUnits(project, messages);
        ValidateDefinitions(project, messages);
        ValidateFinancialInputs(project, messages);
        ValidateRunSetups(project, messages);
        return new SimulationProjectValidationResult(messages);
    }

    private static void ValidateRoot(SimulationProjectState project, ICollection<SimulationProjectValidationMessage> messages)
    {
        if (!string.Equals(project.Summary.ProjectId, project.Model.ProjectId, StringComparison.Ordinal))
        {
            messages.Add(Error(
                "project-id-mismatch",
                "Project summary and model project ids must match.",
                "summary.projectId",
                project.Summary.ProjectId));
        }

        if (project.Model.PlanningEndDate < project.Model.PlanningStartDate)
        {
            messages.Add(Error(
                "invalid-planning-window",
                "Project planning end date cannot be before planning start date.",
                "model.planningEndDate",
                project.Model.ProjectId));
        }

        if (project.Metadata.TryGetValue("scenarioProject", out string? scenarioProjectValue)
            && bool.TryParse(scenarioProjectValue, out bool isScenarioProject)
            && isScenarioProject
            && (!project.Metadata.TryGetValue("scenarioCode", out string? scenarioCode)
                || string.IsNullOrWhiteSpace(scenarioCode)))
        {
            messages.Add(Error(
                "missing-scenario-code",
                "Scenario projects require non-empty 'scenarioCode' metadata.",
                "metadata.scenarioCode",
                project.Summary.ProjectId));
        }
    }

    private static void ValidateFolders(SimulationProjectState project, ICollection<SimulationProjectValidationMessage> messages)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (SimulationProjectFolderState folder in project.Folders)
        {
            if (!ids.Add(folder.FolderId))
            {
                messages.Add(Error("duplicate-folder-id", $"Folder id '{folder.FolderId}' is duplicated.", "folders", folder.FolderId));
            }
        }

        foreach (SimulationProjectFolderState folder in project.Folders.Where(x => !string.IsNullOrWhiteSpace(x.ParentFolderId)))
        {
            if (!ids.Contains(folder.ParentFolderId!))
            {
                messages.Add(Error("invalid-folder-parent", $"Folder '{folder.FolderId}' references missing parent folder '{folder.ParentFolderId}'.", "folders", folder.FolderId));
            }
        }
    }

    private static void ValidateBusinessUnits(SimulationProjectState project, ICollection<SimulationProjectValidationMessage> messages)
    {
        HashSet<BusinessUnitId> businessUnitIds = new();
        foreach (BusinessUnitState businessUnit in project.BusinessUnits)
        {
            if (!businessUnitIds.Add(businessUnit.BusinessUnitId))
            {
                messages.Add(Error("duplicate-business-unit-id", $"Business unit '{businessUnit.BusinessUnitId}' is duplicated.", "businessUnits", businessUnit.BusinessUnitId.Value));
            }

            if (!businessUnit.KnownAddresses.Contains(businessUnit.PrimaryAddress))
            {
                messages.Add(Error("missing-primary-address", $"Business unit '{businessUnit.DisplayName}' primary address is not part of known addresses.", "businessUnits.primaryAddress", businessUnit.BusinessUnitId.Value));
            }
        }

        foreach (BusinessUnitState businessUnit in project.BusinessUnits.Where(x => x.ParentBusinessUnitId is not null))
        {
            if (!businessUnitIds.Contains(businessUnit.ParentBusinessUnitId!.Value))
            {
                messages.Add(Error("invalid-business-unit-parent", $"Business unit '{businessUnit.BusinessUnitId}' references missing parent '{businessUnit.ParentBusinessUnitId}'.", "businessUnits.parentBusinessUnitId", businessUnit.BusinessUnitId.Value));
            }
        }

        foreach (BusinessUnitAddressBindingState binding in project.AddressBindings)
        {
            if (!businessUnitIds.Contains(binding.BusinessUnitId))
            {
                messages.Add(Error("invalid-address-binding-business-unit", $"Address binding '{binding.Address}' references missing business unit '{binding.BusinessUnitId}'.", "addressBindings.businessUnitId", binding.BusinessUnitId.Value));
            }

            if (binding.ParentBusinessUnitId is not null && !businessUnitIds.Contains(binding.ParentBusinessUnitId.Value))
            {
                messages.Add(Error("invalid-address-binding-parent", $"Address binding '{binding.Address}' references missing parent business unit '{binding.ParentBusinessUnitId}'.", "addressBindings.parentBusinessUnitId", binding.BusinessUnitId.Value));
            }

            if (binding.ValidToUtc is not null && binding.ValidToUtc < binding.ValidFromUtc)
            {
                messages.Add(Error("invalid-address-binding-window", $"Address binding '{binding.Address}' valid-to date cannot be before valid-from date.", "addressBindings.validToUtc", binding.BusinessUnitId.Value));
            }
        }
    }

    private static void ValidateDefinitions(SimulationProjectState project, ICollection<SimulationProjectValidationMessage> messages)
    {
        HashSet<string> folders = project.Folders.Select(x => x.FolderId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> definitions = new(StringComparer.Ordinal);
        HashSet<BusinessUnitId> businessUnits = project.BusinessUnits.Select(x => x.BusinessUnitId).ToHashSet();
        ILedgerSimulatorObjectTypeCatalog catalog = LedgerSimulatorObjectTypeCatalog.Instance;

        foreach (SimulationBusinessObjectDefinitionState definition in project.BusinessObjectDefinitions)
        {
            if (!definitions.Add(definition.DefinitionId))
            {
                messages.Add(Error("duplicate-bo-definition-id", $"BO definition id '{definition.DefinitionId}' is duplicated.", "businessObjectDefinitions", definition.DefinitionId));
            }

            if (!string.IsNullOrWhiteSpace(definition.FolderId) && !folders.Contains(definition.FolderId!))
            {
                messages.Add(Error("invalid-bo-folder", $"BO definition '{definition.DefinitionId}' references missing folder '{definition.FolderId}'.", "businessObjectDefinitions.folderId", definition.DefinitionId));
            }

            if (definition.OwnerBusinessUnitId is not null && !businessUnits.Contains(definition.OwnerBusinessUnitId.Value))
            {
                messages.Add(Error("invalid-bo-owner", $"BO definition '{definition.DefinitionId}' references missing owner business unit '{definition.OwnerBusinessUnitId}'.", "businessObjectDefinitions.ownerBusinessUnitId", definition.DefinitionId));
            }

            if (!catalog.Contains(definition.ObjectType))
            {
                messages.Add(Error("unknown-bo-object-type", $"BO definition '{definition.DefinitionId}' uses unknown object type '{definition.ObjectType}'.", "businessObjectDefinitions.objectType", definition.DefinitionId));
            }

            if (!string.IsNullOrWhiteSpace(definition.StateJson))
            {
                try
                {
                    JsonDocument.Parse(definition.StateJson);
                }
                catch (JsonException ex)
                {
                    messages.Add(Error("invalid-bo-state-json", $"BO definition '{definition.DefinitionId}' has invalid state JSON: {ex.Message}", "businessObjectDefinitions.stateJson", definition.DefinitionId));
                }
            }

            if (definition.RuleDefinition is not null && !RuleTokenAuthority.TryValidate(definition.RuleDefinition, out IReadOnlyList<string> ruleErrors))
            {
                foreach (string ruleError in ruleErrors)
                {
                    messages.Add(Error("invalid-rule-definition", ruleError, "businessObjectDefinitions.ruleDefinition", definition.DefinitionId));
                }
            }
        }
    }

    private static void ValidateFinancialInputs(SimulationProjectState project, ICollection<SimulationProjectValidationMessage> messages)
    {
        HashSet<BusinessUnitId> businessUnits = project.BusinessUnits.Select(x => x.BusinessUnitId).ToHashSet();
        foreach (SimulationFinancialInputProfileState profile in project.FinancialInputProfiles)
        {
            foreach (SimulationFinancialInputState input in profile.Inputs)
            {
                if (input.ValidTo is not null && input.ValidFrom is not null && input.ValidTo < input.ValidFrom)
                {
                    messages.Add(Error("invalid-financial-input-window", $"Input '{input.InputId}' valid-to date cannot be before valid-from date.", "financialInputs.validTo", input.InputId));
                }

                if (input.BusinessUnitId is not null && !businessUnits.Contains(input.BusinessUnitId.Value))
                {
                    messages.Add(Error("invalid-financial-input-business-unit", $"Input '{input.InputId}' references missing business unit '{input.BusinessUnitId}'.", "financialInputs.businessUnitId", input.InputId));
                }

                if (input.Unit != SimulationFinancialInputUnit.Text && input.NumericValue is null)
                {
                    messages.Add(Error("missing-financial-input-value", $"Input '{input.InputId}' requires a numeric value.", "financialInputs.numericValue", input.InputId));
                }

                if (input.NumericValue is not null)
                {
                    ValidateFinancialNumber(input, messages);
                }
            }
        }
    }

    private static void ValidateFinancialNumber(SimulationFinancialInputState input, ICollection<SimulationProjectValidationMessage> messages)
    {
        decimal value = input.NumericValue ?? 0m;
        if (input.Unit is SimulationFinancialInputUnit.Percent && (value < 0m || value > 1m))
        {
            messages.Add(Error("invalid-financial-rate", $"Rate input '{input.InputId}' must be between 0 and 1.", "financialInputs.numericValue", input.InputId));
        }

        if ((input.Unit is SimulationFinancialInputUnit.Days or SimulationFinancialInputUnit.Count or SimulationFinancialInputUnit.Amount) && value < 0m)
        {
            messages.Add(Error("invalid-financial-input-negative", $"Input '{input.InputId}' cannot be negative.", "financialInputs.numericValue", input.InputId));
        }
    }

    private static void ValidateRunSetups(SimulationProjectState project, ICollection<SimulationProjectValidationMessage> messages)
    {
        HashSet<BusinessUnitId> businessUnits = project.BusinessUnits.Select(x => x.BusinessUnitId).ToHashSet();
        foreach (SimulationRunSetupState setup in project.RunSetups)
        {
            if (!Enum.IsDefined(setup.StepPeriod))
            {
                messages.Add(Error("invalid-run-step-period", $"Run setup '{setup.SetupId}' has an unsupported step period.", "runSetups.stepPeriod", setup.SetupId));
            }

            if (setup.StepCount <= 0)
            {
                messages.Add(Error("invalid-run-step-count", $"Run setup '{setup.SetupId}' must include at least one step.", "runSetups.stepCount", setup.SetupId));
            }

            if (setup.HorizonEndDate < setup.HorizonStartDate)
            {
                messages.Add(Error("invalid-run-horizon", $"Run setup '{setup.SetupId}' horizon end cannot be before horizon start.", "runSetups.horizonEndDate", setup.SetupId));
            }

            foreach (BusinessUnitId includedBusinessUnit in setup.IncludedBusinessUnits)
            {
                if (!businessUnits.Contains(includedBusinessUnit))
                {
                    messages.Add(Error("invalid-run-included-business-unit", $"Run setup '{setup.SetupId}' includes missing business unit '{includedBusinessUnit}'.", "runSetups.includedBusinessUnits", setup.SetupId));
                }
            }
        }
    }

    private static SimulationProjectValidationMessage Error(string code, string message, string path, string relatedId)
        => new(code, SimulationValidationSeverity.Error, message, path, relatedId);
}

public sealed class SimulationProjectImportExportService(
    ILedgerSimulatorJsonSerializer serializer,
    ISimulationProjectValidationService validationService) : ISimulationProjectImportExportService
{
    public SimulationProjectExportManifest CreateManifest(SimulationProjectState project, DateTimeOffset exportedAtUtc)
    {
        IReadOnlyDictionary<string, string> contentHashes = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = serializer.HashJson(project.Model),
            ["businessUnits"] = serializer.HashJson(project.BusinessUnits.OrderBy(x => x.BusinessUnitId.Value, StringComparer.Ordinal).ToArray()),
            ["addressBindings"] = serializer.HashJson(project.AddressBindings.OrderBy(x => x.BusinessUnitId.Value, StringComparer.Ordinal).ThenBy(x => x.Role).ThenBy(x => x.Address.Value, StringComparer.Ordinal).ToArray()),
            ["folders"] = serializer.HashJson(project.Folders.OrderBy(x => x.FolderId, StringComparer.Ordinal).ToArray()),
            ["businessObjectDefinitions"] = serializer.HashJson(project.BusinessObjectDefinitions.OrderBy(x => x.DefinitionId, StringComparer.Ordinal).ToArray()),
            ["financialInputProfiles"] = serializer.HashJson(project.FinancialInputProfiles.OrderBy(x => x.ProfileId, StringComparer.Ordinal).ToArray()),
            ["runSetups"] = serializer.HashJson(project.RunSetups.OrderBy(x => x.SetupId, StringComparer.Ordinal).ToArray())
        };

        return new SimulationProjectExportManifest(
            LedgerSimulatorSchema.Version,
            project.Summary.ProjectId,
            exportedAtUtc,
            project,
            contentHashes);
    }

    public string SerializeManifest(SimulationProjectExportManifest manifest)
        => serializer.Serialize(manifest);

    public SimulationProjectImportPreview PreviewImport(string manifestJson)
    {
        try
        {
            SimulationProjectExportManifest? manifest = serializer.Deserialize<SimulationProjectExportManifest>(manifestJson);
            if (manifest is null)
            {
                return InvalidPreview("import-empty", "Import manifest could not be read.");
            }

            SimulationProjectValidationResult validation = validationService.Validate(manifest.Project);
            var warnings = new List<string>();
            if (!string.Equals(manifest.SchemaVersion, LedgerSimulatorSchema.Version, StringComparison.Ordinal))
            {
                warnings.Add($"Manifest schema '{manifest.SchemaVersion}' differs from runtime schema '{LedgerSimulatorSchema.Version}'.");
            }

            return new SimulationProjectImportPreview(manifest, validation, validation.IsValid, warnings);
        }
        catch (JsonException ex)
        {
            return InvalidPreview("import-json-invalid", ex.Message);
        }
    }

    public string BuildDeterministicRunSetupHash(SimulationProjectState project, SimulationRunSetupState setup)
    {
        var canonical = new DeterministicRunSetupInput(
            project.Summary.ProjectId,
            setup.ScenarioId.Value,
            setup.ScenarioName,
            setup.StepPeriod,
            setup.StepCount,
            setup.HorizonStartDate,
            setup.HorizonEndDate,
            setup.Seed,
            setup.IncludedBusinessUnits.Select(x => x.Value).Order(StringComparer.Ordinal).ToArray(),
            setup.IncludedTemplateIds.Order(StringComparer.Ordinal).ToArray(),
            setup.RuleExecutionEnabled,
            setup.AssumptionOverrides
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            serializer.HashJson(project.FinancialInputProfiles.OrderBy(x => x.ProfileId, StringComparer.Ordinal).ToArray()),
            serializer.HashJson(project.BusinessObjectDefinitions.OrderBy(x => x.DefinitionId, StringComparer.Ordinal).ToArray()));

        return serializer.HashJson(canonical);
    }

    private static SimulationProjectImportPreview InvalidPreview(string code, string message)
        => new(
            null,
            new SimulationProjectValidationResult([new SimulationProjectValidationMessage(code, SimulationValidationSeverity.Error, message)]),
            false,
            []);

    private sealed record DeterministicRunSetupInput(
        string ProjectId,
        string ScenarioId,
        string ScenarioName,
        SimulationRunStepPeriod StepPeriod,
        int StepCount,
        DateOnly HorizonStartDate,
        DateOnly HorizonEndDate,
        int Seed,
        IReadOnlyList<string> IncludedBusinessUnits,
        IReadOnlyList<string> IncludedTemplateIds,
        bool RuleExecutionEnabled,
        IReadOnlyDictionary<string, string> AssumptionOverrides,
        string FinancialInputHash,
        string BusinessObjectDefinitionHash);
}

public sealed class SimulationRunObservationService(ILedgerSimulatorJsonSerializer serializer) : ISimulationRunObservationService
{
    public SimulationRunObservationState BuildObservation(
        SimulationProjectState project,
        SimulationRunSetupState setup,
        ScenarioRunState run,
        SimulationResultState result,
        IReadOnlyList<LedgerGraphData> graphs,
        IReadOnlyList<RuleExecutionEvidenceState> ruleEvidence)
    {
        string outputHash = result.OutputHashes.TryGetValue("result", out string? resultHash)
            ? resultHash
            : serializer.HashJson(result);
        int currentStep = result.Metadata.TryGetValue("currentStep", out string? currentStepText)
            && int.TryParse(currentStepText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int currentStepValue)
                ? Math.Clamp(currentStepValue, 1, Math.Max(1, setup.StepCount))
                : Math.Clamp(setup.StepCount, 1, Math.Max(1, setup.StepCount));
        DateOnly currentDate = result.Metadata.TryGetValue("currentDate", out string? currentDateText)
            && DateOnly.TryParseExact(currentDateText, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly currentDateValue)
                ? currentDateValue
                : result.HorizonEndDate;
        var summary = new SimulationRunSummary(
            project.Summary.ProjectId,
            project.Summary.DisplayName,
            run.ScenarioId,
            setup.ScenarioName,
            run.RunId,
            run.Status,
            run.StartedAtUtc,
            run.HorizonStartDate,
            run.HorizonEndDate,
            currentStep,
            setup.StepCount,
            currentDate,
            outputHash);
        var clock = new SimulationRunClockState(currentDate, currentStep, setup.StepCount, setup.StepPeriod, setup.UiSpeed);
        IReadOnlyList<SimulationBusinessUnitAccountSnapshot> snapshots = BuildAccountSnapshots(project, result, ruleEvidence);
        IReadOnlyDictionary<string, string> outputHashes = new SortedDictionary<string, string>(
            result.OutputHashes.Concat(new[] { KeyValuePair.Create("observation", serializer.HashJson(new { run.RunId, setup.SetupId, outputHash })) })
                .GroupBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var artifacts = new[]
        {
            new SimulationRunArtifactState(
                $"artifact:{run.RunId}:result",
                "Run result export",
                "simulation-result",
                outputHash,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["projectId"] = project.Summary.ProjectId,
                    ["scenarioId"] = run.ScenarioId.Value,
                    ["runId"] = run.RunId
                }),
            new SimulationRunArtifactState(
                $"artifact:{run.RunId}:graphs",
                "Graph projection rows",
                "graph-data",
                serializer.HashJson(graphs),
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["rowCount"] = graphs.Sum(x => x.DrilldownRows.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)
                })
        };

        return new SimulationRunObservationState(
            summary,
            clock,
            graphs,
            snapshots,
            project.BusinessObjectDefinitions,
            ruleEvidence,
            result.Issues,
            artifacts,
            outputHashes);
    }

    private static IReadOnlyList<SimulationBusinessUnitAccountSnapshot> BuildAccountSnapshots(
        SimulationProjectState project,
        SimulationResultState result,
        IReadOnlyList<RuleExecutionEvidenceState> ruleEvidence)
    {
        Dictionary<BusinessUnitId, BusinessUnitState> businessUnits = project.BusinessUnits.ToDictionary(x => x.BusinessUnitId);
        return project.AddressBindings
            .OrderBy(x => x.BusinessUnitId.Value, StringComparer.Ordinal)
            .ThenBy(x => x.Role)
            .ThenBy(x => x.Address.Value, StringComparer.Ordinal)
            .Select(binding =>
            {
                businessUnits.TryGetValue(binding.BusinessUnitId, out BusinessUnitState? businessUnit);
                Amount balance = result.CashFlow
                    .Where(x => x.BusinessUnitId == binding.BusinessUnitId)
                    .OrderBy(x => x.Date)
                    .LastOrDefault()
                    ?.BalanceAfter ?? Amount.Zero;
                IReadOnlyList<string> currentObjects = project.BusinessObjectDefinitions
                    .Where(x => x.OwnerBusinessUnitId == binding.BusinessUnitId)
                    .Select(x => x.DefinitionId)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                IReadOnlyList<string> issueIds = result.Issues
                    .Where(x => x.BusinessUnitId == binding.BusinessUnitId)
                    .Select(x => x.IssueId)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                int failedActions = ruleEvidence.Count(x => string.Equals(x.Status, AccountRuleExecutionStatus.Failed.ToString(), StringComparison.OrdinalIgnoreCase));
                int skippedActions = ruleEvidence.Count(x => string.Equals(x.Status, AccountRuleExecutionStatus.Skipped.ToString(), StringComparison.OrdinalIgnoreCase));
                int processedTriggers = ruleEvidence.Count(x => string.Equals(x.Status, AccountRuleExecutionStatus.Executed.ToString(), StringComparison.OrdinalIgnoreCase));

                return new SimulationBusinessUnitAccountSnapshot(
                    $"snapshot:{binding.BusinessUnitId.Value}:{binding.Address.Value}",
                    binding.BusinessUnitId,
                    businessUnit?.DisplayName ?? binding.BusinessUnitId.Value,
                    binding.Address,
                    binding.Role,
                    binding.Status,
                    balance,
                    currentObjects,
                    project.BusinessObjectDefinitions.Any(x => x.Kind == SimulationBusinessObjectDefinitionKind.RuleDefinition && x.OwnerBusinessUnitId == binding.BusinessUnitId),
                    processedTriggers,
                    failedActions,
                    skippedActions,
                    issueIds,
                    binding.Metadata);
            })
            .ToArray();
    }
}
