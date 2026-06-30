using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectCastingTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("default"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-05-04T00:00:00Z");

    [Fact]
    public async Task Registry_UsesSettingsCasterAndManualTimeProvider()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-05T00:00:00Z"));
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<ITimeProvider>(timeProvider)
            .AddBusinessObjectCasting()
            .BuildServiceProvider();
        IBusinessObjectCastRegistry registry = provider.GetRequiredService<IBusinessObjectCastRegistry>();

        IBusinessObjectView view = await registry.CastAsync(CreateSource("settings", TransactionDataItemType.AccountSettings, "{\"theme\":\"dense\"}"), CancellationToken.None);

        var settings = Assert.IsType<AccountSettingsBusinessObjectView>(view);
        Assert.Equal("dense", settings.Settings["theme"]);
        Assert.Equal(timeProvider.UtcNow, settings.ViewedAt);
    }

    [Fact]
    public async Task Registry_AllowsCustomCasterWithoutChangingGenericCode()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-06T00:00:00Z"));
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<ITimeProvider>(timeProvider)
            .AddBusinessObjectCasting()
            .AddBusinessObjectCaster<PurchaseRequestCaster>()
            .BuildServiceProvider();
        IBusinessObjectCastRegistry registry = provider.GetRequiredService<IBusinessObjectCastRegistry>();

        IBusinessObjectView view = await registry.CastAsync(CreateSource("purchase-request", TransactionDataItemType.BusinessObject, "{\"amount\":\"120\"}"), CancellationToken.None);

        var purchaseRequest = Assert.IsType<PurchaseRequestView>(view);
        Assert.Equal("120", purchaseRequest.Amount);
        Assert.Equal(timeProvider.UtcNow, purchaseRequest.ViewedAt);
    }

    [Fact]
    public async Task Registry_FallsBackToGenericBusinessObject()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-07T00:00:00Z"));
        IBusinessObjectCastRegistry registry = new BusinessObjectCastRegistry(new IBusinessObjectCaster[]
        {
            new GenericBusinessObjectCaster(timeProvider)
        });

        IBusinessObjectView view = await registry.CastAsync(CreateSource("unknown", TransactionDataItemType.BusinessObject, "{\"value\":\"x\"}"), CancellationToken.None);

        var generic = Assert.IsType<GenericBusinessObjectView>(view);
        Assert.Equal("unknown", generic.ObjectType);
        Assert.Equal(timeProvider.UtcNow, generic.ViewedAt);
    }

    [Theory]
    [InlineData("post", "Post")]
    [InlineData("ticket", "Ticket")]
    [InlineData("event", "Event")]
    [InlineData("payment", "Payment")]
    [InlineData("purchase", "Purchase")]
    [InlineData("receipt", "Receipt")]
    [InlineData("invoice", "Invoice")]
    [InlineData("message", "Message")]
    [InlineData("product", "Product")]
    [InlineData("profile", "Profile")]
    public async Task Registry_CastsGenericFamiliesAsTypedMediaViews(string objectType, string expectedDisplayName)
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<ITimeProvider>(timeProvider)
            .AddBusinessObjectCasting()
            .BuildServiceProvider();
        IBusinessObjectCastRegistry registry = provider.GetRequiredService<IBusinessObjectCastRegistry>();
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(objectType);
        string firstField = definition.Fields.First().Key;
        string stateJson = BusinessObjectPayloadBuilder.Serialize(BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            $"{expectedDisplayName} sample",
            definition.Description,
            "Typed BO body",
            "#typed #bo",
            [],
            new Dictionary<string, string> { [firstField] = "typed field value" }));

        IBusinessObjectView view = await registry.CastAsync(CreateSource(objectType, TransactionDataItemType.BusinessObject, stateJson), CancellationToken.None);

        ITypedBusinessObjectView typed = Assert.IsAssignableFrom<ITypedBusinessObjectView>(view);
        Assert.Equal(expectedDisplayName, typed.Definition.DisplayName);
        Assert.Equal($"{expectedDisplayName} sample", typed.Name);
        Assert.Contains("typed", typed.Tags);
        Assert.Contains(typed.Facts, fact => fact.Value == "typed field value");
        Assert.Equal(timeProvider.UtcNow, typed.ViewedAt);
    }

    private static BusinessObjectCastSource CreateSource(string objectType, TransactionDataItemType itemType, string stateJson)
        => new(
            Context,
            new BusinessObjectId($"{objectType}-1"),
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('a', 64)),
            new OutPoint(new TxId("tx1"), 0),
            stateJson,
            null,
            itemType,
            CreatedAt);

    private sealed class PurchaseRequestCaster(ITimeProvider timeProvider) : IBusinessObjectCaster
    {
        public int Priority => 200;

        public bool CanCast(BusinessObjectCastSource source)
            => string.Equals(source.ObjectType, "purchase-request", StringComparison.Ordinal);

        public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
        {
            string amount = source.StateJson?.Contains("120", StringComparison.Ordinal) == true ? "120" : "0";
            return ValueTask.FromResult<IBusinessObjectView>(new PurchaseRequestView(
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
                amount,
                source.CreatedAt,
                timeProvider.UtcNow));
        }
    }

    private sealed record PurchaseRequestView(
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
        string Amount,
        DateTimeOffset CreatedAt,
        DateTimeOffset ViewedAt) : IBusinessObjectView;
}
