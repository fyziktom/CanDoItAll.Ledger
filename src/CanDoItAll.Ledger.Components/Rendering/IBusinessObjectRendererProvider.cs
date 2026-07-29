using CanDoItAll.Ledger.BusinessObjects;

namespace CanDoItAll.Ledger.Components.Rendering;

public interface IBusinessObjectRendererProvider
{
    int Priority { get; }

    bool CanRender(IBusinessObjectView businessObject);

    BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject);
}
