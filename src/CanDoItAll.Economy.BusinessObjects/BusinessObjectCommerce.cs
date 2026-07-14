using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public enum BusinessObjectSaleMode
{
    Original = 0,
    Copy = 1
}

public enum BusinessObjectSaleStatus
{
    NotForSale = 0,
    Listed = 1,
    Sold = 2,
    Paused = 3
}

public enum BusinessObjectCommerceRecordStatus
{
    Pending = 0,
    Dispatched = 1,
    Rejected = 2,
    Cancelled = 3
}

public sealed record BusinessObjectCommerceEnvelope(
    string SchemaVersion = BusinessObjectCommercePayloadBuilder.SchemaVersion,
    BusinessObjectSaleTerms? Sale = null,
    BusinessObjectPaymentRecord? Payment = null,
    BusinessObjectPurchaseRecord? Purchase = null,
    BusinessObjectReceiptRecord? Receipt = null)
{
    public bool IsEmpty => Sale is null && Payment is null && Purchase is null && Receipt is null;
}

public sealed record BusinessObjectSaleTerms(
    BusinessObjectSaleStatus Status,
    BusinessObjectSaleMode Mode,
    decimal PriceAmount,
    string? TokenId,
    string SellerAddress,
    int? MaxClones,
    int SoldCount,
    DateTimeOffset ListedAtUtc,
    string? SharePath = null);

public sealed record BusinessObjectPaymentRecord(
    [property: JsonIgnore] string PaymentObjectId,
    string BuyerAddress,
    string SellerAddress,
    string TargetObjectId,
    int TargetVersion,
    string TargetOutPoint,
    decimal PriceAmount,
    string? TokenId,
    BusinessObjectSaleMode SaleMode,
    BusinessObjectCommerceRecordStatus Status,
    string? PaymentTxId,
    string? DispatchTxId,
    string? ReceiptObjectId,
    string? PurchaseObjectId,
    string? DeliveredObjectId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null,
    string? RejectionReason = null,
    int? SaleMaxClones = null);

