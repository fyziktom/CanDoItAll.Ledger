using System.Text.Json;

namespace CanDoItAll.Economy.Simulation.Abstractions;

public interface ISimulationExperimentInputPackLoader
{
    SimulationExperimentLoadResult Load(string experimentJsonPath, SimulationExperimentLoadOptions options);
}

public sealed class SimulationExperimentLoadOptions
{
    public string BaseDirectory { get; set; } = string.Empty;
    public SimulationExperimentMode Mode { get; set; } = SimulationExperimentMode.Exploratory;
    public bool StrictMode { get; set; }
    public SimulationExperimentPolicy? Policy { get; set; }
    public bool VerifyDocumentReferences { get; set; } = true;
    public bool VerifyDocumentContentHashes { get; set; }
    public bool VerifyPackHash { get; set; }
}

public sealed class SimulationExperimentLoadResult
{
    public SimulationExperimentInputPack ExperimentInputPack { get; set; } = new();
    public SimulationScenarioDefinition ScenarioDefinition { get; set; } = new();
    public SimulationPlacementDefinition Placement { get; set; } = new();
    public SimulationParameterSetDefinition Parameters { get; set; } = new();
    public SimulationInstitutionRulesDefinition InstitutionRules { get; set; } = new();
    public SimulationRunPlanDefinition RunPlan { get; set; } = new();
    public EconomyVisualMappingDefinition VisualMapping { get; set; } = new();
    public SimulationExpectedInvariantsDocument Invariants { get; set; } = new();
    public SimulationEventStream EventStream { get; set; } = new();
    public SimulationExperimentPolicy Policy { get; set; } = SimulationExperimentPolicy.ExploratoryDemo;
    public Dictionary<string, string> Hashes { get; set; } = [];
    public List<SimulationScenarioDefinitionValidationMessage> Diagnostics { get; set; } = [];
    public bool IsValid => Diagnostics.All(message => !string.Equals(message.Severity, "error", StringComparison.OrdinalIgnoreCase));
}

