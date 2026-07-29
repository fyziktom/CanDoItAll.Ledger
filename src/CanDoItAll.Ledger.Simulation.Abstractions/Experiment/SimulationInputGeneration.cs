namespace CanDoItAll.Ledger.Simulation.Abstractions;

public interface ISimulationInputGenerator
{
    SimulationInputGenerationResult Generate(SimulationInputGenerationRequest request);
}

public sealed class SimulationInputGenerationRequest
{
    public string GeneratorId { get; set; } = "deterministic-placement-generator";
    public string GeneratorVersion { get; set; } = "1";
    public int Seed { get; set; }
    public string ScenarioId { get; set; } = string.Empty;
    public List<SimulationRef> Entities { get; set; } = [];
    public string OutputDirectory { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class SimulationInputGenerationResult
{
    public SimulationPlacementDefinition Placement { get; set; } = new();
    public SimulationGeneratedInputProvenance Provenance { get; set; } = new();
    public string OutputPath { get; set; } = string.Empty;
    public string OutputHash { get; set; } = string.Empty;
}

public sealed class DeterministicSimulationPlacementGenerator : ISimulationInputGenerator
{
    private readonly SimulationExperimentInputSerializer serializer = new();

    public SimulationInputGenerationResult Generate(SimulationInputGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var random = new Random(request.Seed);
        var placement = new SimulationPlacementDefinition
        {
            PlacementId = $"placement.{request.ScenarioId}.{request.Seed}",
            ScenarioId = request.ScenarioId,
            Metadata = new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal)
        };

        foreach (SimulationRef entity in request.Entities.OrderBy(static item => item.Kind).ThenBy(static item => item.Id, StringComparer.Ordinal))
        {
            placement.Entities.Add(new()
            {
                RefKind = entity.Kind,
                RefId = entity.Id,
                X = Math.Round((decimal)(random.NextDouble() * 10), 3),
                Y = Math.Round((decimal)(random.NextDouble() * 10), 3),
                Z = 0
            });
        }

        var provenance = new SimulationGeneratedInputProvenance
        {
            GeneratorId = request.GeneratorId,
            GeneratorVersion = request.GeneratorVersion,
            Seed = request.Seed,
            GeneratedAtUtc = DateTimeOffset.UnixEpoch,
            RequestHash = SimulationDeterministicHash.HashInputGenerationRequest(request)
        };
        placement.GeneratedBy = provenance;
        provenance.OutputHash = SimulationDeterministicHash.HashPlacement(placement);

        string outputPath = string.IsNullOrWhiteSpace(request.OutputDirectory)
            ? string.Empty
            : Path.Combine(request.OutputDirectory, "placement.json");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            serializer.SaveFile(outputPath, placement);
        }

        return new()
        {
            Placement = placement,
            Provenance = provenance,
            OutputPath = outputPath,
            OutputHash = provenance.OutputHash
        };
    }
}