public sealed record BusinessObjectPurchaseRecord(
    [property: JsonIgnore] string PurchaseObjectId,
    string PaymentObjectId,
    string BuyerAddress,
    string SellerAddress,
    string TargetObjectId,
    string? DeliveredObjectId,
    decimal PriceAmount,
    string? TokenId,
    BusinessObjectSaleMode SaleMode,
    BusinessObjectCommerceRecordStatus Status,
    string? PaymentTxId,
    string? DispatchTxId,
    string? ReceiptObjectId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record BusinessObjectReceiptRecord(
    [property: JsonIgnore] string ReceiptObjectId,
    string PaymentObjectId,
    string BuyerAddress,
    string SellerAddress,
    string TargetObjectId,
    string DeliveredObjectId,
    decimal PriceAmount,
    string? TokenId,
    BusinessObjectSaleMode SaleMode,
    BusinessObjectCommerceRecordStatus Status,
    string? PaymentTxId,
    string DispatchTxId,
    DateTimeOffset IssuedAtUtc);

public sealed record BusinessObjectCommerceValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static BusinessObjectCommerceValidationResult Valid { get; } = new(Array.Empty<string>());

    public static BusinessObjectCommerceValidationResult FromErrors(IEnumerable<string> errors)
    {
        string[] normalized = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Select(error => error.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return normalized.Length == 0 ? Valid : new BusinessObjectCommerceValidationResult(normalized);
    }

    public void EnsureValid(string subject)
    {
        if (!IsValid)
        {
            throw new InvalidOperationException($"{subject} is invalid: {string.Join("; ", Errors)}");
        }
    }
}

public static class BusinessObjectCommercePayloadBuilder
{
    public const string SchemaVersion = "1.0";
    public const string MainCurrencyCode = "MAIN";

    public static BusinessObjectSaleTerms CreateSaleTerms(
        BusinessObjectSaleMode mode,
        Amount priceAmount,
        Address sellerAddress,
        DateTimeOffset listedAtUtc,
        int? maxClones = null,
        TokenId? tokenId = null,
        string? sharePath = null,
        BusinessObjectSaleStatus status = BusinessObjectSaleStatus.Listed)
    {
        priceAmount.EnsureNonNegative(nameof(priceAmount));
        return new BusinessObjectSaleTerms(
            status,
            mode,
            priceAmount.Value,
            NormalizeTokenId(tokenId),
            sellerAddress.Value,
            maxClones,
            SoldCount: 0,
            listedAtUtc,
            NormalizeOptionalText(sharePath));
    }

    public static BusinessObjectMediaPayload WithSaleTerms(
        BusinessObjectMediaPayload payload,
        BusinessObjectSaleTerms terms)
    {
        BusinessObjectCommerceValidator.ValidateSaleTerms(terms).EnsureValid("BusinessObject sale terms");
        BusinessObjectCommerceEnvelope commerce = NormalizeCommerce(payload.Commerce) with { Sale = terms };
        return payload with { Commerce = commerce };
    }

    public static BusinessObjectMediaPayload ClearSaleTerms(BusinessObjectMediaPayload payload)
    {
        BusinessObjectCommerceEnvelope commerce = NormalizeCommerce(payload.Commerce) with { Sale = null };
        return payload with { Commerce = commerce.IsEmpty ? null : commerce };
    }

    public static string WithSaleTermsStateJson(
        string? stateJson,
        string fallbackName,
        BusinessObjectSaleTerms terms)
    {
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, fallbackName);
        return BusinessObjectPayloadBuilder.Serialize(WithSaleTerms(payload, terms));
    }

    public static string ClearSaleTermsStateJson(string? stateJson, string fallbackName)
    {
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, fallbackName);
        return BusinessObjectPayloadBuilder.Serialize(ClearSaleTerms(payload));
    }

    public static bool TryGetSaleTerms(string? stateJson, out BusinessObjectSaleTerms? terms)
    {
        terms = null;
        if (!BusinessObjectPayloadBuilder.TryParseMediaPayload(stateJson, out BusinessObjectMediaPayload? payload))
        {
            return false;
        }

        terms = payload?.Commerce?.Sale;
        return terms is not null;
    }

    public static BusinessObjectPaymentRecord CreatePaymentRecord(
        Address buyerAddress,
        Address sellerAddress,
        BusinessObjectId targetObjectId,
        int targetVersion,
        OutPoint targetOutPoint,
        Amount priceAmount,
        BusinessObjectSaleMode saleMode,
        TxId? paymentTxId,
        DateTimeOffset createdAtUtc,
        TokenId? tokenId = null,
        int? saleMaxClones = null)
        => CreatePaymentRecordCore(
            string.Empty,
            buyerAddress,
            sellerAddress,
            targetObjectId,
            targetVersion,
            targetOutPoint,
            priceAmount,
            saleMode,
            paymentTxId,
            createdAtUtc,
            tokenId,
            saleMaxClones);

    [Obsolete("Payment BO self identity is assigned from the confirmed output; use the overload without paymentObjectId.")]
    public static BusinessObjectPaymentRecord CreatePaymentRecord(
        BusinessObjectId paymentObjectId,
        Address buyerAddress,
        Address sellerAddress,
        BusinessObjectId targetObjectId,
        int targetVersion,
        OutPoint targetOutPoint,
        Amount priceAmount,
        BusinessObjectSaleMode saleMode,
        TxId? paymentTxId,
        DateTimeOffset createdAtUtc,
        TokenId? tokenId = null,
        int? saleMaxClones = null)
        => CreatePaymentRecordCore(
            paymentObjectId.Value,
            buyerAddress,
            sellerAddress,
            targetObjectId,
            targetVersion,
            targetOutPoint,
            priceAmount,
            saleMode,
            paymentTxId,
            createdAtUtc,
            tokenId,
            saleMaxClones);

    private static BusinessObjectPaymentRecord CreatePaymentRecordCore(
        string paymentObjectId,
        Address buyerAddress,
        Address sellerAddress,
        BusinessObjectId targetObjectId,
        int targetVersion,
        OutPoint targetOutPoint,
        Amount priceAmount,
        BusinessObjectSaleMode saleMode,
        TxId? paymentTxId,
        DateTimeOffset createdAtUtc,
        TokenId? tokenId,
        int? saleMaxClones)
    {
        priceAmount.EnsureNonNegative(nameof(priceAmount));
        return new BusinessObjectPaymentRecord(
            paymentObjectId,
            buyerAddress.Value,
            sellerAddress.Value,
            targetObjectId.Value,
            targetVersion,
            targetOutPoint.ToString(),
            priceAmount.Value,
            NormalizeTokenId(tokenId),
            saleMode,
            BusinessObjectCommerceRecordStatus.Pending,
            paymentTxId?.Value,
            DispatchTxId: null,
            ReceiptObjectId: null,
            PurchaseObjectId: null,
            DeliveredObjectId: null,
            createdAtUtc,
            UpdatedAtUtc: null,
            RejectionReason: null,
            saleMaxClones);
    }

    public static BusinessObjectPurchaseRecord CreatePurchaseRecord(
        BusinessObjectPaymentRecord payment,
        DateTimeOffset createdAtUtc)
        => CreatePurchaseRecordCore(string.Empty, payment, createdAtUtc);

    [Obsolete("Purchase BO self identity is assigned from the confirmed output; use the overload without purchaseObjectId.")]
    public static BusinessObjectPurchaseRecord CreatePurchaseRecord(
        BusinessObjectId purchaseObjectId,
        BusinessObjectPaymentRecord payment,
        DateTimeOffset createdAtUtc)
        => CreatePurchaseRecordCore(purchaseObjectId.Value, payment, createdAtUtc);

    private static BusinessObjectPurchaseRecord CreatePurchaseRecordCore(
        string purchaseObjectId,
        BusinessObjectPaymentRecord payment,
        DateTimeOffset createdAtUtc)
    {
        BusinessObjectCommerceValidator.ValidatePaymentRecord(payment).EnsureValid("BusinessObject payment record");
        return new BusinessObjectPurchaseRecord(
            purchaseObjectId,
            payment.PaymentObjectId,
            payment.BuyerAddress,
            payment.SellerAddress,
            payment.TargetObjectId,
            payment.DeliveredObjectId,
            payment.PriceAmount,
            payment.TokenId,
            payment.SaleMode,
            payment.Status,
            payment.PaymentTxId,
            payment.DispatchTxId,
            payment.ReceiptObjectId,
            createdAtUtc);
    }

    public static BusinessObjectReceiptRecord CreateReceiptRecord(
        BusinessObjectPaymentRecord payment,
        BusinessObjectId deliveredObjectId,
        TxId dispatchTxId,
        DateTimeOffset issuedAtUtc)
        => CreateReceiptRecordCore(
            string.Empty,
            payment,
            deliveredObjectId,
            dispatchTxId,
            issuedAtUtc);

    [Obsolete("Receipt BO self identity is assigned from the confirmed output; use the overload without receiptObjectId.")]
    public static BusinessObjectReceiptRecord CreateReceiptRecord(
        BusinessObjectId receiptObjectId,
        BusinessObjectPaymentRecord payment,
        BusinessObjectId deliveredObjectId,
        TxId dispatchTxId,
        DateTimeOffset issuedAtUtc)
        => CreateReceiptRecordCore(
            receiptObjectId.Value,
            payment,
            deliveredObjectId,
            dispatchTxId,
            issuedAtUtc);

    private static BusinessObjectReceiptRecord CreateReceiptRecordCore(
        string receiptObjectId,
        BusinessObjectPaymentRecord payment,
        BusinessObjectId deliveredObjectId,
        TxId dispatchTxId,
        DateTimeOffset issuedAtUtc)
    {
        BusinessObjectCommerceValidator.ValidatePaymentRecord(payment).EnsureValid("BusinessObject payment record");
        return new BusinessObjectReceiptRecord(
            receiptObjectId,
            payment.PaymentObjectId,
            payment.BuyerAddress,
            payment.SellerAddress,
            payment.TargetObjectId,
            deliveredObjectId.Value,
            payment.PriceAmount,
            payment.TokenId,
            payment.SaleMode,
            BusinessObjectCommerceRecordStatus.Dispatched,
            payment.PaymentTxId,
            dispatchTxId.Value,
            issuedAtUtc);
    }

    public static BusinessObjectMediaPayload CreatePaymentPayload(BusinessObjectPaymentRecord payment)
    {
        BusinessObjectCommerceValidator.ValidatePaymentRecord(payment).EnsureValid("BusinessObject payment record");
        BusinessObjectPaymentRecord storedPayment = payment with { PaymentObjectId = string.Empty };
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("payment");
        IReadOnlyDictionary<string, string> fields = CommerceFields(storedPayment);
        return BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            $"Payment for {storedPayment.TargetObjectId}",
            "Buyer payment record for BusinessObject commerce.",
            $"Payment from {storedPayment.BuyerAddress} to {storedPayment.SellerAddress} for {storedPayment.TargetObjectId}.",
            "#commerce #payment",
            [],
            fields) with
        {
            Commerce = new BusinessObjectCommerceEnvelope(Payment: storedPayment)
        };
    }

    public static BusinessObjectMediaPayload CreatePurchasePayload(BusinessObjectPurchaseRecord purchase)
    {
        BusinessObjectCommerceValidator.ValidatePurchaseRecord(purchase).EnsureValid("BusinessObject purchase record");
        BusinessObjectPurchaseRecord storedPurchase = purchase with { PurchaseObjectId = string.Empty };
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("purchase");
        IReadOnlyDictionary<string, string> fields = CommerceFields(storedPurchase);
        return BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            $"Purchase for {storedPurchase.TargetObjectId}",
            "Buyer-side BusinessObject purchase record.",
            $"Purchase of {storedPurchase.TargetObjectId} from {storedPurchase.SellerAddress}.",
            "#commerce #purchase",
            [],
            fields) with
        {
            Commerce = new BusinessObjectCommerceEnvelope(Purchase: storedPurchase)
        };
    }

    public static BusinessObjectMediaPayload CreateReceiptPayload(BusinessObjectReceiptRecord receipt)
    {
        BusinessObjectCommerceValidator.ValidateReceiptRecord(receipt).EnsureValid("BusinessObject receipt record");
        BusinessObjectReceiptRecord storedReceipt = receipt with { ReceiptObjectId = string.Empty };
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("receipt");
        IReadOnlyDictionary<string, string> fields = CommerceFields(storedReceipt);
        return BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            $"Receipt for {storedReceipt.TargetObjectId}",
            "Seller-side BusinessObject commerce receipt.",
            $"Receipt for dispatch of {storedReceipt.DeliveredObjectId} to {storedReceipt.BuyerAddress}.",
            "#commerce #receipt",
            [],
            fields) with
        {
            Commerce = new BusinessObjectCommerceEnvelope(Receipt: storedReceipt)
        };
    }

    [Obsolete("BusinessObject ids are confirmed output outpoints; do not precompute payment ids.")]
    public static BusinessObjectId CreatePaymentObjectId(
        BusinessObjectId targetObjectId,
        Address buyerAddress,
        DateTimeOffset requestedAtUtc)
        => CreateDerivedObjectId("bo:payment", targetObjectId.Value, buyerAddress.Value, requestedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

    [Obsolete("BusinessObject ids are confirmed output outpoints; do not precompute purchase ids.")]
    public static BusinessObjectId CreatePurchaseObjectId(BusinessObjectId paymentObjectId)
        => CreateDerivedObjectId("bo:purchase", paymentObjectId.Value);

    [Obsolete("BusinessObject ids are confirmed output outpoints; do not precompute receipt ids.")]
    public static BusinessObjectId CreateReceiptObjectId(BusinessObjectId paymentObjectId)
        => CreateDerivedObjectId("bo:receipt", paymentObjectId.Value);

    [Obsolete("BusinessObject ids are confirmed output outpoints; do not precompute clone ids.")]
    public static BusinessObjectId CreateCloneObjectId(BusinessObjectId targetObjectId, BusinessObjectId paymentObjectId)
        => CreateDerivedObjectId("bo:clone", targetObjectId.Value, paymentObjectId.Value);

    public static BusinessObjectCommerceEnvelope NormalizeCommerce(BusinessObjectCommerceEnvelope? commerce)
        => commerce is null
            ? new BusinessObjectCommerceEnvelope()
            : commerce with { SchemaVersion = string.IsNullOrWhiteSpace(commerce.SchemaVersion) ? SchemaVersion : commerce.SchemaVersion.Trim() };

    public static IReadOnlyList<BusinessObjectDisplayFact> CreateCommerceFacts(BusinessObjectCommerceEnvelope? commerce)
    {
        if (commerce is null || commerce.IsEmpty)
        {
            return [];
        }

        var facts = new List<BusinessObjectDisplayFact>();
        if (commerce.Sale is { } sale)
        {
            facts.Add(new BusinessObjectDisplayFact("Sale status", sale.Status.ToString()));
            facts.Add(new BusinessObjectDisplayFact("Sale mode", sale.Mode.ToString()));
            facts.Add(new BusinessObjectDisplayFact("Price", FormatPrice(sale.PriceAmount, sale.TokenId)));
            facts.Add(new BusinessObjectDisplayFact("Seller", sale.SellerAddress));
            if (sale.MaxClones is { } maxClones)
            {
                facts.Add(new BusinessObjectDisplayFact("Max clones", maxClones.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (commerce.Payment is { } payment)
        {
            AddPaymentFacts(facts, payment);
        }

        if (commerce.Purchase is { } purchase)
        {
            AddPurchaseFacts(facts, purchase);
        }

        if (commerce.Receipt is { } receipt)
        {
            AddReceiptFacts(facts, receipt);
        }

        return facts;
    }

    internal static IReadOnlyDictionary<string, string> MergeCommerceFields(
        IReadOnlyDictionary<string, string>? fields,
        BusinessObjectCommerceEnvelope? commerce)
    {
        var merged = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (fields is not null)
        {
            foreach ((string key, string value) in fields)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    merged[key] = value;
                }
            }
        }

        if (commerce?.Payment is { } payment)
        {
            AddFields(merged, CommerceFields(payment));
        }

        if (commerce?.Purchase is { } purchase)
        {
            AddFields(merged, CommerceFields(purchase));
        }

        if (commerce?.Receipt is { } receipt)
        {
            AddFields(merged, CommerceFields(receipt));
        }

        return merged;
    }

    private static IReadOnlyDictionary<string, string> CommerceFields(BusinessObjectPaymentRecord payment)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = FormatDecimal(payment.PriceAmount),
            ["currency"] = FormatCurrency(payment.TokenId),
            ["payer"] = payment.BuyerAddress,
            ["payee"] = payment.SellerAddress,
            ["reference"] = payment.TargetObjectId,
            ["status"] = payment.Status.ToString(),
            ["targetObjectId"] = payment.TargetObjectId,
            ["buyerAddress"] = payment.BuyerAddress,
            ["sellerAddress"] = payment.SellerAddress,
            ["saleMode"] = payment.SaleMode.ToString()
        };
        AddCurrentObjectField(fields, "paymentObjectId", payment.PaymentObjectId);
        return fields;
    }

    private static IReadOnlyDictionary<string, string> CommerceFields(BusinessObjectPurchaseRecord purchase)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = FormatDecimal(purchase.PriceAmount),
            ["currency"] = FormatCurrency(purchase.TokenId),
            ["vendor"] = purchase.SellerAddress,
            ["buyer"] = purchase.BuyerAddress,
            ["status"] = purchase.Status.ToString(),
            ["targetObjectId"] = purchase.TargetObjectId,
            ["paymentObjectId"] = purchase.PaymentObjectId
        };
        AddCurrentObjectField(fields, "orderNumber", purchase.PurchaseObjectId);
        return fields;
    }

    private static IReadOnlyDictionary<string, string> CommerceFields(BusinessObjectReceiptRecord receipt)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = FormatDecimal(receipt.PriceAmount),
            ["currency"] = FormatCurrency(receipt.TokenId),
            ["paidBy"] = receipt.BuyerAddress,
            ["receivedBy"] = receipt.SellerAddress,
            ["issuedAt"] = receipt.IssuedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ["targetObjectId"] = receipt.TargetObjectId,
            ["paymentObjectId"] = receipt.PaymentObjectId,
            ["deliveredObjectId"] = receipt.DeliveredObjectId,
            ["dispatchTxId"] = receipt.DispatchTxId,
            ["status"] = receipt.Status.ToString()
        };
        AddCurrentObjectField(fields, "receiptNumber", receipt.ReceiptObjectId);
        return fields;
    }

    private static void AddCurrentObjectField(
        IDictionary<string, string> fields,
        string fieldName,
        string? currentObjectId)
    {
        if (!string.IsNullOrWhiteSpace(currentObjectId))
        {
            fields[fieldName] = currentObjectId;
        }
    }

    private static void AddPaymentFacts(List<BusinessObjectDisplayFact> facts, BusinessObjectPaymentRecord payment)
    {
        facts.Add(new BusinessObjectDisplayFact("Payment BO", payment.PaymentObjectId));
        facts.Add(new BusinessObjectDisplayFact("Target BO", payment.TargetObjectId));
        facts.Add(new BusinessObjectDisplayFact("Buyer", payment.BuyerAddress));
        facts.Add(new BusinessObjectDisplayFact("Seller", payment.SellerAddress));
        facts.Add(new BusinessObjectDisplayFact("Status", payment.Status.ToString()));
        facts.Add(new BusinessObjectDisplayFact("Price", FormatPrice(payment.PriceAmount, payment.TokenId)));
        facts.Add(new BusinessObjectDisplayFact("Sale mode", payment.SaleMode.ToString()));
        AddOptionalFact(facts, "Payment tx", payment.PaymentTxId);
        AddOptionalFact(facts, "Dispatch tx", payment.DispatchTxId);
        AddOptionalFact(facts, "Delivered BO", payment.DeliveredObjectId);
    }

    private static void AddPurchaseFacts(List<BusinessObjectDisplayFact> facts, BusinessObjectPurchaseRecord purchase)
    {
        facts.Add(new BusinessObjectDisplayFact("Purchase BO", purchase.PurchaseObjectId));
        facts.Add(new BusinessObjectDisplayFact("Payment BO", purchase.PaymentObjectId));
        facts.Add(new BusinessObjectDisplayFact("Target BO", purchase.TargetObjectId));
        facts.Add(new BusinessObjectDisplayFact("Buyer", purchase.BuyerAddress));
        facts.Add(new BusinessObjectDisplayFact("Seller", purchase.SellerAddress));
        facts.Add(new BusinessObjectDisplayFact("Status", purchase.Status.ToString()));
        facts.Add(new BusinessObjectDisplayFact("Price", FormatPrice(purchase.PriceAmount, purchase.TokenId)));
        AddOptionalFact(facts, "Delivered BO", purchase.DeliveredObjectId);
    }

    private static void AddReceiptFacts(List<BusinessObjectDisplayFact> facts, BusinessObjectReceiptRecord receipt)
    {
        facts.Add(new BusinessObjectDisplayFact("Receipt BO", receipt.ReceiptObjectId));
        facts.Add(new BusinessObjectDisplayFact("Payment BO", receipt.PaymentObjectId));
        facts.Add(new BusinessObjectDisplayFact("Target BO", receipt.TargetObjectId));
        facts.Add(new BusinessObjectDisplayFact("Delivered BO", receipt.DeliveredObjectId));
        facts.Add(new BusinessObjectDisplayFact("Buyer", receipt.BuyerAddress));
        facts.Add(new BusinessObjectDisplayFact("Seller", receipt.SellerAddress));
        facts.Add(new BusinessObjectDisplayFact("Status", receipt.Status.ToString()));
        facts.Add(new BusinessObjectDisplayFact("Price", FormatPrice(receipt.PriceAmount, receipt.TokenId)));
        facts.Add(new BusinessObjectDisplayFact("Dispatch tx", receipt.DispatchTxId));
    }

    private static void AddOptionalFact(List<BusinessObjectDisplayFact> facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Add(new BusinessObjectDisplayFact(label, value));
        }
    }

    private static void AddFields(IDictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach ((string key, string value) in source)
        {
            target[key] = value;
        }
    }

    private static BusinessObjectId CreateDerivedObjectId(string prefix, params string[] parts)
    {
        string material = string.Join("|", parts.Select(part => part.Trim()));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        string suffix = Convert.ToHexString(hash).ToLowerInvariant()[..24];
        return new BusinessObjectId($"{prefix}:{suffix}");
    }

    private static string? NormalizeTokenId(TokenId? tokenId)
        => tokenId is { } value && !string.IsNullOrWhiteSpace(value.Value)
            ? value.Value.Trim()
            : null;

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatCurrency(string? tokenId)
        => string.IsNullOrWhiteSpace(tokenId) ? MainCurrencyCode : tokenId;

    private static string FormatPrice(decimal amount, string? tokenId)
        => $"{FormatDecimal(amount)} {FormatCurrency(tokenId)}";

    private static string FormatDecimal(decimal amount)
        => amount.ToString("0.#############################", CultureInfo.InvariantCulture);
}

