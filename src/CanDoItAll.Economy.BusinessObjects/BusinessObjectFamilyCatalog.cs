using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Ledger;

namespace CanDoItAll.Economy.BusinessObjects;

public enum BusinessObjectFieldKind
{
    Text = 0,
    LongText = 1,
    DateTime = 2,
    Amount = 3,
    Address = 4,
    Url = 5,
    Status = 6,
    Boolean = 7
}

public sealed record BusinessObjectFieldDefinition(
    string Key,
    string Label,
    BusinessObjectFieldKind Kind = BusinessObjectFieldKind.Text,
    bool Required = false,
    string? Placeholder = null);

public sealed record BusinessObjectFamilyDefinition(
    string Family,
    string ObjectType,
    string DisplayName,
    string Icon,
    string Description,
    IReadOnlyList<BusinessObjectFieldDefinition> Fields,
    IReadOnlyList<string>? Aliases = null,
    bool IsUserMintable = true)
{
    public IReadOnlyList<string> TypeKeys
        => [ObjectType, .. Aliases ?? []];

    public bool Matches(string objectType)
        => TypeKeys.Any(key => key.Equals(objectType, StringComparison.OrdinalIgnoreCase));
}

public sealed record BusinessObjectDisplayFact(
    string Label,
    string Value);

public interface ITypedBusinessObjectView : IMediaBusinessObjectView
{
    BusinessObjectFamilyDefinition Definition { get; }

    IReadOnlyDictionary<string, string> Fields { get; }

    IReadOnlyList<BusinessObjectDisplayFact> Facts { get; }
}

public sealed record GenericTypedBusinessObjectView(
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
    IReadOnlyList<BusinessObjectDisplayFact> Facts) : ITypedBusinessObjectView;

