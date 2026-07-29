using CanDoItAll.Ledger.BusinessObjects;

namespace CanDoItAll.Ledger.Components.Rendering;

public sealed class BusinessObjectRendererRegistry(IEnumerable<IBusinessObjectRendererProvider> providers)
    : IBusinessObjectRendererRegistry
{
    private readonly IReadOnlyList<IBusinessObjectRendererProvider> providers = providers
        .OrderByDescending(provider => provider.Priority)
        .ToArray();

    public BusinessObjectRendererDescriptor Resolve(IBusinessObjectView businessObject)
    {
        IBusinessObjectRendererProvider? provider = providers.FirstOrDefault(provider => provider.CanRender(businessObject));
        if (provider is null)
        {
            throw new InvalidOperationException($"No BusinessObject renderer is registered for type '{businessObject.ObjectType}'.");
        }

        return provider.CreateDescriptor(businessObject);
    }
}