public static class BusinessObjectCommerceValidator
{
    public static BusinessObjectCommerceValidationResult ValidateSaleTerms(BusinessObjectSaleTerms? terms)
    {
        var errors = new List<string>();
        if (terms is null)
        {
            return BusinessObjectCommerceValidationResult.FromErrors(["sale terms are required"]);
        }

        if (terms.Status == BusinessObjectSaleStatus.NotForSale)
        {
            return BusinessObjectCommerceValidationResult.Valid;
        }

        ValidatePositiveAmount(errors, terms.PriceAmount, "sale price");
        ValidateAddress(errors, terms.SellerAddress, "seller address");
        ValidateObjectToken(errors, terms.TokenId, "token id");

        if (terms.Mode == BusinessObjectSaleMode.Copy)
        {
            if (terms.MaxClones is null || terms.MaxClones <= 0)
            {
                errors.Add("copy sale requires a positive max clone count");
            }
        }
        else if (terms.MaxClones <= 0)
        {
            errors.Add("max clone count must be positive when supplied");
        }

        if (terms.SoldCount < 0)
        {
            errors.Add("sold count must be non-negative");
        }

        return BusinessObjectCommerceValidationResult.FromErrors(errors);
    }

    public static BusinessObjectCommerceValidationResult ValidatePaymentRecord(BusinessObjectPaymentRecord? payment)
    {
        var errors = new List<string>();
        if (payment is null)
        {
            return BusinessObjectCommerceValidationResult.FromErrors(["payment record is required"]);
        }

        ValidateAddress(errors, payment.BuyerAddress, "buyer address");
        ValidateAddress(errors, payment.SellerAddress, "seller address");
        ValidateBusinessObjectId(errors, payment.TargetObjectId, "target object id");
        ValidateOptionalBusinessObjectId(errors, payment.ReceiptObjectId, "receipt object id");
        ValidateOptionalBusinessObjectId(errors, payment.PurchaseObjectId, "purchase object id");
        ValidateOptionalBusinessObjectId(errors, payment.DeliveredObjectId, "delivered object id");
        ValidatePositiveAmount(errors, payment.PriceAmount, "payment amount");
        ValidateObjectToken(errors, payment.TokenId, "token id");

        if (payment.TargetVersion <= 0)
        {
            errors.Add("target version must be positive");
        }

        if (string.IsNullOrWhiteSpace(payment.TargetOutPoint))
        {
            errors.Add("target outpoint is required");
        }
        else if (!string.Equals(payment.TargetOutPoint, payment.TargetObjectId, StringComparison.Ordinal))
        {
            errors.Add("target outpoint must match target object id");
        }

        ValidateDispatchedState(errors, payment.Status, payment.DispatchTxId, payment.DeliveredObjectId);
        return BusinessObjectCommerceValidationResult.FromErrors(errors);
    }