public static class BusinessObjectFamilyCatalog
{
    private static readonly IReadOnlyList<BusinessObjectFamilyDefinition> BuiltInFamilies =
    [
        new(
            "content",
            "post",
            "Post",
            "article",
            "Ledger-owned post or short article.",
            [
                new("channel", "Channel", Placeholder: "News, product, support"),
                new("visibility", "Visibility", BusinessObjectFieldKind.Status, Placeholder: "Public")
            ],
            ["bo-post", "posts"]),
        new(
            "content",
            "image",
            "Image",
            "image",
            "Image object with IPFS-backed data items.",
            [
                new("creator", "Creator"),
                new("license", "License"),
                new("source", "Source URL", BusinessObjectFieldKind.Url)
            ],
            ["bo-image", "images"]),
        new(
            "content",
            "message",
            "Message",
            "mail",
            "Account-to-account message or note.",
            [
                new("from", "From"),
                new("to", "To"),
                new("subject", "Subject"),
                new("channel", "Channel")
            ],
            ["messages"]),
        new(
            "event",
            "event",
            "Event",
            "event",
            "Event, milestone, meeting, or operational occurrence.",
            [
                new("startsAt", "Starts at", BusinessObjectFieldKind.DateTime),
                new("endsAt", "Ends at", BusinessObjectFieldKind.DateTime),
                new("eventId", "Event id"),
                new("eventDate", "Event date", BusinessObjectFieldKind.DateTime),
                new("eventClass", "Event class", BusinessObjectFieldKind.Status, Placeholder: "Birthday, Concert, OnlineMeeting"),
                new("location", "Location"),
                new("locationCoordinates", "Location coordinates", Placeholder: "50.087,14.421"),
                new("organizer", "Organizer"),
                new("authorLink", "Author website", BusinessObjectFieldKind.Url),
                new("videoLink", "Video link", BusinessObjectFieldKind.Url),
                new("musicInLink", "Music in link", BusinessObjectFieldKind.Boolean),
                new("mintAuthorAddress", "Mint author address", BusinessObjectFieldKind.Address),
                new("status", "Status", BusinessObjectFieldKind.Status)
            ],
            ["events"]),
        new(
            "event",
            "ticket",
            "Ticket",
            "confirmation_number",
            "Ticket, access pass, service case, or approval ticket.",
            [
                new("ticketNumber", "Ticket number"),
                new("eventId", "Event BO outpoint"),
                new("eventAddress", "Event owner address", BusinessObjectFieldKind.Address),
                new("eventDate", "Event date", BusinessObjectFieldKind.DateTime),
                new("location", "Location"),
                new("locationCoordinates", "Location coordinates", Placeholder: "50.087,14.421"),
                new("authorLink", "Author website", BusinessObjectFieldKind.Url),
                new("ticketClass", "Ticket class", BusinessObjectFieldKind.Status, Placeholder: "Standard, VIP, Family"),
                new("ticketDuration", "Ticket duration", BusinessObjectFieldKind.Status, Placeholder: "Day, Weekend, Week"),
                new("holder", "Holder"),
                new("seat", "Seat or scope"),
                new("used", "Used", BusinessObjectFieldKind.Boolean),
                new("usedAt", "Used at", BusinessObjectFieldKind.DateTime),
                new("status", "Status", BusinessObjectFieldKind.Status)
            ],
            ["tickets"]),
        new(
            "commerce",
            "payment",
            "Payment",
            "payments",
            "Payment request, proof, or settlement record.",
            [
                new("amount", "Amount", BusinessObjectFieldKind.Amount),
                new("currency", "Currency"),
                new("payer", "Payer", BusinessObjectFieldKind.Address),
                new("payee", "Payee", BusinessObjectFieldKind.Address),
                new("reference", "Reference"),
                new("status", "Status", BusinessObjectFieldKind.Status)
            ],
            ["payments"],
            IsUserMintable: false),
        new(
            "commerce",
            "purchase",
            "Purchase",
            "shopping_cart",
            "Purchase order, cart, or procurement record.",
            [
                new("vendor", "Vendor"),
                new("buyer", "Buyer"),
                new("amount", "Amount", BusinessObjectFieldKind.Amount),
                new("currency", "Currency"),
                new("orderNumber", "Order number"),
                new("status", "Status", BusinessObjectFieldKind.Status)
            ],
            ["purchase-order", "order", "orders", "purchases"],
            IsUserMintable: false),
        new(
            "commerce",
            "receipt",
            "Receipt",
            "receipt_long",
            "Receipt, delivery confirmation, or proof of payment.",
            [
                new("receiptNumber", "Receipt number"),
                new("amount", "Amount", BusinessObjectFieldKind.Amount),
                new("currency", "Currency"),
                new("paidBy", "Paid by"),
                new("receivedBy", "Received by"),
                new("issuedAt", "Issued at", BusinessObjectFieldKind.DateTime)
            ],
            ["receipts"],
            IsUserMintable: false),
        new(
            "commerce",
            "invoice",
            "Invoice",
            "request_quote",
            "Invoice or billing request.",
            [
                new("invoiceNumber", "Invoice number"),
                new("seller", "Seller"),
                new("buyer", "Buyer"),
                new("amount", "Amount", BusinessObjectFieldKind.Amount),
                new("currency", "Currency"),
                new("dueAt", "Due at", BusinessObjectFieldKind.DateTime),
                new("status", "Status", BusinessObjectFieldKind.Status)
            ],
            ["invoices"]),
        new(
            "commerce",
            "product",
            "Product",
            "inventory_2",
            "Product, service, or listed offer.",
            [
                new("sku", "SKU"),
                new("price", "Price", BusinessObjectFieldKind.Amount),
                new("currency", "Currency"),
                new("availability", "Availability", BusinessObjectFieldKind.Status),
                new("website", "Website", BusinessObjectFieldKind.Url)
            ],
            ["products", "offer", "offers"]),
        new(
            "identity",
            "profile",
            "Profile",
            "badge",
            "Public profile, organization card, or account identity record.",
            [
                new("firstName", "First name"),
                new("surname", "Surname"),
                new("nickname", "Nickname"),
                new("bio", "Bio", BusinessObjectFieldKind.LongText),
                new("imageLink", "Image link", BusinessObjectFieldKind.Url),
                new("displayName", "Display name"),
                new("contact", "Contact"),
                new("website", "Website", BusinessObjectFieldKind.Url),
                new("role", "Role"),
                new("age", "Age"),
                new("relationshipStatus", "Relationship status"),
                new("currency", "Currency"),
                new("approvalMode", "Approval mode"),
                new("simulationClock", "Simulation clock")
            ],
            ["profiles"],
            IsUserMintable: false),
        new(
            "content",
            "music",
            "Music",
            "music_note",
            "Audio or music proof package.",
            [
                new("artist", "Artist"),
                new("album", "Album"),
                new("license", "License")
            ],
            ["audio", "track", "tracks"])
    ];

