using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Rendering;

public interface IBusinessObjectRendererProvider
{
    int Priority { get; }

    bool CanRender(IBusinessObjectView businessObject);

    BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject);
}
