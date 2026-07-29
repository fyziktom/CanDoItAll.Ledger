using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class SimulationExperimentDefinition
{
    public string SchemaVersion { get; set; } = "candoitall.economy.experiment/v1";
    public string ExperimentId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SimulationTreatmentDefinition Treatment { get; set; } = new();
    public SimulationHypothesisDefinition Hypothesis { get; set; } = new();
    public List<SimulationMetricDefinition> Metrics { get; set; } = [];
    public List<SimulationExpectedInvariantDefinition> ExpectedInvariants { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationExperimentInputPack
{
    public string SchemaVersion { get; set; } = "candoitall.economy.experiment-input/v1";
    public string ExperimentId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SimulationHypothesisDefinition Hypothesis { get; set; } = new();
    public SimulationTreatmentDefinition Treatment { get; set; } = new();
    public List<SimulationInputDocumentRef> Inputs { get; set; } = [];
    public SimulationInputHashManifest Hashes { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationInputDocumentRef
{
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationInputHashManifest
{
    public string PackHash { get; set; } = string.Empty;
    public Dictionary<string, string> InputHashes { get; set; } = [];
}

public sealed class SimulationExperimentInputPackValidationOptions
{
    public string BaseDirectory { get; set; } = string.Empty;
    public bool VerifyDocumentReferences { get; set; }
    public bool VerifyDocumentContentHashes { get; set; }
    public bool VerifyPackHash { get; set; }
    public bool StrictHashValidation { get; set; }
    public bool AllowExtraHashManifestEntries { get; set; }
    public List<string> RequiredInputKinds { get; set; } = [];
}

public sealed class SimulationRunPlanDefinition
{
    public string RunPlanId { get; set; } = string.Empty;
    public string BackendId { get; set; } = string.Empty;
    public int StartStepIndex { get; set; }
    public int EndStepIndex { get; set; }
    public string SeedRef { get; set; } = string.Empty;
    public Dictionary<string, string> OutputSettings { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationTreatmentDefinition
{
    public string TreatmentId { get; set; } = string.Empty;
    public string InstitutionId { get; set; } = string.Empty;
    public Dictionary<string, string> Factors { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationHypothesisDefinition
{
    public string HypothesisId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<string> MetricIds { get; set; } = [];
}

public sealed class SimulationMetricDefinition
{
    public string MetricId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Aggregation { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationExpectedInvariantDefinition
{
    public string InvariantId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "error";
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationExperimentInputPackValidator
{
    private const string SchemaVersion = "candoitall.economy.experiment-input/v1";
    private static readonly Regex ContentHashPattern = new("^sha256:[A-Za-z0-9._:-]+$", RegexOptions.Compiled);
    private static readonly Regex StrictContentHashPattern = new("^sha256:[0-9a-f]{64}$", RegexOptions.Compiled);

    private static readonly HashSet<string> RequiredKinds = new(StringComparer.Ordinal)
    {
        "scenario",
        "placement",
        "parameters",
        "rules",
        "run-plan",
        "visual-mapping",
        "invariants"
    };

    public SimulationScenarioDefinitionValidationResult Validate(SimulationExperimentInputPack pack)
        => Validate(pack, new SimulationExperimentInputPackValidationOptions());

    public SimulationScenarioDefinitionValidationResult Validate(
        SimulationExperimentInputPack pack,
        SimulationExperimentInputPackValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(options);
        var result = new SimulationScenarioDefinitionValidationResult();
        Require(
            string.Equals(pack.SchemaVersion, SchemaVersion, StringComparison.Ordinal),
            "experimentInput.schemaVersion",
            $"Input pack schema version must be '{SchemaVersion}'.",
            result);
        Require(!string.IsNullOrWhiteSpace(pack.ExperimentId), "experimentInput.experimentId", "Experiment id is required.", result);

        string[] requiredKinds = options.RequiredInputKinds.Count == 0 ? [.. RequiredKinds] : [.. options.RequiredInputKinds];
        Dictionary<string, int> kindCounts = pack.Inputs
            .Where(static input => !string.IsNullOrWhiteSpace(input.Kind))
            .GroupBy(static input => input.Kind, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        foreach (string kind in requiredKinds)
        {
            Require(kindCounts.ContainsKey(kind), $"experimentInput.inputs.{kind}", $"Input pack must reference a {kind} document.", result);
        }

        foreach (KeyValuePair<string, int> count in kindCounts.Where(static item => item.Value > 1))
        {
            Error("duplicate-input-kind", $"experimentInput.inputs.{count.Key}", $"Input pack contains duplicate '{count.Key}' document references.", result);
        }

        foreach (SimulationInputDocumentRef input in pack.Inputs)
        {
            Require(!string.IsNullOrWhiteSpace(input.Kind), "experimentInput.inputs.kind", "Input kind is required.", result);
            Require(!string.IsNullOrWhiteSpace(input.Path), $"experimentInput.inputs.{input.Kind}.path", "Input path is required.", result);
            Require(IsSafeRelativePath(input.Path), $"experimentInput.inputs.{input.Kind}.path", "Input path must be a safe relative path inside the input pack directory.", result);
            Require(!string.IsNullOrWhiteSpace(input.SchemaVersion), $"experimentInput.inputs.{input.Kind}.schemaVersion", "Input schema version is required.", result);
            Require(IsValidContentHash(input.ContentHash, options.StrictHashValidation), $"experimentInput.inputs.{input.Kind}.contentHash", options.StrictHashValidation
                ? "Input content hash must use sha256:<64 lowercase hexadecimal characters> in strict mode."
                : "Input content hash must use sha256:<value> format.", result);
            if (pack.Hashes.InputHashes.TryGetValue(input.Kind, out string? manifestHash))
            {
                Require(IsValidContentHash(manifestHash, options.StrictHashValidation), $"experimentInput.hashes.{input.Kind}", options.StrictHashValidation
                    ? "Hash manifest entry must use sha256:<64 lowercase hexadecimal characters> in strict mode."
                    : "Hash manifest entry must use sha256:<value> format.", result);
                Require(
                    string.Equals(NormalizeHash(manifestHash), NormalizeHash(input.ContentHash), StringComparison.Ordinal),
                    $"experimentInput.hashes.{input.Kind}",
                    "Hash manifest entry must match the input reference hash.",
                    result);
            }
            else if (options.StrictHashValidation)
            {
                Error("missing-input-hash", $"experimentInput.hashes.{input.Kind}", "Strict mode requires a matching hash manifest entry for every input document.", result);
            }

            if (options.VerifyDocumentReferences || options.VerifyDocumentContentHashes)
            {
                string? fullPath = TryResolveInputPath(options.BaseDirectory, input.Path);
                Require(fullPath is not null, $"experimentInput.inputs.{input.Kind}.path", "Input path must resolve under the input pack base directory.", result);
                Require(fullPath is not null && File.Exists(fullPath), $"experimentInput.inputs.{input.Kind}.path", $"Input document '{input.Path}' was not found.", result);
                if (options.VerifyDocumentContentHashes && File.Exists(fullPath))
                {
                    string actualHash = HashFile(fullPath);
                    Require(
                        string.Equals(NormalizeHash(input.ContentHash), NormalizeHash(actualHash), StringComparison.Ordinal),
                        $"experimentInput.inputs.{input.Kind}.contentHash",
                        $"Input document '{input.Path}' content hash did not match.",
                        result);
                }
            }
        }

        if (options.StrictHashValidation && !options.AllowExtraHashManifestEntries)
        {
            HashSet<string> inputKinds = pack.Inputs.Select(static input => input.Kind).Where(static kind => !string.IsNullOrWhiteSpace(kind)).ToHashSet(StringComparer.Ordinal);
            foreach (string key in pack.Hashes.InputHashes.Keys.Where(key => !inputKinds.Contains(key)))
            {
                Error("extra-input-hash", $"experimentInput.hashes.{key}", "Strict mode does not allow hash manifest entries without a matching input document.", result);
            }
        }

        if (options.VerifyPackHash || !string.IsNullOrWhiteSpace(pack.Hashes.PackHash))
        {
            Require(!string.IsNullOrWhiteSpace(pack.Hashes.PackHash), "experimentInput.hashes.packHash", "Pack hash is required when pack hash verification is enabled.", result);
            Require(!options.StrictHashValidation || IsValidContentHash(pack.Hashes.PackHash, strict: true), "experimentInput.hashes.packHash", "Pack hash must use sha256:<64 lowercase hexadecimal characters> in strict mode.", result);
            string actualHash = SimulationDeterministicHash.HashInputPack(pack);
            Require(
                string.Equals(NormalizeHash(pack.Hashes.PackHash), NormalizeHash(actualHash), StringComparison.Ordinal),
                "experimentInput.hashes.packHash",
                "Pack hash does not match the canonical input-pack hash.",
                result);
        }

        return result;
    }

    public static string HashFile(string path)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()}";

    public static string ResolveInputPath(string baseDirectory, string relativePath)
        => TryResolveInputPath(baseDirectory, relativePath)
            ?? throw new InvalidOperationException($"Input path '{relativePath}' resolves outside '{baseDirectory}'.");

    public static string? TryResolveInputPath(string baseDirectory, string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            return null;
        }

        string root = string.IsNullOrWhiteSpace(baseDirectory) ? Directory.GetCurrentDirectory() : baseDirectory;
        string fullRoot = Path.GetFullPath(root);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        string rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }

    private static bool IsSafeRelativePath(string path)
        => !string.IsNullOrWhiteSpace(path) &&
           !Path.IsPathRooted(path) &&
           !path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(static segment => segment == "..");

    private static bool IsValidContentHash(string value, bool strict)
        => !string.IsNullOrWhiteSpace(value) && (strict ? StrictContentHashPattern : ContentHashPattern).IsMatch(value);

    private static string NormalizeHash(string value)
        => value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? value["sha256:".Length..].ToLowerInvariant()
            : value.ToLowerInvariant();

    private static void Require(bool condition, string path, string message, SimulationScenarioDefinitionValidationResult result)
    {
        if (!condition)
        {
            Error("invalid-input-pack", path, message, result);
        }
    }

    private static void Error(string code, string path, string message, SimulationScenarioDefinitionValidationResult result)
        => result.Messages.Add(new() { Code = code, Path = path, Message = message });
}

public sealed class SimulationExperimentInputSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    public T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new JsonException($"JSON did not contain a {typeof(T).Name} document.");

    public T LoadFile<T>(string path)
        => Deserialize<T>(File.ReadAllText(path));

    public void SaveFile<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Serialize(value));
    }
}
