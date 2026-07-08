using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Rendering;

public sealed class TypedFamilyBusinessObjectRendererProvider : IBusinessObjectRendererProvider
{
    public int Priority => 110;

    public bool CanRender(IBusinessObjectView businessObject)
        => businessObject is ITypedBusinessObjectView and not ICommerceBusinessObjectView;

    public BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject)
    {
        string displayName = businessObject is ITypedBusinessObjectView typed
            ? $"{typed.Definition.DisplayName} details"
            : "Typed business object";
        return new(
            "typed-family",
            displayName,
            typeof(TypedFamilyBusinessObjectRenderer),
            Priority);
    }
}

