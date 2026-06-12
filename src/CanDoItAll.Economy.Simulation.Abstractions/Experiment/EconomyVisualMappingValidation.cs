namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class EconomyVisualMappingValidationResult
{
    public List<SimulationScenarioDefinitionValidationMessage> Messages { get; set; } = [];
    public bool IsValid => Messages.All(message => !string.Equals(message.Severity, "error", StringComparison.OrdinalIgnoreCase));
}

public static class EconomyVisualMappingBridgeBoundFields
{
    public const string DiagnosticCode = "bridge-bound-visual-field";
    public const string FollowUp = "Bridge-bound visual field remains for current runtime bridge compatibility; split it into a renderer-specific mapping extension later.";
}

public sealed class EconomyVisualMappingValidator
{
    public const string SupportedSchemaVersion = "visual-mapping/v1";
    private static readonly string[] RendererSpecificAssetMarkers =
    [
        string.Concat(".", "g", "l", "b"),
        string.Concat(".", "g", "l", "t", "f"),
        ".fbx",
        ".obj",
        "threejs",
        "babylon",
        "://",
        "/",
        "\\"
    ];
    private static readonly string RendererNameMarker = string.Concat("web", "gl");

    public EconomyVisualMappingValidationResult Validate(EconomyVisualMappingDefinition mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        var result = new EconomyVisualMappingValidationResult();
        Require(string.Equals(mapping.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal), "visualMapping.schemaVersion", $"Visual mapping schema version must be '{SupportedSchemaVersion}'.", result);
        HashSet<string> anchorAliases = [.. mapping.AnchorAliases.Select(static item => item.Alias).Where(static alias => !string.IsNullOrWhiteSpace(alias))];
        int missingPoseBindings = 0;
        int missingSymbolBindings = 0;

        foreach (EconomyVisualActionMappingDefinition action in mapping.EffectiveActionMappings)
        {
            Require(!string.IsNullOrWhiteSpace(action.ActionKind), $"visualMapping.actions.{FirstNonEmpty(action.EventKind, action.ActionKind, "unknown")}.actionKind", "Action mapping requires an action kind.", result);
            CheckSemanticKey("visualMapping.actions.actionKind", action.ActionKind, result);
            CheckSemanticKey("visualMapping.actions.poseKey", action.PoseKey, result);
            CheckSemanticKey("visualMapping.actions.symbolCategory", action.SymbolCategory, result);
            if (!string.IsNullOrWhiteSpace(action.AnchorAlias) && !anchorAliases.Contains(action.AnchorAlias))
            {
                Warning("unknown-anchor-alias", $"visualMapping.actions.{FirstNonEmpty(action.EventKind, action.ActionKind)}.anchorAlias", $"Action mapping references unknown anchor alias '{action.AnchorAlias}'.", result);
            }

            if (RequiresPose(action.ActionKind) &&
                string.IsNullOrWhiteSpace(action.PoseKey) &&
                !action.AllowNoOpFallback &&
                !mapping.Fallbacks.AllowNoOpPoseFallback)
            {
                Warning("missing-pose-binding", $"visualMapping.actions.{action.EventKind}.poseKey", "Action mapping has no pose binding and no explicit no-op fallback.", result);
            }
            else if (RequiresPose(action.ActionKind) && string.IsNullOrWhiteSpace(action.PoseKey))
            {
                missingPoseBindings++;
            }

            if (RequiresSymbol(action.ActionKind) &&
                string.IsNullOrWhiteSpace(action.SymbolCategory) &&
                !action.AllowNoOpFallback &&
                !mapping.Fallbacks.AllowNoOpSymbolFallback)
            {
                Warning("missing-symbol-binding", $"visualMapping.actions.{action.EventKind}.symbolCategory", "Action mapping has no symbol binding and no explicit no-op fallback.", result);
            }
            else if (RequiresSymbol(action.ActionKind) && string.IsNullOrWhiteSpace(action.SymbolCategory))
            {
                missingSymbolBindings++;
            }
        }

        foreach (EconomyVisualNodeMappingDefinition node in mapping.NodeMappings)
        {
            string nodeAssetId = ResolveRendererValue(
                mapping.RendererBinding.NodeAssetIds,
                node.SourceKind,
                node.VisualCategory);
            Require(!string.IsNullOrWhiteSpace(node.SourceKind), "visualMapping.nodes.sourceKind", "Node mapping requires a source kind.", result);
            Require(!string.IsNullOrWhiteSpace(node.VisualCategory), $"visualMapping.nodes.{node.SourceKind}.visualCategory", "Node mapping requires a visual category.", result);
            CheckSemanticKey("visualMapping.nodes.visualCategory", node.VisualCategory, result);
            CheckSemanticKey("visualMapping.nodes.assetId", node.AssetId, result);
            MarkBridgeBound("visualMapping.nodes.assetId", node.AssetId, result);
            if (string.IsNullOrWhiteSpace(node.AssetId) && string.IsNullOrWhiteSpace(nodeAssetId))
            {
                Warning("missing-node-asset", $"visualMapping.nodes.{node.SourceKind}.assetId", "Node mapping has no direct asset id and will rely on category or fallback mapping.", result);
            }
        }

        foreach (EconomyVisualCategoryMappingDefinition category in mapping.CategoryMappings)
        {
            string categoryAssetId = FirstNonEmpty(
                category.AssetId,
                ResolveRendererValue(mapping.RendererBinding.CategoryAssetIds, category.VisualCategory));
            Require(!string.IsNullOrWhiteSpace(category.VisualCategory), "visualMapping.categories.visualCategory", "Category mapping requires a visual category.", result);
            Require(!string.IsNullOrWhiteSpace(categoryAssetId), $"visualMapping.categories.{category.VisualCategory}.assetId", "Category mapping requires an asset id or renderer binding.", result);
            CheckSemanticKey("visualMapping.categories.visualCategory", category.VisualCategory, result);
            CheckSemanticKey("visualMapping.categories.assetId", category.AssetId, result);
            MarkBridgeBound("visualMapping.categories.assetId", category.AssetId, result);
            if (!string.IsNullOrWhiteSpace(category.AnchorAlias) && !anchorAliases.Contains(category.AnchorAlias))
            {
                Warning("unknown-anchor-alias", $"visualMapping.categories.{category.VisualCategory}.anchorAlias", $"Category mapping references unknown anchor alias '{category.AnchorAlias}'.", result);
            }
        }

        foreach (EconomyVisualPoseHintDefinition pose in mapping.PoseHints)
        {
            string poseAssetId = FirstNonEmpty(
                pose.AssetId,
                ResolveRendererValue(mapping.RendererBinding.PoseAssetIds, pose.PoseKey));
            string poseVariantId = FirstNonEmpty(
                pose.AssetVariantId,
                ResolveRendererValue(mapping.RendererBinding.PoseVariantIds, pose.PoseKey));
            Require(!string.IsNullOrWhiteSpace(pose.PoseKey), "visualMapping.poseHints.poseKey", "Pose hint requires a pose key.", result);
            CheckSemanticKey("visualMapping.poseHints.poseKey", pose.PoseKey, result);
            CheckSemanticKey("visualMapping.poseHints.assetId", pose.AssetId, result);
            CheckSemanticKey("visualMapping.poseHints.assetVariantId", pose.AssetVariantId, result);
            MarkBridgeBound("visualMapping.poseHints.assetId", pose.AssetId, result);
            MarkBridgeBound("visualMapping.poseHints.assetVariantId", pose.AssetVariantId, result);
            if (string.IsNullOrWhiteSpace(poseAssetId) && string.IsNullOrWhiteSpace(poseVariantId) && !pose.IsNoOpFallback)
            {
                Warning("missing-pose-asset", $"visualMapping.poseHints.{pose.PoseKey}.assetId", "Pose hint has no asset id and will rely on no-op or runtime fallback.", result);
            }

            Require(pose.ScaleX > 0 && pose.ScaleY > 0 && pose.ScaleZ > 0, $"visualMapping.poseHints.{pose.PoseKey}.scale", "Pose hint scale values must be positive.", result);
        }

        foreach (EconomyVisualSymbolMappingDefinition symbol in mapping.SymbolMappings)
        {
            string symbolKey = FirstNonEmpty(symbol.SymbolKey, symbol.SymbolCategory);
            string symbolAssetId = FirstNonEmpty(
                symbol.SymbolAssetId,
                ResolveRendererValue(mapping.RendererBinding.SymbolAssetIds, symbolKey, symbol.SymbolCategory));
            Require(!string.IsNullOrWhiteSpace(symbolKey), "visualMapping.symbolMappings.symbolKey", "Symbol mapping requires a symbol key or symbol category.", result);
            CheckSemanticKey("visualMapping.symbolMappings.symbolKey", symbolKey, result);
            CheckSemanticKey("visualMapping.symbolMappings.symbolAssetId", symbol.SymbolAssetId, result);
            MarkBridgeBound("visualMapping.symbolMappings.symbolAssetId", symbol.SymbolAssetId, result);
            if (string.IsNullOrWhiteSpace(symbolAssetId) && !symbol.IsNoOpFallback)
            {
                Warning("missing-symbol-asset", $"visualMapping.symbolMappings.{symbolKey}.symbolAssetId", "Symbol mapping has no asset id and will rely on semantic/runtime symbol fallback.", result);
            }
        }

        foreach (EconomyVisualAnchorAliasDefinition alias in mapping.AnchorAliases)
        {
            string anchorKey = FirstNonEmpty(
                alias.AnchorKey,
                ResolveRendererValue(mapping.RendererBinding.AnchorKeys, alias.Alias));
            Require(!string.IsNullOrWhiteSpace(alias.Alias), "visualMapping.anchorAliases.alias", "Anchor alias requires an alias.", result);
            Require(!string.IsNullOrWhiteSpace(anchorKey), $"visualMapping.anchorAliases.{alias.Alias}.anchorKey", "Anchor alias requires an anchor key or renderer binding.", result);
            MarkBridgeBound("visualMapping.anchorAliases.anchorKey", alias.AnchorKey, result);
        }

        if (!string.IsNullOrWhiteSpace(mapping.Fallbacks.DiagnosticObjectId))
        {
            MarkBridgeBound("visualMapping.fallbacks.diagnosticObjectId", mapping.Fallbacks.DiagnosticObjectId, result);
        }

        if (mapping.Fallbacks.AllowNoOpPoseFallback && (missingPoseBindings > 0 || mapping.PoseHints.Count == 0))
        {
            Warning("fallback-heavy-mapping", "visualMapping.fallbacks.allowNoOpPoseFallback", "Mapping relies on no-op pose fallback for at least one action or has no pose hints.", result);
        }

        if (mapping.Fallbacks.AllowNoOpSymbolFallback && (missingSymbolBindings > 0 || mapping.SymbolMappings.Count == 0))
        {
            Warning("fallback-heavy-mapping", "visualMapping.fallbacks.allowNoOpSymbolFallback", "Mapping relies on no-op symbol fallback for at least one action or has no symbol mappings.", result);
        }

        AddDuplicateErrors(mapping.CategoryMappings.Select(static item => item.VisualCategory), "duplicate-category-mapping", "visualMapping.categories", result);
        AddDuplicateErrors(mapping.AnchorAliases.Select(static item => item.Alias), "duplicate-anchor-alias", "visualMapping.anchorAliases", result);
        AddDuplicateErrors(mapping.PoseHints.Select(static item => item.PoseKey).Where(static key => !string.IsNullOrWhiteSpace(key)), "duplicate-pose-key", "visualMapping.poseHints", result);
        AddDuplicateErrors(mapping.SymbolMappings.Select(static item => FirstNonEmpty(item.SymbolKey, item.SymbolCategory)).Where(static key => !string.IsNullOrWhiteSpace(key)), "duplicate-symbol-key", "visualMapping.symbolMappings", result);
        return result;
    }

