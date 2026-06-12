namespace CanDoItAll.Economy.Components.Rendering;

public sealed record BusinessObjectRendererDescriptor(
    string Key,
    string DisplayName,
    Type ComponentType,
    int Priority);
