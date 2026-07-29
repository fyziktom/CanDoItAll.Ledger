using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Ledger.BusinessObjects;

public sealed record BusinessObjectDataItemUploadRequest(
    Stream Data,
    string FileName,
    string ContentType,
    long Size,
    bool IsMain = false,
    string? Description = null,
    bool Pin = true);

public interface IBusinessObjectDataItemUploadService
{
    Task<BusinessObjectDataItem> SaveToIpfsAsync(
        BusinessObjectDataItemUploadRequest request,
        CancellationToken cancellationToken);
}

public sealed record HttpIpfsBusinessObjectDataItemUploadOptions(
    Uri ApiBaseUri,
    string GatewayBaseUri);

public sealed class HttpIpfsBusinessObjectDataItemUploadService(
    HttpClient httpClient,
    HttpIpfsBusinessObjectDataItemUploadOptions options) : IBusinessObjectDataItemUploadService
{
    public async Task<BusinessObjectDataItem> SaveToIpfsAsync(
        BusinessObjectDataItemUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Data);

        string fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "upload.bin"
            : request.FileName.Trim();
        string contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType.Trim();

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(request.Data);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(streamContent, "file", fileName);

        using HttpResponseMessage response = await httpClient
            .PostAsync(BuildEndpoint("add", request.Pin), content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string responseText = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        HttpIpfsAddResponse addResponse = ParseAddResponse(responseText);

        if (string.IsNullOrWhiteSpace(addResponse.Hash))
        {
            throw new InvalidOperationException("IPFS add response did not include a CID.");
        }

        long size = addResponse.SizeLong > 0 ? addResponse.SizeLong : request.Size;
        return new BusinessObjectDataItem(
            fileName,
            BusinessObjectDataItem.DetectType($"{contentType} {fileName}"),
            BusinessObjectDataItemStorage.Ipfs,
            addResponse.Hash,
            BuildGatewayUri(addResponse.Hash),
            contentType,
            fileName,
            size,
            request.IsMain,
            request.Description);
    }

    private string BuildEndpoint(string operation, bool pin)
    {
        string prefix = ShouldUseDirectOperation(options.ApiBaseUri) ? string.Empty : "api/v0/";
        return $"{prefix}{operation}?pin={pin.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}";
    }

    private Uri BuildGatewayUri(string hash)
    {
        string gateway = string.IsNullOrWhiteSpace(options.GatewayBaseUri)
            ? options.ApiBaseUri.ToString()
            : options.GatewayBaseUri.Trim();

        if (gateway.Contains("{hash}", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(gateway.Replace("{hash}", Uri.EscapeDataString(hash), StringComparison.OrdinalIgnoreCase), UriKind.Absolute);
        }

        if (gateway.EndsWith("=", StringComparison.Ordinal) || gateway.EndsWith("?arg=", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"{gateway}{Uri.EscapeDataString(hash)}", UriKind.Absolute);
        }

        if (!gateway.EndsWith("/", StringComparison.Ordinal))
        {
            gateway += "/";
        }

        return new Uri(new Uri(gateway, UriKind.Absolute), $"ipfs/{Uri.EscapeDataString(hash)}");
    }

    private static bool ShouldUseDirectOperation(Uri baseUri)
    {
        string path = baseUri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        return path.EndsWith("/api/v0", StringComparison.Ordinal)
            || path.EndsWith("/ipfs-api", StringComparison.Ordinal);
    }

    private static HttpIpfsAddResponse ParseAddResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("IPFS add response was empty.");
        }

        foreach (string line in responseText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse())
        {
            HttpIpfsAddResponse? parsed = JsonSerializer.Deserialize<HttpIpfsAddResponse>(line, BusinessObjectPayloadBuilder.SerializerOptions);
            if (!string.IsNullOrWhiteSpace(parsed?.Hash))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException("IPFS add response did not include a CID.");
    }

    private sealed record HttpIpfsAddResponse(
        [property: JsonPropertyName("Hash")] string Hash,
        [property: JsonPropertyName("Size")] string? Size)
    {
        public long SizeLong => long.TryParse(Size, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : 0;
    }
}

public sealed class HttpIpfsBusinessObjectDataStorageDriver(
    HttpClient httpClient,
    HttpIpfsBusinessObjectDataItemUploadOptions options) : IBusinessObjectDataStorageDriver
{
    public BusinessObjectDataStorageKind Storage => BusinessObjectDataStorageKind.Ipfs;

    public async Task<BusinessObjectStorageWriteResult> SaveAsync(
        BusinessObjectStorageWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Data);

        string fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "upload.bin"
            : request.FileName.Trim();
        string contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType.Trim();

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(request.Data);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(streamContent, "file", fileName);

        using HttpResponseMessage response = await httpClient
            .PostAsync(BuildEndpoint("add", request.Pin), content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string responseText = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        HttpIpfsAddResponse addResponse = ParseAddResponse(responseText);
        if (string.IsNullOrWhiteSpace(addResponse.Hash))
        {
            throw new InvalidOperationException("IPFS add response did not include a CID.");
        }

        long size = addResponse.SizeLong > 0 ? addResponse.SizeLong : GetLengthOrZero(request.Data);
        return new BusinessObjectStorageWriteResult(
            BusinessObjectDataStorageKind.Ipfs,
            addResponse.Hash,
            BuildGatewayUri(addResponse.Hash),
            fileName,
            contentType,
            size);
    }

    public async Task<BusinessObjectStorageReadResult> ReadAsync(
        string hashOrUri,
        CancellationToken cancellationToken)
    {
        string hash = BusinessObjectStorageUri.GetHash(hashOrUri);
        Stream stream = await httpClient
            .GetStreamAsync(BuildCatEndpoint(hash), cancellationToken)
            .ConfigureAwait(false);
        return new BusinessObjectStorageReadResult(stream, hash, "application/octet-stream");
    }

    private string BuildEndpoint(string operation, bool pin)
    {
        string prefix = ShouldUseDirectOperation(options.ApiBaseUri) ? string.Empty : "api/v0/";
        return $"{prefix}{operation}?pin={pin.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}";
    }

    private string BuildCatEndpoint(string hash)
    {
        string prefix = ShouldUseDirectOperation(options.ApiBaseUri) ? string.Empty : "api/v0/";
        return $"{prefix}cat?arg={Uri.EscapeDataString(hash)}";
    }

    private Uri BuildGatewayUri(string hash)
    {
        string gateway = string.IsNullOrWhiteSpace(options.GatewayBaseUri)
            ? options.ApiBaseUri.ToString()
            : options.GatewayBaseUri.Trim();

        if (gateway.Contains("{hash}", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(gateway.Replace("{hash}", Uri.EscapeDataString(hash), StringComparison.OrdinalIgnoreCase), UriKind.Absolute);
        }

        if (gateway.EndsWith("=", StringComparison.Ordinal) || gateway.EndsWith("?arg=", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"{gateway}{Uri.EscapeDataString(hash)}", UriKind.Absolute);
        }

        if (!gateway.EndsWith("/", StringComparison.Ordinal))
        {
            gateway += "/";
        }

        return new Uri(new Uri(gateway, UriKind.Absolute), $"ipfs/{Uri.EscapeDataString(hash)}");
    }

    private static long GetLengthOrZero(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Length : 0;
        }
        catch (NotSupportedException)
        {
            return 0;
        }
    }

    private static bool ShouldUseDirectOperation(Uri baseUri)
    {
        string path = baseUri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        return path.EndsWith("/api/v0", StringComparison.Ordinal)
            || path.EndsWith("/ipfs-api", StringComparison.Ordinal);
    }

    private static HttpIpfsAddResponse ParseAddResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("IPFS add response was empty.");
        }

        foreach (string line in responseText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse())
        {
            HttpIpfsAddResponse? parsed = JsonSerializer.Deserialize<HttpIpfsAddResponse>(line, BusinessObjectPayloadBuilder.SerializerOptions);
            if (!string.IsNullOrWhiteSpace(parsed?.Hash))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException("IPFS add response did not include a CID.");
    }

    private sealed record HttpIpfsAddResponse(
        [property: JsonPropertyName("Hash")] string Hash,
        [property: JsonPropertyName("Size")] string? Size)
    {
        public long SizeLong => long.TryParse(Size, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : 0;
    }
}

public sealed class BusinessObjectDataItemUploadService(
    IBusinessObjectDataStorageDriver storageDriver) : IBusinessObjectDataItemUploadService
{
    public async Task<BusinessObjectDataItem> SaveToIpfsAsync(
        BusinessObjectDataItemUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Data);

        string fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "upload.bin"
            : request.FileName.Trim();
        string contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType.Trim();

        BusinessObjectStorageWriteResult result = await storageDriver
            .SaveAsync(
                new BusinessObjectStorageWriteRequest(
                    request.Data,
                    fileName,
                    contentType,
                    request.Pin),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Storage != BusinessObjectDataStorageKind.Ipfs)
        {
            throw new InvalidOperationException("BusinessObject file uploads must be stored in IPFS.");
        }

        string hash = BusinessObjectStorageUri.GetHash(
            string.IsNullOrWhiteSpace(result.Hash)
                ? result.Uri.ToString()
                : result.Hash);

        return new BusinessObjectDataItem(
            fileName,
            BusinessObjectDataItem.DetectType($"{contentType} {fileName}"),
            BusinessObjectDataItemStorage.Ipfs,
            hash,
            result.Uri,
            contentType,
            fileName,
            result.Size > 0 ? result.Size : request.Size,
            request.IsMain,
            request.Description);
    }
}
