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
