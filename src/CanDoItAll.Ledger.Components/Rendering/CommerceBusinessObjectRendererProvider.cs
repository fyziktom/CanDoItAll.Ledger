using CanDoItAll.Ledger.BusinessObjects;

namespace CanDoItAll.Ledger.Components.Rendering;

public sealed class CommerceBusinessObjectRendererProvider : IBusinessObjectRendererProvider
{
    public int Priority => 130;

    public bool CanRender(IBusinessObjectView businessObject)
        => businessObject is ICommerceBusinessObjectView;

    public BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject)
        => new(
            "commerce-record",
            CommerceLabel(businessObject),
            typeof(CommerceBusinessObjectRenderer),
            Priority);

    private static string CommerceLabel(IBusinessObjectView businessObject)
        => businessObject is ICommerceBusinessObjectView commerce
            ? commerce.Commerce switch
            {
                { Payment: not null } => "Payment record",
                { Purchase: not null } => "Purchase record",
                { Receipt: not null } => "Receipt record",
                _ => "Commerce record"
            }
            : "Commerce record";
}

