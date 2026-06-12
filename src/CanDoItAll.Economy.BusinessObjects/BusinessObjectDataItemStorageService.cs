namespace CanDoItAll.Economy.BusinessObjects;

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
