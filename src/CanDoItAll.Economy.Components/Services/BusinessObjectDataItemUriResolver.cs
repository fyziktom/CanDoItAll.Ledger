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

public sealed class IpfsContentEndpointDataItemUriResolver : IBusinessObjectDataItemUriResolver
{
    public IpfsContentEndpointDataItemUriResolver(Uri contentEndpointUri)
    {
        ArgumentNullException.ThrowIfNull(contentEndpointUri);
        if (!contentEndpointUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The IPFS content endpoint URI must be absolute.", nameof(contentEndpointUri));
        }

        ContentEndpointUri = contentEndpointUri;
    }

    public Uri ContentEndpointUri { get; }

    public Uri? Resolve(BusinessObjectDataItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Storage != BusinessObjectDataItemStorage.Ipfs || string.IsNullOrWhiteSpace(item.Hash))
        {
            return item.Uri;
        }

        string hash = BusinessObjectStorageUri.GetHash(item.Hash);
        string fileName = ResolveFileName(item);
        var uriBuilder = new UriBuilder(ContentEndpointUri)
        {
            Query = string.Join(
                "&",
                $"path={Uri.EscapeDataString($"/ipfs/{hash}")}",
                $"name={Uri.EscapeDataString(fileName)}",
                "download=false")
        };
        return uriBuilder.Uri;
    }

    private static string ResolveFileName(BusinessObjectDataItem item)
    {
        string fileName = string.IsNullOrWhiteSpace(item.FileName) ? item.Name : item.FileName;
        if (Path.HasExtension(fileName))
        {
            return fileName;
        }

        string contentType = item.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant() ?? string.Empty;
        string extension = contentType switch
        {
            "image/svg+xml" => ".svg",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            "application/json" => ".json",
            "text/plain" => ".txt",
            "text/html" => ".html",
            "text/markdown" => ".md",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "video/mp4" => ".mp4",
            _ => item.Type switch
            {
                BusinessObjectDataItemType.Pdf => ".pdf",
                BusinessObjectDataItemType.Json => ".json",
                BusinessObjectDataItemType.Text => ".txt",
                BusinessObjectDataItemType.Html => ".html",
                BusinessObjectDataItemType.Mermaid => ".mmd",
                _ => string.Empty
            }
        };

        return $"{fileName}{extension}";
    }
}