    public static BusinessObjectCommerceValidationResult ValidatePurchaseRecord(BusinessObjectPurchaseRecord? purchase)
    {
        var errors = new List<string>();
        if (purchase is null)
        {
            return BusinessObjectCommerceValidationResult.FromErrors(["purchase record is required"]);
        }

        ValidateBusinessObjectId(errors, purchase.PaymentObjectId, "payment object id");
        ValidateAddress(errors, purchase.BuyerAddress, "buyer address");
        ValidateAddress(errors, purchase.SellerAddress, "seller address");
        ValidateBusinessObjectId(errors, purchase.TargetObjectId, "target object id");
        ValidateOptionalBusinessObjectId(errors, purchase.DeliveredObjectId, "delivered object id");
        ValidateOptionalBusinessObjectId(errors, purchase.ReceiptObjectId, "receipt object id");
        ValidatePositiveAmount(errors, purchase.PriceAmount, "purchase amount");
        ValidateObjectToken(errors, purchase.TokenId, "token id");
        ValidateDispatchedState(errors, purchase.Status, purchase.DispatchTxId, purchase.DeliveredObjectId);
        return BusinessObjectCommerceValidationResult.FromErrors(errors);
    }

    public static BusinessObjectCommerceValidationResult ValidateReceiptRecord(BusinessObjectReceiptRecord? receipt)
    {
        var errors = new List<string>();
        if (receipt is null)
        {
            return BusinessObjectCommerceValidationResult.FromErrors(["receipt record is required"]);
        }

        ValidateBusinessObjectId(errors, receipt.PaymentObjectId, "payment object id");
        ValidateAddress(errors, receipt.BuyerAddress, "buyer address");
        ValidateAddress(errors, receipt.SellerAddress, "seller address");
        ValidateBusinessObjectId(errors, receipt.TargetObjectId, "target object id");
        ValidateBusinessObjectId(errors, receipt.DeliveredObjectId, "delivered object id");
        ValidatePositiveAmount(errors, receipt.PriceAmount, "receipt amount");
        ValidateObjectToken(errors, receipt.TokenId, "token id");

        if (string.IsNullOrWhiteSpace(receipt.DispatchTxId))
        {
            errors.Add("dispatch tx id is required");
        }

        return BusinessObjectCommerceValidationResult.FromErrors(errors);
    }