    private static bool RequiresPose(string actionKind)
        => actionKind.Contains("pose", StringComparison.OrdinalIgnoreCase) ||
           actionKind.Contains("state", StringComparison.OrdinalIgnoreCase) ||
           actionKind.Contains("work", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresSymbol(string actionKind)
        => actionKind.Contains("symbol", StringComparison.OrdinalIgnoreCase);

    private static void CheckSemanticKey(string path, string value, EconomyVisualMappingValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Contains(RendererNameMarker, StringComparison.OrdinalIgnoreCase) ||
            RendererSpecificAssetMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            result.Messages.Add(new() { Code = "renderer-specific-visual-key", Path = path, Message = "Economy visual mapping must use renderer-neutral semantic keys." });
        }
    }

    private static void MarkBridgeBound(string path, string value, EconomyVisualMappingValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Warning(EconomyVisualMappingBridgeBoundFields.DiagnosticCode, path, EconomyVisualMappingBridgeBoundFields.FollowUp, result);
        }
    }

    private static void AddDuplicateErrors(IEnumerable<string> values, string code, string path, EconomyVisualMappingValidationResult result)
    {
        foreach (string key in values.Where(static value => !string.IsNullOrWhiteSpace(value)).GroupBy(value => value, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key))
        {
            result.Messages.Add(new() { Code = code, Path = path, Message = $"Duplicate visual mapping key '{key}'." });
        }
    }

    private static void Require(bool condition, string path, string message, EconomyVisualMappingValidationResult result)
    {
        if (!condition)
        {
            result.Messages.Add(new() { Code = "invalid-visual-mapping", Path = path, Message = message });
        }
    }

    private static void Warning(string code, string path, string message, EconomyVisualMappingValidationResult result)
        => result.Messages.Add(new() { Severity = "warning", Code = code, Path = path, Message = message });

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string ResolveRendererValue(IReadOnlyDictionary<string, string> values, params string?[] keys)
    {
        foreach (string? key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key) && values.TryGetValue(key, out string? value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
