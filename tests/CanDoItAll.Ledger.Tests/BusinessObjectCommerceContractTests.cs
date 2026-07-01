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
    private static readonly BusinessObjectId TargetObjectId = new("bo:post:commerce-target");
    private static readonly BusinessObjectId PaymentObjectId = new("bo:payment:commerce-payment");
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
            sharePath: "/buy-bo/bo:post:commerce-target");

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
        BusinessObjectPaymentRecord payment = BusinessObjectCommercePayloadBuilder.CreatePaymentRecord(
            PaymentObjectId,
            Bob,
            Alice,
            TargetObjectId,
            targetVersion: 2,
            targetOutPoint: new OutPoint(new TxId("target-tx"), 0),
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            new TxId("payment-tx"),
            ListedAtUtc);
        BusinessObjectPurchaseRecord purchase = BusinessObjectCommercePayloadBuilder.CreatePurchaseRecord(
            BusinessObjectCommercePayloadBuilder.CreatePurchaseObjectId(PaymentObjectId),
            payment,
            ListedAtUtc.AddMinutes(1));
        BusinessObjectReceiptRecord receipt = BusinessObjectCommercePayloadBuilder.CreateReceiptRecord(
            BusinessObjectCommercePayloadBuilder.CreateReceiptObjectId(PaymentObjectId),
            payment,
            deliveredObjectId: TargetObjectId,
            dispatchTxId: new TxId("dispatch-tx"),
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

        BusinessObjectMediaPayload parsedPayment = BusinessObjectPayloadBuilder.ParseMediaPayload(
            BusinessObjectPayloadBuilder.Serialize(paymentPayload),
            "payment");
        Assert.Equal(PaymentObjectId.Value, parsedPayment.Commerce?.Payment?.PaymentObjectId);
        Assert.Equal(Bob.Value, parsedPayment.Commerce?.Payment?.BuyerAddress);
        Assert.Equal(TargetObjectId.Value, parsedPayment.Commerce?.Payment?.TargetObjectId);
    }

    [Fact]
    public void DerivedCommerceObjectIds_AreDeterministicAndBounded()
    {
        BusinessObjectId firstCloneId = BusinessObjectCommercePayloadBuilder.CreateCloneObjectId(TargetObjectId, PaymentObjectId);
        BusinessObjectId secondCloneId = BusinessObjectCommercePayloadBuilder.CreateCloneObjectId(TargetObjectId, PaymentObjectId);

        Assert.Equal(firstCloneId, secondCloneId);
        Assert.StartsWith("bo:clone:", firstCloneId.Value, StringComparison.Ordinal);
        Assert.True(firstCloneId.Value.Length <= LedgerValidationLimits.MaxBusinessObjectIdLength);
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
            PaymentObjectId,
            Bob,
            Alice,
            TargetObjectId,
            targetVersion: 2,
            targetOutPoint: new OutPoint(new TxId("target-tx"), 0),
            Amount.FromInt64(25),
            BusinessObjectSaleMode.Original,
            new TxId("payment-tx"),
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
            new OutPoint(new TxId("tx1"), 0),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject,
            ListedAtUtc);
}