public sealed class SimulationInstitutionRulesDefinition
{
    public string SchemaVersion { get; set; } = "institution-rules/v1";
    public List<SimulationInstitutionRuleDefinition> Rules { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationInstitutionRuleDefinition
{
    public string RuleId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationExpectedInvariantsDocument
{
    public string SchemaVersion { get; set; } = "invariants/v1";
    public List<SimulationMetricDefinition> Metrics { get; set; } = [];
    public List<SimulationExpectedInvariantDefinition> Invariants { get; set; } = [];
}

public sealed class SimulationExperimentInputPackLoader : ISimulationExperimentInputPackLoader
{
    private readonly SimulationExperimentInputSerializer serializer = new();

    public SimulationExperimentLoadResult Load(string experimentJsonPath, SimulationExperimentLoadOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentJsonPath);
        ArgumentNullException.ThrowIfNull(options);

        string baseDirectory = string.IsNullOrWhiteSpace(options.BaseDirectory)
            ? Path.GetDirectoryName(Path.GetFullPath(experimentJsonPath)) ?? Directory.GetCurrentDirectory()
            : options.BaseDirectory;
        SimulationExperimentPolicy policy = options.Policy ?? SimulationExperimentPolicy.FromLegacyOptions(options.Mode, options.StrictMode);
        var result = new SimulationExperimentLoadResult();
        result.Policy = policy;
        result.ExperimentInputPack = serializer.LoadFile<SimulationExperimentInputPack>(experimentJsonPath);

        SimulationScenarioDefinitionValidationResult packValidation = new SimulationExperimentInputPackValidator().Validate(
            result.ExperimentInputPack,
            new()
            {
                BaseDirectory = baseDirectory,
                StrictHashValidation = policy.StrictMode,
                VerifyDocumentReferences = options.VerifyDocumentReferences || policy.VerifyDocumentReferences,
                VerifyDocumentContentHashes = options.VerifyDocumentContentHashes || policy.VerifyDocumentContentHashes || policy.StrictMode,
                VerifyPackHash = options.VerifyPackHash || policy.VerifyPackHash || policy.StrictMode
            });
        result.Diagnostics.AddRange(packValidation.Messages);
        foreach (SimulationInputDocumentRef input in result.ExperimentInputPack.Inputs)
        {
            result.Hashes[input.Kind] = input.ContentHash;
        }

        result.Hashes["pack"] = result.ExperimentInputPack.Hashes.PackHash;
        if (!packValidation.IsValid)
        {
            return result;
        }

        result.ScenarioDefinition = LoadInput<SimulationScenarioDefinition>(result, baseDirectory, "scenario");
        if (policy.Mode is not SimulationExperimentMode.Exploratory)
        {
            result.ScenarioDefinition.ExperimentMode = policy.Mode;
        }

        string packMode = result.ExperimentInputPack.Metadata.GetValueOrDefault("experimentMode", string.Empty);
        if (!string.IsNullOrWhiteSpace(packMode) && result.ScenarioDefinition.ExperimentMode is SimulationExperimentMode.Exploratory)
        {
            result.ScenarioDefinition.ExperimentMode = SimulationExperimentModeNames.Parse(packMode);
        }

        result.Placement = LoadInput<SimulationPlacementDefinition>(result, baseDirectory, "placement");
        result.Parameters = LoadInput<SimulationParameterSetDefinition>(result, baseDirectory, "parameters");
        result.InstitutionRules = LoadInput<SimulationInstitutionRulesDefinition>(result, baseDirectory, "rules");
        result.RunPlan = LoadInput<SimulationRunPlanDefinition>(result, baseDirectory, "run-plan");
        result.VisualMapping = LoadVisualMapping(result, baseDirectory);
        result.Invariants = LoadInput<SimulationExpectedInvariantsDocument>(result, baseDirectory, "invariants");

        var placementValidator = new SimulationPlacementParameterValidator();
        result.ScenarioDefinition = placementValidator.ApplyPlacementAndParameters(result.ScenarioDefinition, result.Placement, result.Parameters);
        result.Diagnostics.AddRange(placementValidator.ValidatePlacement(result.Placement).Messages);
        result.Diagnostics.AddRange(placementValidator.ValidateParameters(result.Parameters).Messages);

        SimulationScenarioDefinitionNormalizationResult normalized = new SimulationScenarioDefinitionNormalizer().Normalize(result.ScenarioDefinition);
        result.ScenarioDefinition = normalized.Definition;
        result.Diagnostics.AddRange(normalized.Messages);
        result.Diagnostics.AddRange(new SimulationScenarioDefinitionValidator().Validate(result.ScenarioDefinition).Messages);
        result.Diagnostics.AddRange(new SimulationMetricInvariantDefinitionValidator().Validate(
            result.Invariants.Metrics,
            result.Invariants.Invariants,
            new() { Mode = result.ScenarioDefinition.ExperimentMode }).Messages);
        PromoteUnapprovedWarnings(result.Diagnostics, result.ScenarioDefinition, policy);
        result.EventStream = new SimulationScenarioEventCompiler().Compile(result.ScenarioDefinition);
        return result;
    }

    private T LoadInput<T>(SimulationExperimentLoadResult result, string baseDirectory, string kind)
        where T : new()
    {
        SimulationInputDocumentRef? input = result.ExperimentInputPack.Inputs.FirstOrDefault(item => string.Equals(item.Kind, kind, StringComparison.Ordinal));
        if (input is null)
        {
            result.Diagnostics.Add(new() { Code = "missing-input", Path = $"experimentInput.inputs.{kind}", Message = $"Input '{kind}' was not found." });
            return new T();
        }

        try
        {
            return serializer.LoadFile<T>(SimulationExperimentInputPackValidator.ResolveInputPath(baseDirectory, input.Path));
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            result.Diagnostics.Add(new() { Code = "load-input-failed", Path = $"experimentInput.inputs.{kind}", Message = ex.Message });
            return new T();
        }
    }

    private EconomyVisualMappingDefinition LoadVisualMapping(SimulationExperimentLoadResult result, string baseDirectory)
    {
        SimulationInputDocumentRef? input = result.ExperimentInputPack.Inputs.FirstOrDefault(static item => item.Kind == "visual-mapping");
        if (input is null)
        {
            result.Diagnostics.Add(new() { Code = "missing-input", Path = "experimentInput.inputs.visual-mapping", Message = "Input 'visual-mapping' was not found." });
            return new();
        }

        try
        {
            EconomyVisualMappingLoadResult loaded = new EconomyVisualMappingLoader().Load(SimulationExperimentInputPackValidator.ResolveInputPath(baseDirectory, input.Path));
            result.Diagnostics.AddRange(loaded.Validation.Messages);
            return loaded.Mapping;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            result.Diagnostics.Add(new() { Code = "load-input-failed", Path = "experimentInput.inputs.visual-mapping", Message = ex.Message });
            return new();
        }
    }

    private static void PromoteUnapprovedWarnings(
        List<SimulationScenarioDefinitionValidationMessage> diagnostics,
        SimulationScenarioDefinition definition,
        SimulationExperimentPolicy policy)
    {
        if (definition.ExperimentMode is not (SimulationExperimentMode.Strict or SimulationExperimentMode.ResearchGrade) &&
            !policy.ResearchClaimsAllowed)
        {
            return;
        }

        HashSet<string> allowed = [.. definition.AllowedWarningCodes.Concat(policy.AllowlistedWarningCodes)];
        foreach (SimulationScenarioDefinitionValidationMessage warning in diagnostics
            .Where(message => string.Equals(message.Severity, "warning", StringComparison.OrdinalIgnoreCase) &&
                              !allowed.Contains(message.Code)))
        {
            warning.Severity = "error";
            warning.Message = $"Strict mode does not allow warning '{warning.Code}': {warning.Message}";
        }
    }
}
