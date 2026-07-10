using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Services;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Economy.Components;

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
}
