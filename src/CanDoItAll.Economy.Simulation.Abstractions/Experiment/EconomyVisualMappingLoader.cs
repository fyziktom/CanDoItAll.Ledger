namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class EconomyVisualMappingLoadResult
{
    public EconomyVisualMappingDefinition Mapping { get; set; } = new();
    public EconomyVisualMappingValidationResult Validation { get; set; } = new();
    public bool IsValid => Validation.IsValid;
}

public sealed class EconomyVisualMappingLoader
{
    private readonly SimulationExperimentInputSerializer serializer = new();
    private readonly EconomyVisualMappingValidator validator = new();

    public EconomyVisualMappingLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EconomyVisualMappingDefinition mapping = serializer.LoadFile<EconomyVisualMappingDefinition>(path);
        return new()
        {
            Mapping = mapping,
            Validation = validator.Validate(mapping)
        };
    }

    public EconomyVisualMappingLoadResult LoadStrict(string path)
    {
        EconomyVisualMappingLoadResult result = Load(path);
        if (!result.IsValid)
        {
            string diagnostics = string.Join(Environment.NewLine, result.Validation.Messages.Select(static message => $"{message.Code}:{message.Path}:{message.Message}"));
            throw new InvalidOperationException($"Visual mapping '{path}' failed strict validation.{Environment.NewLine}{diagnostics}");
        }

        return result;
    }
}
