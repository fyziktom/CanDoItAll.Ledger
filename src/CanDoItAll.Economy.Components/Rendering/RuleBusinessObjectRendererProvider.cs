using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Rendering;

public sealed class RuleBusinessObjectRendererProvider : IBusinessObjectRendererProvider
{
    public int Priority => 150;

    public bool CanRender(IBusinessObjectView businessObject)
        => RuleBusinessObjectDisplay.CanDisplay(businessObject);

    public BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject)
        => new(
            "account-rule",
            "Account rule",
            typeof(RuleBusinessObjectRenderer),
            Priority);
}
