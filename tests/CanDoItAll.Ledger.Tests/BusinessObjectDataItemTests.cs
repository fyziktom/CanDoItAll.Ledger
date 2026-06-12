using System.Text.Json;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectDataItemTests
{
    [Fact]
    public async Task UploadService_CreatesIpfsDataItemWithoutInlinePayloadData()
    {
        var driver = new FakeStorageDriver(
            new BusinessObjectStorageWriteResult(
                BusinessObjectDataStorageKind.Ipfs,
                "QmUploadedImage",
                new Uri("http://127.0.0.1:5001/ipfs/QmUploadedImage"),
                "diagram.png",
                "image/png",
                128));
        var service = new BusinessObjectDataItemUploadService(driver);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        BusinessObjectDataItem item = await service.SaveToIpfsAsync(
            new BusinessObjectDataItemUploadRequest(
                stream,
                "diagram.png",
                "image/png",
                4,
                IsMain: true),
            CancellationToken.None);
        var payload = new BusinessObjectMediaPayload(
            "IPFS-backed BO",
            "CID payload",
            "No uploaded bytes in transaction state",
            "#ipfs",
            DataItems: [item]);
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(BusinessObjectDataItemStorage.Ipfs, item.Storage);
        Assert.Equal(BusinessObjectDataItemType.Image, item.Type);
        Assert.Equal("QmUploadedImage", item.Hash);
        Assert.Equal("image/png", item.ContentType);
        Assert.Contains("dataItems", json);
        Assert.Contains("QmUploadedImage", json);
        Assert.DoesNotContain("data:", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadService_RejectsNonIpfsStorage()
    {
        var driver = new FakeStorageDriver(
            new BusinessObjectStorageWriteResult(
                BusinessObjectDataStorageKind.Inline,
                "inline-hash",
                new Uri("memory://inline-hash"),
                "diagram.png",
                "image/png",
                128));
        var service = new BusinessObjectDataItemUploadService(driver);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveToIpfsAsync(
                new BusinessObjectDataItemUploadRequest(stream, "diagram.png", "image/png", 4),
                CancellationToken.None));
    }

    [Fact]
    public async Task CastRegistry_CastsImageAndPostBusinessObjectsWithDataItems()
    {
        var viewedAt = DateTimeOffset.Parse("2026-05-06T19:00:00Z");
        var registry = new BusinessObjectCastRegistry(
        [
            new GenericBusinessObjectCaster(new ManualTimeProvider(viewedAt)),
            new ImageBusinessObjectCaster(new ManualTimeProvider(viewedAt)),
            new PostBusinessObjectCaster(new ManualTimeProvider(viewedAt))
        ]);

        IBusinessObjectView image = await registry.CastAsync(
            CreateSource("image", CreatePayload("Power transformer DYI", "Image #Electronics #DYI")),
            CancellationToken.None);
        IBusinessObjectView post = await registry.CastAsync(
            CreateSource("post", CreatePayload("Open digitalization", "Post #OpenSource #Blazor")),
            CancellationToken.None);

        ImageBusinessObjectView imageView = Assert.IsType<ImageBusinessObjectView>(image);
        PostBusinessObjectView postView = Assert.IsType<PostBusinessObjectView>(post);
        Assert.Equal("Power transformer DYI", imageView.Name);
        Assert.Contains("Electronics", imageView.Tags);
        Assert.Equal(BusinessObjectDataItemType.Image, Assert.Single(imageView.DataItems).Type);
        Assert.Equal("Open digitalization", postView.Name);
        Assert.Contains("OpenSource", postView.Tags);
    }

    [Theory]
    [InlineData("photo.png", BusinessObjectDataItemType.Image)]
    [InlineData("video.mp4", BusinessObjectDataItemType.AVMedia)]
    [InlineData("document.pdf", BusinessObjectDataItemType.Pdf)]
    [InlineData("payload.json", BusinessObjectDataItemType.Json)]
    [InlineData("page.html", BusinessObjectDataItemType.Html)]
    [InlineData("diagram.mmd", BusinessObjectDataItemType.Mermaid)]
    [InlineData("notes.md", BusinessObjectDataItemType.Text)]
    public void DataItem_DetectsTypeFromFileName(string fileName, BusinessObjectDataItemType expected)
    {
        Assert.Equal(expected, BusinessObjectDataItem.DetectType(fileName));
    }

    private static BusinessObjectCastSource CreateSource(string objectType, string stateJson)
    {
        var context = new LedgerAccountContext(new LedgerId("test-ledger"));
        var now = DateTimeOffset.Parse("2026-05-06T18:00:00Z");
        return new BusinessObjectCastSource(
            context,
            new BusinessObjectId($"bo:{objectType}:1"),
            objectType,
            "1.0",
            1,
            Address.FromPublicKey(Hex.Utf8("test-public")),
            BusinessObjectOperation.Create,
            new HashHex("hash"),
            new OutPoint(new TxId("tx-test"), 0),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject,
            now);
    }

    private static string CreatePayload(string name, string tags)
    {
        var payload = new BusinessObjectMediaPayload(
            name,
            "Short description",
            "Longer body text",
            tags,
            "VENFT demo",
            ImageLink: "http://127.0.0.1:8080/ipfs/QmDemoImage",
            DataItems:
            [
                new BusinessObjectDataItem(
                    "main image",
                    BusinessObjectDataItemType.Image,
                    BusinessObjectDataItemStorage.Ipfs,
                    "QmDemoImage",
                    new Uri("http://127.0.0.1:8080/ipfs/QmDemoImage"),
                    "image/png",
                    "image.png",
                    120,
                    true)
            ]);

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private sealed class FakeStorageDriver(BusinessObjectStorageWriteResult result) : IBusinessObjectDataStorageDriver
    {
        public BusinessObjectDataStorageKind Storage => result.Storage;

        public Task<BusinessObjectStorageWriteResult> SaveAsync(
            BusinessObjectStorageWriteRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(result);

        public Task<BusinessObjectStorageReadResult> ReadAsync(
            string hashOrUri,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
