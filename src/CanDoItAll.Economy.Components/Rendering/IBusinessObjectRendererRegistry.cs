using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Rendering;

public interface IBusinessObjectRendererRegistry
{
    BusinessObjectRendererDescriptor Resolve(IBusinessObjectView businessObject);
}
