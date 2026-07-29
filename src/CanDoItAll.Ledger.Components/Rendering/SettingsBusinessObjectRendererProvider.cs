using CanDoItAll.Ledger.BusinessObjects;

namespace CanDoItAll.Ledger.Components.Rendering;

public sealed class SettingsBusinessObjectRendererProvider : IBusinessObjectRendererProvider
{
    public int Priority => 100;

    public bool CanRender(IBusinessObjectView businessObject)
        => businessObject is AccountSettingsBusinessObjectView;

    public BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject)
        => new(
            "account-settings",
            "Account settings",
            typeof(SettingsBusinessObjectRenderer),
            Priority);
}
