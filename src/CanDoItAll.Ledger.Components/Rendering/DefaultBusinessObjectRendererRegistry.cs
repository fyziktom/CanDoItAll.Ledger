namespace CanDoItAll.Ledger.Components.Rendering;

internal static class DefaultBusinessObjectRendererRegistry
{
    public static IBusinessObjectRendererRegistry Instance { get; } = new BusinessObjectRendererRegistry(
    [
        new SettingsBusinessObjectRendererProvider(),
        new RuleBusinessObjectRendererProvider(),
        new CommerceBusinessObjectRendererProvider(),
        new TypedFamilyBusinessObjectRendererProvider(),
        new MediaBusinessObjectRendererProvider(),
        new GenericBusinessObjectRendererProvider()
    ]);
}
