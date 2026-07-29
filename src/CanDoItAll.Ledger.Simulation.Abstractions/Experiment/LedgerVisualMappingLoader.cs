namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class LedgerVisualMappingLoadResult
{
    public LedgerVisualMappingDefinition Mapping { get; set; } = new();
    public LedgerVisualMappingValidationResult Validation { get; set; } = new();
    public bool IsValid => Validation.IsValid;
}

public sealed class LedgerVisualMappingLoader
{
    private readonly SimulationExperimentInputSerializer serializer = new();
    private readonly LedgerVisualMappingValidator validator = new();

    public LedgerVisualMappingLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        LedgerVisualMappingDefinition mapping = serializer.LoadFile<LedgerVisualMappingDefinition>(path);
        return new()
        {
            Mapping = mapping,
            Validation = validator.Validate(mapping)
        };
    }

    public LedgerVisualMappingLoadResult LoadStrict(string path)
    {
        LedgerVisualMappingLoadResult result = Load(path);
        if (!result.IsValid)
        {
            string diagnostics = string.Join(Environment.NewLine, result.Validation.Messages.Select(static message => $"{message.Code}:{message.Path}:{message.Message}"));
            throw new InvalidOperationException($"Visual mapping '{path}' failed strict validation.{Environment.NewLine}{diagnostics}");
        }

        return result;
    }
}
