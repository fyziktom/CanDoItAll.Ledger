namespace CanDoItAll.Economy.Components.Rendering;

internal static class DefaultBusinessObjectRendererRegistry
{
    public static IBusinessObjectRendererRegistry Instance { get; } = new BusinessObjectRendererRegistry(
    [
        new SettingsBusinessObjectRendererProvider(),
        new RuleBusinessObjectRendererProvider(),
        new MediaBusinessObjectRendererProvider(),
        new GenericBusinessObjectRendererProvider()
    ]);
}