    private static void ValidateDispatchedState(
        List<string> errors,
        BusinessObjectCommerceRecordStatus status,
        string? dispatchTxId,
        string? deliveredObjectId)
    {
        if (status != BusinessObjectCommerceRecordStatus.Dispatched)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(dispatchTxId))
        {
            errors.Add("dispatched status requires dispatch tx id");
        }

        if (string.IsNullOrWhiteSpace(deliveredObjectId))
        {
            errors.Add("dispatched status requires delivered object id");
        }
    }

    private static void ValidatePositiveAmount(List<string> errors, decimal amount, string fieldName)
    {
        if (amount <= 0m)
        {
            errors.Add($"{fieldName} must be positive");
        }
    }

    private static void ValidateAddress(List<string> errors, string? value, string fieldName)
    {
        if (!Address.TryParse(value, out _))
        {
            errors.Add($"{fieldName} is invalid");
        }
    }

    private static void ValidateBusinessObjectId(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required");
            return;
        }

        if (!new BusinessObjectId(value).IsCanonicalOutPointId())
        {
            errors.Add($"{fieldName} must be a canonical BusinessObject UTXO id");
        }
    }

    private static void ValidateOptionalBusinessObjectId(
        List<string> errors,
        string? value,
        string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ValidateBusinessObjectId(errors, value, fieldName);
        }
    }

    private static void ValidateObjectToken(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!LedgerValueValidation.IsValidIdentifier(value, LedgerValidationLimits.MaxTokenIdLength))
        {
            errors.Add($"{fieldName} is invalid");
        }
    }
}

