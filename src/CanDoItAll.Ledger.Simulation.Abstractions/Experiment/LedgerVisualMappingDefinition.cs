namespace CanDoItAll.Ledger.Simulation.Abstractions;

public sealed class LedgerVisualMappingDefinition
{
    public string SchemaVersion { get; set; } = "visual-mapping/v1";
    public List<LedgerVisualNodeMappingDefinition> NodeMappings { get; set; } = [];
    public List<LedgerVisualCategoryMappingDefinition> CategoryMappings { get; set; } = [];
    public List<LedgerVisualActionMappingDefinition> ActionMappings { get; set; } = [];
    public List<LedgerVisualActionMappingDefinition> Actions { get; set; } = [];
    public List<LedgerVisualSymbolMappingDefinition> SymbolMappings { get; set; } = [];
    public List<LedgerVisualPoseHintDefinition> PoseHints { get; set; } = [];
    public List<LedgerVisualAnchorAliasDefinition> AnchorAliases { get; set; } = [];
    public List<LedgerVisualRelationshipMappingDefinition> RelationshipMappings { get; set; } = [];
    public LedgerVisualRendererBindingDefinition RendererBinding { get; set; } = new();
    public LedgerVisualFallbackDefinition Fallbacks { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = [];

    public IReadOnlyList<LedgerVisualActionMappingDefinition> EffectiveActionMappings
        => ActionMappings.Count > 0 ? ActionMappings : Actions;
}
