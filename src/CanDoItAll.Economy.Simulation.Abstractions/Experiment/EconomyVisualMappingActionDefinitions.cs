namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class EconomyVisualActionMappingDefinition
{
    public string EventKind { get; set; } = string.Empty;
    public string ActionKind { get; set; } = string.Empty;
    public string PoseKey { get; set; } = string.Empty;
    public string SymbolCategory { get; set; } = string.Empty;
    public string AnchorAlias { get; set; } = string.Empty;
    public string AnchorKey { get; set; } = string.Empty;
    public bool AllowNoOpFallback { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class EconomyVisualSymbolMappingDefinition
{
    public string RuleCategory { get; set; } = string.Empty;
    public string ResourceKind { get; set; } = string.Empty;
    public string SymbolCategory { get; set; } = string.Empty;
    public string SymbolKey { get; set; } = string.Empty;
    public string SymbolAssetId { get; set; } = string.Empty;
    public string Color { get; set; } = "#facc15";
    public string EffectKey { get; set; } = "pulse";
    public string Tooltip { get; set; } = string.Empty;
    public bool IsNoOpFallback { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class EconomyVisualPoseHintDefinition
{
    public string ActionKind { get; set; } = string.Empty;
    public string PoseKey { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string AssetVariantId { get; set; } = string.Empty;
    public decimal ScaleX { get; set; } = 1;
    public decimal ScaleY { get; set; } = 1;
    public decimal ScaleZ { get; set; } = 1;
    public bool IsNoOpFallback { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class EconomyVisualAnchorAliasDefinition
{
    public string Alias { get; set; } = string.Empty;
    public string AnchorKey { get; set; } = string.Empty;
    public decimal OffsetX { get; set; }
    public decimal OffsetY { get; set; }
    public decimal OffsetZ { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