public interface ICommerceBusinessObjectView : ITypedBusinessObjectView
{
    BusinessObjectCommerceEnvelope Commerce { get; }
}

public sealed record CommerceTypedBusinessObjectView(
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
    string? ExternalRef,
    TransactionDataItemType ItemType,
    DateTimeOffset CreatedAt,
    DateTimeOffset ViewedAt,
    string Name,
    string Description,
    string Text,
    string? Author,
    string? Link,
    string? ImageLink,
    IReadOnlyList<string> Tags,
    IReadOnlyList<BusinessObjectDataItem> DataItems,
    BusinessObjectFamilyDefinition Definition,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<BusinessObjectDisplayFact> Facts,
    BusinessObjectCommerceEnvelope Commerce) : ICommerceBusinessObjectView;

public sealed class CommerceBusinessObjectCaster(ITimeProvider timeProvider) : IBusinessObjectCaster
{
    public int Priority => 150;

    public bool CanCast(BusinessObjectCastSource source)
        => source.ItemType == TransactionDataItemType.BusinessObject
            && IsCommerceType(source.ObjectType);

    public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(source.ObjectType);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(source.StateJson, "Untitled");
        BusinessObjectCommerceEnvelope commerce = BindCurrentObjectIdentity(
            BusinessObjectCommercePayloadBuilder.NormalizeCommerce(payload.Commerce),
            source.ObjectId);
        IReadOnlyDictionary<string, string> fields = BusinessObjectCommercePayloadBuilder.MergeCommerceFields(payload.Fields, commerce);
        IReadOnlyList<BusinessObjectDisplayFact> facts = MergeFacts(
            BusinessObjectPayloadBuilder.CreateFacts(definition, fields),
            BusinessObjectCommercePayloadBuilder.CreateCommerceFacts(commerce));

