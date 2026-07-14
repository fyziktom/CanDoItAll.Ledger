using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectCommerceContractTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("default"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly BusinessObjectId TargetObjectId = BusinessObjectId.FromOutPoint(new TxId(new string('a', 64)), 0);
    private static readonly BusinessObjectId PaymentObjectId = BusinessObjectId.FromOutPoint(new TxId(new string('b', 64)), 0);
    private static readonly DateTimeOffset ListedAtUtc = DateTimeOffset.Parse("2026-06-30T10:00:00Z");

    [Fact]
    public void SaleTerms_RoundTripInMediaPayloadAndValidateCopyLimits()
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("post");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            "Commerce post",
            "Short commerce description",
            "Long commerce body.",
            "#commerce",
            []);
        BusinessObjectSaleTerms terms = BusinessObjectCommercePayloadBuilder.CreateSaleTerms(
            BusinessObjectSaleMode.Copy,
            Amount.FromDecimal(12.5m),
            Alice,
            ListedAtUtc,
            maxClones: 3,
            sharePath: "/buy-bo/tx/tx-commerce-target/0");

        BusinessObjectCommerceValidationResult validation = BusinessObjectCommerceValidator.ValidateSaleTerms(terms);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));

        BusinessObjectMediaPayload listedPayload = BusinessObjectCommercePayloadBuilder.WithSaleTerms(payload, terms);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(listedPayload);

        Assert.Contains("\"commerce\"", stateJson, StringComparison.Ordinal);
        Assert.Contains("\"sale\"", stateJson, StringComparison.Ordinal);
        Assert.Contains("\"maxClones\":3", stateJson, StringComparison.Ordinal);

        BusinessObjectMediaPayload parsed = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, "fallback");
        BusinessObjectSaleTerms parsedTerms = Assert.IsType<BusinessObjectSaleTerms>(parsed.Commerce?.Sale);
        Assert.Equal(BusinessObjectSaleMode.Copy, parsedTerms.Mode);
        Assert.Equal(12.5m, parsedTerms.PriceAmount);
        Assert.Equal(Alice.Value, parsedTerms.SellerAddress);
        Assert.Equal(3, parsedTerms.MaxClones);

        BusinessObjectCommerceValidationResult invalid = BusinessObjectCommerceValidator.ValidateSaleTerms(
            parsedTerms with { MaxClones = 0 });
        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Errors, error => error.Contains("max clone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CommerceRecords_BuildTypedPaymentPurchaseAndReceiptPayloads()
    {
        BusinessObjectPaymentRecord paymentDraft = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            Bob,
            Alice,
            TargetObjectId,
            targetVersion: 2,
            targetOutPoint: TargetObjectId.RequireOutPoint(),
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            PaymentObjectId.RequireOutPoint().TxId,
            ListedAtUtc);
        BusinessObjectPaymentRecord payment = paymentDraft with { PaymentObjectId = PaymentObjectId.Value };
        BusinessObjectPurchaseRecord purchase = BusinessObjectCommercePayloadBuilder.CreatePurchaseRecord(
            payment,
            ListedAtUtc.AddMinutes(1));
        BusinessObjectReceiptRecord receipt = BusinessObjectCommercePayloadBuilder.CreateReceiptRecord(
            payment,
            deliveredObjectId: TargetObjectId,
            dispatchTxId: new TxId(new string('c', 64)),
            issuedAtUtc: ListedAtUtc.AddMinutes(2));

        Assert.True(BusinessObjectCommerceValidator.ValidatePaymentRecord(payment).IsValid);
        Assert.True(BusinessObjectCommerceValidator.ValidatePurchaseRecord(purchase).IsValid);
        Assert.True(BusinessObjectCommerceValidator.ValidateReceiptRecord(receipt).IsValid);

        BusinessObjectMediaPayload paymentPayload = BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment);
        BusinessObjectMediaPayload purchasePayload = BusinessObjectCommercePayloadBuilder.CreatePurchasePayload(purchase);
        BusinessObjectMediaPayload receiptPayload = BusinessObjectCommercePayloadBuilder.CreateReceiptPayload(receipt);

        Assert.Equal("commerce", paymentPayload.Family);
        Assert.Equal("Pending", paymentPayload.Fields!["status"]);
        Assert.Equal(TargetObjectId.Value, paymentPayload.Fields["targetObjectId"]);
        Assert.Equal(TargetObjectId.Value, purchasePayload.Fields!["targetObjectId"]);
        Assert.Equal(PaymentObjectId.Value, receiptPayload.Fields!["paymentObjectId"]);
        Assert.False(paymentPayload.Fields.ContainsKey("paymentObjectId"));
        Assert.False(purchasePayload.Fields.ContainsKey("orderNumber"));
        Assert.False(receiptPayload.Fields.ContainsKey("receiptNumber"));

        BusinessObjectMediaPayload parsedPayment = BusinessObjectPayloadBuilder.ParseMediaPayload(
            BusinessObjectPayloadBuilder.Serialize(paymentPayload),
            "payment");
        Assert.True(string.IsNullOrWhiteSpace(parsedPayment.Commerce?.Payment?.PaymentObjectId));
        Assert.Equal(Bob.Value, parsedPayment.Commerce?.Payment?.BuyerAddress);
        Assert.Equal(TargetObjectId.Value, parsedPayment.Commerce?.Payment?.TargetObjectId);
    }

    [Fact]
    public void CommerceReferences_RejectNamedOrMalformedBusinessObjectIds()
    {
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            Bob,
            Alice,
            TargetObjectId,
            targetVersion: 1,
            targetOutPoint: TargetObjectId.RequireOutPoint(),
            Amount.FromInt64(5),
            BusinessObjectSaleMode.Original,
            paymentTxId: null,
            ListedAtUtc);

        BusinessObjectCommerceValidationResult namedTarget = BusinessObjectCommerceValidator.ValidatePaymentRecord(
            payment with { TargetObjectId = "bo:post:user-name", TargetOutPoint = "bo:post:user-name" });
        BusinessObjectCommerceValidationResult malformedLink = BusinessObjectCommerceValidator.ValidatePaymentRecord(
            payment with { ReceiptObjectId = "receipt-by-name" });

        Assert.False(namedTarget.IsValid);
        Assert.Contains(namedTarget.Errors, error => error.Contains("canonical BusinessObject UTXO id", StringComparison.Ordinal));
        Assert.False(malformedLink.IsValid);
        Assert.Contains(malformedLink.Errors, error => error.Contains("canonical BusinessObject UTXO id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CommerceCaster_BindsSelfIdentityFromConfirmedOutputOnly()
    {
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            Bob,
            Alice,
            TargetObjectId,
            targetVersion: 2,
            targetOutPoint: TargetObjectId.RequireOutPoint(),
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            PaymentObjectId.RequireOutPoint().TxId,
            ListedAtUtc);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment));
        var caster = new CommerceBusinessObjectCaster(new ManualTimeProvider(ListedAtUtc));

        IBusinessObjectView view = await caster.CastAsync(CreateSource("payment", stateJson), CancellationToken.None);

        ICommerceBusinessObjectView commerce = Assert.IsAssignableFrom<ICommerceBusinessObjectView>(view);
        Assert.Equal(PaymentObjectId.Value, commerce.Commerce.Payment?.PaymentObjectId);
        Assert.DoesNotContain("paymentObjectId", stateJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registry_CastsPaymentRecordsWithCommerceFacts()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-06-30T11:00:00Z"));
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<ITimeProvider>(timeProvider)
            .AddBusinessObjectCasting()
            .BuildServiceProvider();
        IBusinessObjectCastRegistry registry = provider.GetRequiredService<IBusinessObjectCastRegistry>();
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            Bob,
            Alice,
            TargetObjectId,
            targetVersion: 2,
            targetOutPoint: TargetObjectId.RequireOutPoint(),
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            PaymentObjectId.RequireOutPoint().TxId,
            ListedAtUtc);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(
            BusinessObjectCommercePayloadBuilder.CreatePaymentPayload(payment));

        IBusinessObjectView view = await registry.CastAsync(CreateSource("payment", stateJson), CancellationToken.None);

        ICommerceBusinessObjectView commerce = Assert.IsAssignableFrom<ICommerceBusinessObjectView>(view);
        Assert.Equal("Payment", commerce.Definition.DisplayName);
        Assert.Equal(PaymentObjectId.Value, commerce.Commerce.Payment?.PaymentObjectId);
        Assert.Contains(commerce.Facts, fact => fact.Label == "Target BO" && fact.Value == TargetObjectId.Value);
        Assert.Contains(commerce.Facts, fact => fact.Label == "Status" && fact.Value == "Pending");
        Assert.Equal(timeProvider.UtcNow, commerce.ViewedAt);
    }

    private static BusinessObjectCastSource CreateSource(string objectType, string stateJson)
        => new(
            Context,
            PaymentObjectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('a', 64)),
            PaymentObjectId.RequireOutPoint(),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject,
            ListedAtUtc);
}
