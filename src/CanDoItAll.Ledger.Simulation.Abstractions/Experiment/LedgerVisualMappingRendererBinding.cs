namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class LedgerVisualRendererBindingDefinition
{
    public string RendererKey { get; set; } = string.Empty;
    public Dictionary<string, string> CategoryAssetIds { get; set; } = [];
    public Dictionary<string, string> NodeAssetIds { get; set; } = [];
    public Dictionary<string, string> PoseAssetIds { get; set; } = [];
    public Dictionary<string, string> PoseVariantIds { get; set; } = [];
    public Dictionary<string, string> SymbolAssetIds { get; set; } = [];
    public Dictionary<string, string> AnchorKeys { get; set; } = [];
    public string DiagnosticObjectId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
