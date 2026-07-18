using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Services;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectDataItemUriResolverTests : BunitContext
{
    [Fact]
    public void ContentEndpointResolver_UsesCidAndOriginalFileNameForIpfsItems()
    {
        var resolver = new IpfsContentEndpointDataItemUriResolver(
            new Uri("https://control.example/api/files/content", UriKind.Absolute));
        var item = new BusinessObjectDataItem(
            "Vector preview",
            BusinessObjectDataItemType.Image,
            BusinessObjectDataItemStorage.Ipfs,
            "QmSvgPreview",
            new Uri("http://stale.example/ipfs/QmSvgPreview"),
            "image/svg+xml",
            "diagram proof.svg");

        Uri? resolved = resolver.Resolve(item);

        Assert.Equal(
            "https://control.example/api/files/content?path=%2Fipfs%2FQmSvgPreview&name=diagram%20proof.svg&download=false",
            resolved?.AbsoluteUri);
    }

    [Fact]
    public void ContentEndpointResolver_PreservesOrdinaryUrlItems()
    {
        var resolver = new IpfsContentEndpointDataItemUriResolver(
            new Uri("https://control.example/api/files/content", UriKind.Absolute));
        var source = new Uri("https://cdn.example/diagram.svg", UriKind.Absolute);
        var item = new BusinessObjectDataItem(
            "Vector preview",
            BusinessObjectDataItemType.Image,
            BusinessObjectDataItemStorage.Url,
            "url-hash",
            source,
            "image/svg+xml",
            "diagram.svg");

        Assert.Same(source, resolver.Resolve(item));
    }

    [Fact]
    public void ContentEndpointResolver_UsesContentTypeExtensionWhenLegacyItemHasNoFileExtension()
    {
        var resolver = new IpfsContentEndpointDataItemUriResolver(
            new Uri("https://control.example/api/files/content", UriKind.Absolute));
        var item = new BusinessObjectDataItem(
            "Vector preview",
            BusinessObjectDataItemType.Image,
            BusinessObjectDataItemStorage.Ipfs,
            "ipfs://QmLegacySvg",
            ContentType: "image/svg+xml");

        Uri? resolved = resolver.Resolve(item);

        Assert.Contains("path=%2Fipfs%2FQmLegacySvg", resolved?.AbsoluteUri);
        Assert.Contains("name=Vector%20preview.svg", resolved?.AbsoluteUri);
    }

    [Theory]
    [InlineData(BusinessObjectDataItemType.Text, "text/plain", "notes.txt")]
    [InlineData(BusinessObjectDataItemType.Json, "application/json", "metadata.json")]
    public void InteractivePreview_RendersTextDocumentsInAReadOnlyFrame(
        BusinessObjectDataItemType type,
        string contentType,
        string fileName)
    {
        var source = new Uri($"https://content.example/{fileName}", UriKind.Absolute);
        var item = new BusinessObjectDataItem(
            fileName,
            type,
            BusinessObjectDataItemStorage.Url,
            $"{fileName}-hash",
            source,
            contentType,
            fileName);

        IRenderedComponent<BusinessObjectDataItemInteractivePreview> component =
            Render<BusinessObjectDataItemInteractivePreview>(parameters => parameters
                .Add(x => x.Item, item));

        Assert.Equal(source.ToString(), component.Find("iframe").GetAttribute("src"));
    }

    public BusinessObjectDataItemUriResolverTests()
    {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Resolve_IpfsItemWithPersistedLocalUrl_UsesConfiguredRemoteGateway()
    {
        var resolver = new IpfsGatewayDataItemUriResolver(new Uri("https://gateway.example/", UriKind.Absolute));
        BusinessObjectDataItem item = CreateIpfsImage(
            new Uri("http://localhost:5081/ipfs-api/cat?arg=QmPortable", UriKind.Absolute));

        Uri? resolved = resolver.Resolve(item);

        Assert.Equal("https://gateway.example/ipfs/QmPortable", resolved?.ToString());
    }

    [Fact]
    public void Resolve_UrlItem_PreservesItsStoredAddress()
    {
        var resolver = new IpfsGatewayDataItemUriResolver(new Uri("https://gateway.example/", UriKind.Absolute));
        var storedUri = new Uri("https://media.example/image.png", UriKind.Absolute);
        var item = new BusinessObjectDataItem(
            "Remote image",
            BusinessObjectDataItemType.Image,
            BusinessObjectDataItemStorage.Url,
            storedUri.ToString(),
            storedUri,
            "image/png");

        Assert.Same(storedUri, resolver.Resolve(item));
    }

    [Fact]
    public void Constructor_RelativeGateway_RejectsInvalidConfiguration()
    {
        var relativeGateway = new Uri("gateway/", UriKind.Relative);

        Assert.Throws<ArgumentException>(() => new IpfsGatewayDataItemUriResolver(relativeGateway));
    }

    [Fact]
    public void Preview_CascadedResolver_RewritesRenderedIpfsSource()
    {
        var resolver = new IpfsGatewayDataItemUriResolver(new Uri("https://gateway.example/", UriKind.Absolute));
        BusinessObjectDataItem item = CreateIpfsImage(
            new Uri("http://localhost:5081/ipfs-api/cat?arg=QmPortable", UriKind.Absolute));

        IRenderedComponent<CascadingValue<IBusinessObjectDataItemUriResolver>> component =
            Render<CascadingValue<IBusinessObjectDataItemUriResolver>>(parameters => parameters
                .Add(x => x.Value, resolver)
                .AddChildContent<BusinessObjectDataItemPreview>(child => child
                    .Add(x => x.Item, item)));

        Assert.Equal(
            "https://gateway.example/ipfs/QmPortable",
            component.Find("img").GetAttribute("src"));
    }

    private static BusinessObjectDataItem CreateIpfsImage(Uri persistedUri)
        => new(
            "Portable image",
            BusinessObjectDataItemType.Image,
            BusinessObjectDataItemStorage.Ipfs,
            "QmPortable",
            persistedUri,
            "image/png",
            "portable.png",
            IsMain: true);
}
