namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class LedgerVisualNodeMappingDefinition
{
    public string SourceKind { get; set; } = string.Empty;
    public string VisualCategory { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string AnchorAlias { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class LedgerVisualCategoryMappingDefinition
{
    public string VisualCategory { get; set; } = string.Empty;
    public string ObjectKind { get; set; } = "object";
    public string Family { get; set; } = "generic";
    public string AssetId { get; set; } = string.Empty;
    public string Tone { get; set; } = "neutral";
    public string Color { get; set; } = "#ffffff";
    public decimal SizeX { get; set; } = 1;
    public decimal SizeY { get; set; } = 1;
    public decimal SizeZ { get; set; } = 1;
    public string AnchorAlias { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class LedgerVisualRelationshipMappingDefinition
{
    public string RelationshipKind { get; set; } = string.Empty;
    public string VisualCategory { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class LedgerVisualFallbackDefinition
{
    public string MissingNodeCategory { get; set; } = "unknown-node";
    public string MissingActionKind { get; set; } = "wait";
    public string MissingSymbolCategory { get; set; } = "status";
    public bool AllowNoOpPoseFallback { get; set; } = true;
    public bool AllowNoOpSymbolFallback { get; set; } = true;
    public string DiagnosticObjectId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
