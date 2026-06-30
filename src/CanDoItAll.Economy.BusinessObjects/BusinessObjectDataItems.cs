using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public enum BusinessObjectDataItemType
{
    Other = 0,
    Image = 1,
    AVMedia = 2,
    Pdf = 3,
    Json = 4,
    Html = 5,
    Mermaid = 6,
    BlazorApp = 7,
    Text = 8
}

public enum BusinessObjectDataItemStorage
{
    Inline = 0,
    Ipfs = 1,
    Url = 2
}

public sealed record BusinessObjectDataItem(
    string Name,
    BusinessObjectDataItemType Type,
    BusinessObjectDataItemStorage Storage,
    string Hash,
    Uri? Uri = null,
    string? ContentType = null,
    string? FileName = null,
    long? Size = null,
    bool IsMain = false,
    string? Description = null)
{
    public Uri? ResolveUri(Uri ipfsGatewayBaseUri)
        => Storage switch
        {
            BusinessObjectDataItemStorage.Ipfs when !string.IsNullOrWhiteSpace(Hash)
                => BusinessObjectStorageUri.ToGatewayUri(ipfsGatewayBaseUri, Hash),
            BusinessObjectDataItemStorage.Url when Uri is not null => Uri,
            _ => Uri
        };

    public static BusinessObjectDataItemType DetectType(string fileNameOrContentType)
    {
        string value = fileNameOrContentType.ToLowerInvariant();
        if (value.Contains("image/") || value.EndsWith(".png") || value.EndsWith(".jpg") || value.EndsWith(".jpeg") || value.EndsWith(".gif") || value.EndsWith(".bmp"))
        {
            return BusinessObjectDataItemType.Image;
        }

        if (value.Contains("video/") || value.Contains("audio/") || value.EndsWith(".mp3") || value.EndsWith(".mp4") || value.EndsWith(".wav") || value.EndsWith(".mov"))
        {
            return BusinessObjectDataItemType.AVMedia;
        }

        if (value.Contains("pdf") || value.EndsWith(".pdf"))
        {
            return BusinessObjectDataItemType.Pdf;
        }

        if (value.Contains("json") || value.EndsWith(".json"))
        {
            return BusinessObjectDataItemType.Json;
        }

        if (value.Contains("html") || value.EndsWith(".html") || value.EndsWith(".htm"))
        {
            return BusinessObjectDataItemType.Html;
        }

        if (value.EndsWith(".mmd") || value.EndsWith(".mermaid"))
        {
            return BusinessObjectDataItemType.Mermaid;
        }

        if (value.Contains("text/") || value.EndsWith(".txt") || value.EndsWith(".md"))
        {
            return BusinessObjectDataItemType.Text;
        }

        return BusinessObjectDataItemType.Other;
    }
}

public sealed record BusinessObjectMediaPayload(
    string Name,
    string Description,
    string Text,
    string Tags,
    string? Author = null,
    string? Link = null,
    string? ImageLink = null,
    IReadOnlyList<BusinessObjectDataItem>? DataItems = null,
    string? Family = null,
    IReadOnlyDictionary<string, string>? Fields = null)
{
    public IReadOnlyList<string> TagList
        => (Tags ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => tag.TrimStart('#'))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public interface IMediaBusinessObjectView : IBusinessObjectView
{
    string Name { get; }

    string Description { get; }

    string Text { get; }

    string? Author { get; }

    string? Link { get; }

    string? ImageLink { get; }

    IReadOnlyList<string> Tags { get; }

    IReadOnlyList<BusinessObjectDataItem> DataItems { get; }
}

public sealed record ImageBusinessObjectView(
    LedgerAccountContext Context,
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    string? StateJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset ViewedAt,
    string Name,
    string Description,
    string Text,
    string? Author,
    string? Link,
    string? ImageLink,
    IReadOnlyList<string> Tags,
    IReadOnlyList<BusinessObjectDataItem> DataItems) : IMediaBusinessObjectView;

public sealed record PostBusinessObjectView(
    LedgerAccountContext Context,
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    string? StateJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset ViewedAt,
    string Name,
    string Description,
    string Text,
    string? Author,
    string? Link,
    string? ImageLink,
    IReadOnlyList<string> Tags,
    IReadOnlyList<BusinessObjectDataItem> DataItems) : IMediaBusinessObjectView;

public abstract class MediaBusinessObjectCaster(ITimeProvider timeProvider) : IBusinessObjectCaster
{
    public abstract int Priority { get; }

    public abstract bool CanCast(BusinessObjectCastSource source);

    protected abstract IMediaBusinessObjectView CreateView(
        BusinessObjectCastSource source,
        BusinessObjectMediaPayload payload,
        DateTimeOffset viewedAt);

    public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
    {
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(source.StateJson, source.ObjectId.Value);
        return ValueTask.FromResult<IBusinessObjectView>(CreateView(source, payload, timeProvider.UtcNow));
    }
}

public sealed class ImageBusinessObjectCaster(ITimeProvider timeProvider) : MediaBusinessObjectCaster(timeProvider)
{
    public override int Priority => 130;

    public override bool CanCast(BusinessObjectCastSource source)
        => string.Equals(source.ObjectType, "image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source.ObjectType, "bo-image", StringComparison.OrdinalIgnoreCase);

    protected override IMediaBusinessObjectView CreateView(
        BusinessObjectCastSource source,
        BusinessObjectMediaPayload payload,
        DateTimeOffset viewedAt)
        => new ImageBusinessObjectView(
            source.Context,
            source.ObjectId,
            source.ObjectType,
            source.SchemaVersion,
            source.Version,
            source.OwnerAddress,
            source.Operation,
            source.DataHash,
            source.CurrentOutPoint,
            source.StateJson,
            source.CreatedAt,
            viewedAt,
            payload.Name,
            payload.Description,
            payload.Text,
            payload.Author,
            payload.Link,
            payload.ImageLink,
            payload.TagList,
            payload.DataItems ?? Array.Empty<BusinessObjectDataItem>());
}

public sealed class PostBusinessObjectCaster(ITimeProvider timeProvider) : MediaBusinessObjectCaster(timeProvider)
{
    public override int Priority => 120;

    public override bool CanCast(BusinessObjectCastSource source)
        => string.Equals(source.ObjectType, "post", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source.ObjectType, "bo-post", StringComparison.OrdinalIgnoreCase);

    protected override IMediaBusinessObjectView CreateView(
        BusinessObjectCastSource source,
        BusinessObjectMediaPayload payload,
        DateTimeOffset viewedAt)
        => new PostBusinessObjectView(
            source.Context,
            source.ObjectId,
            source.ObjectType,
            source.SchemaVersion,
            source.Version,
            source.OwnerAddress,
            source.Operation,
            source.DataHash,
            source.CurrentOutPoint,
            source.StateJson,
            source.CreatedAt,
            viewedAt,
            payload.Name,
            payload.Description,
            payload.Text,
            payload.Author,
            payload.Link,
            payload.ImageLink,
            payload.TagList,
            payload.DataItems ?? Array.Empty<BusinessObjectDataItem>());
}