        return ValueTask.FromResult<IBusinessObjectView>(new CommerceTypedBusinessObjectView(
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
            source.ExternalRef,
            source.ItemType,
            source.CreatedAt,
            timeProvider.UtcNow,
            payload.Name,
            payload.Description,
            payload.Text,
            payload.Author,
            payload.Link,
            payload.ImageLink,
            payload.TagList,
            payload.DataItems ?? Array.Empty<BusinessObjectDataItem>(),
            definition,
            fields,
            facts,
            commerce));
    }

    private static BusinessObjectCommerceEnvelope BindCurrentObjectIdentity(
        BusinessObjectCommerceEnvelope commerce,
        BusinessObjectId currentObjectId)
        => commerce with
        {
            Payment = commerce.Payment is null
                ? null
                : commerce.Payment with { PaymentObjectId = currentObjectId.Value },
            Purchase = commerce.Purchase is null
                ? null
                : commerce.Purchase with { PurchaseObjectId = currentObjectId.Value },
            Receipt = commerce.Receipt is null
                ? null
                : commerce.Receipt with { ReceiptObjectId = currentObjectId.Value }
        };

    private static bool IsCommerceType(string objectType)
        => string.Equals(objectType, "payment", StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectType, "purchase", StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectType, "receipt", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<BusinessObjectDisplayFact> MergeFacts(
        IReadOnlyList<BusinessObjectDisplayFact> baseFacts,
        IReadOnlyList<BusinessObjectDisplayFact> commerceFacts)
    {
        var merged = new List<BusinessObjectDisplayFact>(baseFacts.Count + commerceFacts.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (BusinessObjectDisplayFact fact in baseFacts.Concat(commerceFacts))
        {
            string key = $"{fact.Label}:{fact.Value}";
            if (seen.Add(key))
            {
                merged.Add(fact);
            }
        }

        return merged;
    }
}