    public static IReadOnlyList<BusinessObjectFamilyDefinition> Families => BuiltInFamilies;

    public static IReadOnlyList<BusinessObjectFamilyDefinition> UserMintableFamilies
        => BuiltInFamilies.Where(family => family.IsUserMintable).ToArray();

    public static bool TryFindByObjectType(string? objectType, out BusinessObjectFamilyDefinition definition)
    {
        definition = BuiltInFamilies.First();
        if (string.IsNullOrWhiteSpace(objectType))
        {
            return false;
        }

        BusinessObjectFamilyDefinition? match = BuiltInFamilies.FirstOrDefault(family => family.Matches(objectType.Trim()));
        if (match is null)
        {
            return false;
        }

        definition = match;
        return true;
    }

    public static BusinessObjectFamilyDefinition FindOrDefault(string? objectType)
        => TryFindByObjectType(objectType, out BusinessObjectFamilyDefinition definition)
            ? definition
            : BuiltInFamilies.First();
}

public static class BusinessObjectPayloadBuilder
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static BusinessObjectMediaPayload CreatePayload(
        BusinessObjectFamilyDefinition definition,
        string name,
        string description,
        string text,
        string tags,
        IReadOnlyList<BusinessObjectDataItem> dataItems,
        IReadOnlyDictionary<string, string>? fields = null)
        => new(
            NormalizeText(name, definition.DisplayName),
            NormalizeText(description, definition.Description),
            text ?? string.Empty,
            tags ?? string.Empty,
            DataItems: NormalizeDataItems(dataItems),
            Family: definition.Family,
            Fields: NormalizeFields(definition, fields));

    public static string Serialize(BusinessObjectMediaPayload payload)
        => JsonSerializer.Serialize(payload, SerializerOptions);

    public static BusinessObjectMediaPayload ParseMediaPayload(string? stateJson, string fallbackName)
    {
        if (!TryParseMediaPayload(stateJson, out BusinessObjectMediaPayload? payload))
        {
            return Empty(fallbackName);
        }

        return NormalizePayload(payload!, fallbackName);
    }

    public static bool TryParseMediaPayload(string? stateJson, out BusinessObjectMediaPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return false;
        }

        try
        {
            string json = UnwrapStateJson(stateJson);
            payload = JsonSerializer.Deserialize<BusinessObjectMediaPayload>(json, SerializerOptions);
            return payload is not null;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static IReadOnlyDictionary<string, string> NormalizeFields(
        BusinessObjectFamilyDefinition definition,
        IReadOnlyDictionary<string, string>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        HashSet<string> knownKeys = definition.Fields
            .Select(field => field.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach ((string key, string value) in fields)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string trimmedKey = definition.Fields
                .FirstOrDefault(field => field.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase))
                ?.Key ?? key.Trim();

            if (knownKeys.Contains(trimmedKey) || !trimmedKey.StartsWith('_'))
            {
                normalized[trimmedKey] = value.Trim();
            }
        }

        return normalized;
    }

    public static IReadOnlyList<BusinessObjectDisplayFact> CreateFacts(
        BusinessObjectFamilyDefinition definition,
        IReadOnlyDictionary<string, string>? fields)
    {
        IReadOnlyDictionary<string, string> normalized = NormalizeFields(definition, fields);
        if (normalized.Count == 0)
        {
            return [];
        }

        var facts = new List<BusinessObjectDisplayFact>();
        foreach (BusinessObjectFieldDefinition field in definition.Fields)
        {
            if (normalized.TryGetValue(field.Key, out string? value)
                && !string.IsNullOrWhiteSpace(value))
            {
                facts.Add(new BusinessObjectDisplayFact(field.Label, value));
            }
        }

        foreach ((string key, string value) in normalized)
        {
            if (definition.Fields.Any(field => field.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            facts.Add(new BusinessObjectDisplayFact(HumanizeKey(key), value));
        }

        return facts;
    }

    public static string DefaultName(BusinessObjectFamilyDefinition definition)
        => definition.ObjectType switch
        {
            "post" => "New ledger post",
            "image" => "New image BO",
            "event" => "New event",
            "ticket" => "New ticket",
            "payment" => "New payment record",
            "purchase" => "New purchase record",
            "receipt" => "New receipt",
            "invoice" => "New invoice",
            "message" => "New message",
            "product" => "New product",
            "profile" => "New profile",
            "music" => "New audio package",
            _ => $"New {definition.DisplayName}"
        };

    private static BusinessObjectMediaPayload NormalizePayload(BusinessObjectMediaPayload payload, string fallbackName)
        => payload with
        {
            Name = NormalizeText(payload.Name, fallbackName),
            Description = payload.Description ?? string.Empty,
            Text = payload.Text ?? string.Empty,
            Tags = payload.Tags ?? string.Empty,
            DataItems = NormalizeDataItems(payload.DataItems ?? Array.Empty<BusinessObjectDataItem>()),
            Fields = payload.Fields ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Commerce = payload.Commerce is { IsEmpty: false } commerce
                ? BusinessObjectCommercePayloadBuilder.NormalizeCommerce(commerce)
                : null
        };

    private static BusinessObjectMediaPayload Empty(string fallbackName)
        => new(fallbackName, string.Empty, string.Empty, string.Empty, DataItems: Array.Empty<BusinessObjectDataItem>());

    private static string UnwrapStateJson(string stateJson)
    {
        if (BusinessObjectPayloadEnvelopeReader.TryRead(stateJson, out BusinessObjectPayloadEnvelope envelope))
        {
            return envelope.StateJson;
        }

        return stateJson;
    }

    private static IReadOnlyList<BusinessObjectDataItem> NormalizeDataItems(IReadOnlyList<BusinessObjectDataItem>? dataItems)
    {
        if (dataItems is null || dataItems.Count == 0)
        {
            return Array.Empty<BusinessObjectDataItem>();
        }

        int mainIndex = -1;
        for (int index = 0; index < dataItems.Count; index++)
        {
            if (dataItems[index].IsMain)
            {
                mainIndex = index;
                break;
            }
        }

        if (mainIndex < 0)
        {
            mainIndex = 0;
        }

        BusinessObjectDataItem[] normalized = new BusinessObjectDataItem[dataItems.Count];
        for (int index = 0; index < dataItems.Count; index++)
        {
            bool isMain = index == mainIndex;
            normalized[index] = dataItems[index].IsMain == isMain
                ? dataItems[index]
                : dataItems[index] with { IsMain = isMain };
        }

        return normalized;
    }

    private static string NormalizeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string HumanizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Field";
        }

        string spaced = string.Concat(key.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $" {character}"
                : character.ToString()));
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}

public sealed class GenericTypedBusinessObjectCaster(ITimeProvider timeProvider) : IBusinessObjectCaster
{
    public int Priority => 140;

    public bool CanCast(BusinessObjectCastSource source)
        => source.ItemType == TransactionDataItemType.BusinessObject
            && BusinessObjectFamilyCatalog.TryFindByObjectType(source.ObjectType, out _);

    public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(source.ObjectType);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(source.StateJson, source.ObjectId.Value);
        IReadOnlyDictionary<string, string> fields = BusinessObjectPayloadBuilder.NormalizeFields(definition, payload.Fields);
        IReadOnlyList<BusinessObjectDisplayFact> facts = BusinessObjectPayloadBuilder.CreateFacts(definition, fields);

        return ValueTask.FromResult<IBusinessObjectView>(new GenericTypedBusinessObjectView(
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
            facts));
    }
}
