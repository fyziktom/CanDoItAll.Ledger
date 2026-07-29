using CanDoItAll.Ledger.BusinessObjects;

namespace CanDoItAll.Ledger.Components.Rendering;

public interface IBusinessObjectRendererRegistry
{
    BusinessObjectRendererDescriptor Resolve(IBusinessObjectView businessObject);
}
