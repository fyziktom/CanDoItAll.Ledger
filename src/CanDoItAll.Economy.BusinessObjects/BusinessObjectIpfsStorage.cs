#if BUSINESS_OBJECT_IPFS
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record BusinessObjectIpfsStorageOptions(
    Uri GatewayBaseUri,
    Uri ApiBaseUri);

public sealed record IpfsBusinessObjectStorageClient(HttpClient HttpClient);

public sealed class IpfsBusinessObjectDataStorageDriver(
    IpfsBusinessObjectStorageClient client,
    BusinessObjectIpfsStorageOptions options) : IBusinessObjectDataStorageDriver
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

        using HttpResponseMessage response = await client.HttpClient
            .PostAsync($"api/v0/add?pin={request.Pin.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}", content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        IpfsAddResponse addResponse = await response.Content
            .ReadFromJsonAsync<IpfsAddResponse>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("IPFS add response was empty.");

        if (string.IsNullOrWhiteSpace(addResponse.Hash))
        {
            throw new InvalidOperationException("IPFS add response did not include a CID.");
        }

        long size = addResponse.SizeLong > 0
            ? addResponse.SizeLong
            : GetLengthOrZero(request.Data);
        return new BusinessObjectStorageWriteResult(
            BusinessObjectDataStorageKind.Ipfs,
            addResponse.Hash,
            BuildIpfsUri(addResponse.Hash),
            fileName,
            contentType,
            size);
    }

    public async Task<BusinessObjectStorageReadResult> ReadAsync(
        string hashOrUri,
        CancellationToken cancellationToken)
    {
        string hash = BusinessObjectStorageUri.GetHash(hashOrUri);
        Stream stream = await client.HttpClient
            .GetStreamAsync($"api/v0/cat?arg={Uri.EscapeDataString(hash)}", cancellationToken)
            .ConfigureAwait(false);
        return new BusinessObjectStorageReadResult(stream, hash, "application/octet-stream");
    }

    private Uri BuildIpfsUri(string hash)
        => new(options.GatewayBaseUri, $"ipfs/{hash}");

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

    private sealed record IpfsAddResponse(
        [property: JsonPropertyName("Hash")] string Hash,
        [property: JsonPropertyName("Size")] string? Size)
    {
        public long SizeLong => long.TryParse(Size, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : 0;
    }
}
#endif
