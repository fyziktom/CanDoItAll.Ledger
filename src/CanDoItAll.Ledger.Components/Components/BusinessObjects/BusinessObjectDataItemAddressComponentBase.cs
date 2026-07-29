using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Services;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Ledger.Components;

public abstract class BusinessObjectDataItemAddressComponentBase : ComponentBase
{
    [CascadingParameter]
    public IBusinessObjectDataItemUriResolver? DataItemUriResolver { get; set; }

    protected Uri? ResolveDataItemUri(BusinessObjectDataItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return DataItemUriResolver is null
            ? item.Uri
            : DataItemUriResolver.Resolve(item);
    }

    protected string? ResolveDataItemUriString(BusinessObjectDataItem item)
        => ResolveDataItemUri(item)?.ToString();

    protected string? ResolveActionableDataItemUriString(BusinessObjectDataItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Storage is not (BusinessObjectDataItemStorage.Ipfs or BusinessObjectDataItemStorage.Url))
        {
            return null;
        }

        string? resolvedUri = ResolveDataItemUriString(item);
        return string.IsNullOrWhiteSpace(resolvedUri) ? null : resolvedUri;
    }
}
