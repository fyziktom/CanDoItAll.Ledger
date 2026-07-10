using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Services;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectDataItemUriResolverTests : TestContext
{
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
            RenderComponent<CascadingValue<IBusinessObjectDataItemUriResolver>>(parameters => parameters
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
