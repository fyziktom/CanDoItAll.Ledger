using Ipfs;
using Ipfs.CoreApi;
using Ipfs.Engine.Client;

namespace CanDoItAll.Economy.BusinessObjects;

public enum BusinessObjectDataStorageKind
{
    Inline = 0,
    Ipfs = 1,
    Url = 2
}

public sealed record BusinessObjectStorageWriteRequest(
    Stream Data,
    string FileName,
    string ContentType,
    bool Pin = true);

public sealed record BusinessObjectStorageWriteResult(
    BusinessObjectDataStorageKind Storage,
    string Hash,
    Uri Uri,
    string FileName,
    string ContentType,
    long Size);

public sealed record BusinessObjectStorageReadResult(
    Stream Data,
    string Hash,
    string ContentType);

public interface IBusinessObjectDataStorageDriver
{
    BusinessObjectDataStorageKind Storage { get; }

    Task<BusinessObjectStorageWriteResult> SaveAsync(
        BusinessObjectStorageWriteRequest request,
        CancellationToken cancellationToken);

    Task<BusinessObjectStorageReadResult> ReadAsync(
        string hashOrUri,
        CancellationToken cancellationToken);
}

public sealed record BusinessObjectIpfsStorageOptions(
    Uri GatewayBaseUri,
    Uri ApiBaseUri);

public sealed class IpfsBusinessObjectDataStorageDriver(
    IpfsEngineClient client,
    BusinessObjectIpfsStorageOptions options) : IBusinessObjectDataStorageDriver
{
    public BusinessObjectDataStorageKind Storage => BusinessObjectDataStorageKind.Ipfs;

    public async Task<BusinessObjectStorageWriteResult> SaveAsync(
        BusinessObjectStorageWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Data);

        IFileSystemNode node = await client.FileSystem.AddAsync(
            request.Data,
            request.FileName,
            new AddFileOptions { Pin = request.Pin },
            cancellationToken).ConfigureAwait(false);

        string hash = node.Id.ToString();
        return new BusinessObjectStorageWriteResult(
            BusinessObjectDataStorageKind.Ipfs,
            hash,
            BuildIpfsUri(hash),
            request.FileName,
            request.ContentType,
            node.Size);
    }

    public async Task<BusinessObjectStorageReadResult> ReadAsync(
        string hashOrUri,
        CancellationToken cancellationToken)
    {
        string hash = BusinessObjectStorageUri.GetHash(hashOrUri);
        Stream stream = await client.FileSystem.ReadFileAsync(hash, cancellationToken).ConfigureAwait(false);
        return new BusinessObjectStorageReadResult(stream, hash, "application/octet-stream");
    }

    private Uri BuildIpfsUri(string hash)
        => new(options.GatewayBaseUri, $"ipfs/{hash}");
}

public static class BusinessObjectStorageUri
{
    public static string GetHash(string hashOrUri)
    {
        if (string.IsNullOrWhiteSpace(hashOrUri))
        {
            throw new ArgumentException("Hash or URI is required.", nameof(hashOrUri));
        }

        if (!Uri.TryCreate(hashOrUri, UriKind.Absolute, out Uri? uri))
        {
            return hashOrUri.Trim();
        }

        string[] segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        int ipfsIndex = Array.FindIndex(segments, segment => string.Equals(segment, "ipfs", StringComparison.OrdinalIgnoreCase));
        if (ipfsIndex >= 0 && ipfsIndex + 1 < segments.Length)
        {
            return segments[ipfsIndex + 1];
        }

        return segments.LastOrDefault() ?? hashOrUri;
    }

    public static Uri ToGatewayUri(Uri gatewayBaseUri, string hash)
        => new(gatewayBaseUri, $"ipfs/{GetHash(hash)}");
}
