namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class EconomyVisualMappingDefinition
{
    public string SchemaVersion { get; set; } = "visual-mapping/v1";
    public List<EconomyVisualNodeMappingDefinition> NodeMappings { get; set; } = [];
    public List<EconomyVisualCategoryMappingDefinition> CategoryMappings { get; set; } = [];
    public List<EconomyVisualActionMappingDefinition> ActionMappings { get; set; } = [];
    public List<EconomyVisualActionMappingDefinition> Actions { get; set; } = [];
    public List<EconomyVisualSymbolMappingDefinition> SymbolMappings { get; set; } = [];
    public List<EconomyVisualPoseHintDefinition> PoseHints { get; set; } = [];
    public List<EconomyVisualAnchorAliasDefinition> AnchorAliases { get; set; } = [];
    public List<EconomyVisualRelationshipMappingDefinition> RelationshipMappings { get; set; } = [];
    public EconomyVisualRendererBindingDefinition RendererBinding { get; set; } = new();
    public EconomyVisualFallbackDefinition Fallbacks { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = [];

    public IReadOnlyList<EconomyVisualActionMappingDefinition> EffectiveActionMappings
        => ActionMappings.Count > 0 ? ActionMappings : Actions;
}
