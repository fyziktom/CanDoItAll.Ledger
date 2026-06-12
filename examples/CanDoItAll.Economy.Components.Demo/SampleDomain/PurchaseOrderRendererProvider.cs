using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Rendering;

namespace CanDoItAll.Economy.Components.Demo.SampleDomain;

public sealed class PurchaseOrderRendererProvider : IBusinessObjectRendererProvider
{
    public int Priority => 250;

    public bool CanRender(IBusinessObjectView businessObject)
        => businessObject is PurchaseOrderBusinessObjectView
            || string.Equals(businessObject.ObjectType, "purchase-order", StringComparison.OrdinalIgnoreCase);

    public BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject)
        => new(
            "purchase-order",
            "Purchase order",
            typeof(PurchaseOrderBusinessObjectRenderer),
            Priority);
}
