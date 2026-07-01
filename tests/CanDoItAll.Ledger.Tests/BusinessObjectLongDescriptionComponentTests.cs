using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using AngleSharp.Dom;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectLongDescriptionComponentTests : TestContext
{
    private const string ShortDescription = "Short summary shown in cards.";
    private const string LongDescription = "RN-01-SB01 long body: this text must be visible separately from the short summary.";

    public BusinessObjectLongDescriptionComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void DetailsDialog_RendersLongDescriptionWhenShortDescriptionExists()
    {
        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, CreatePost()));

        IElement body = component.Find("[data-testid='cda-bo-long-description']");

        Assert.Contains(LongDescription, body.TextContent, StringComparison.Ordinal);
        Assert.Contains(ShortDescription, component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PayloadBuilderAndGenericCaster_RoundTripLongDescriptionWithShortDescription()
    {
        ManualTimeProvider timeProvider = new(DateTimeOffset.Parse("2026-06-30T10:00:00Z"));
        var caster = new GenericTypedBusinessObjectCaster(timeProvider);
        IBusinessObjectView view = await caster.CastAsync(CreateSource(CreateStateJson()), CancellationToken.None);

        ITypedBusinessObjectView typed = Assert.IsAssignableFrom<ITypedBusinessObjectView>(view);

        Assert.Equal(ShortDescription, typed.Description);
        Assert.Equal(LongDescription, typed.Text);
        Assert.Contains("\"text\"", typed.StateJson, StringComparison.Ordinal);
        Assert.Contains(LongDescription, typed.StateJson, StringComparison.Ordinal);
    }

    private static ITypedBusinessObjectView CreatePost()
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        string stateJson = CreateStateJson();
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, "fallback-post");
        var context = new LedgerAccountContext(new LedgerId("test-ledger"));
        DateTimeOffset now = DateTimeOffset.Parse("2026-06-30T10:00:00Z");

        return new GenericTypedBusinessObjectView(
            context,
            new BusinessObjectId("bo:post:rn-01-sb01"),
            "post",
            "1.0",
            1,
            Address.FromPublicKey(Hex.Utf8("alice-public")),
            BusinessObjectOperation.Create,
            new HashHex(new string('1', 64)),
            new OutPoint(new TxId("tx-rn-01-sb01"), 0),
            stateJson,
            "preview://post/rn-01-sb01",
            TransactionDataItemType.BusinessObject,
            now,
            now,
            payload.Name,
            payload.Description,
            payload.Text,
            payload.Author,
            payload.Link,
            payload.ImageLink,
            payload.TagList,
            payload.DataItems ?? Array.Empty<BusinessObjectDataItem>(),
            definition,
            payload.Fields ?? new Dictionary<string, string>(StringComparer.Ordinal),
            BusinessObjectPayloadBuilder.CreateFacts(definition, payload.Fields));
    }

    private static BusinessObjectCastSource CreateSource(string stateJson)
        => new(
            new LedgerAccountContext(new LedgerId("test-ledger")),
            new BusinessObjectId("bo:post:rn-01-sb01"),
            "post",
            "1.0",
            1,
            Address.FromPublicKey(Hex.Utf8("alice-public")),
            BusinessObjectOperation.Create,
            new HashHex(new string('1', 64)),
            new OutPoint(new TxId("tx-rn-01-sb01"), 0),
            stateJson,
            "preview://post/rn-01-sb01",
            TransactionDataItemType.BusinessObject,
            DateTimeOffset.Parse("2026-06-30T10:00:00Z"));

    private static string CreateStateJson()
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            "RN-01 SB01 Post",
            ShortDescription,
            LongDescription,
            "#long-body",
            [],
            new Dictionary<string, string>
            {
                ["channel"] = "proof",
                ["visibility"] = "public"
            });

        return BusinessObjectPayloadBuilder.Serialize(payload);
    }
}
