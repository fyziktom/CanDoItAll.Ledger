using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Services;

public interface IBusinessObjectDataItemUriResolver
{
    Uri? Resolve(BusinessObjectDataItem item);
}

public sealed class IpfsGatewayDataItemUriResolver : IBusinessObjectDataItemUriResolver
{
    public IpfsGatewayDataItemUriResolver(Uri gatewayBaseUri)
    {
        ArgumentNullException.ThrowIfNull(gatewayBaseUri);
        if (!gatewayBaseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The IPFS gateway base URI must be absolute.", nameof(gatewayBaseUri));
        }

        GatewayBaseUri = gatewayBaseUri;
    }

    public Uri GatewayBaseUri { get; }

    public Uri? Resolve(BusinessObjectDataItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.ResolveUri(GatewayBaseUri);
    }
}
