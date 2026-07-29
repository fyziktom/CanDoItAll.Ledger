using CanDoItAll.Ledger.BusinessObjects;

namespace CanDoItAll.Ledger.Components.Rendering;

public sealed class GenericBusinessObjectRendererProvider : IBusinessObjectRendererProvider
{
    public int Priority => int.MinValue;

    public bool CanRender(IBusinessObjectView businessObject) => true;

    public BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject)
        => new(
            "generic",
            "Generic business object",
            typeof(GenericBusinessObjectRenderer),
            Priority);
}
