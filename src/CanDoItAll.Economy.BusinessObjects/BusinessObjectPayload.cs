using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

internal static class BusinessObjectPayload
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static HashHex HashJson(string json)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return new HashHex(Convert.ToHexString(hash).ToLowerInvariant());
    }

    public static TransactionData CreateData(
        string json,
        TransactionDataItemType itemType,
        string contentType = "application/json",
        string? externalRef = null)
        => new(contentType, HashJson(json), json, externalRef, itemType);

    public static TransactionData CreateData(CreateBusinessObjectRequest request)
    {
        string envelope = CreateStructuredEnvelope(
            request.ObjectType,
            request.SchemaVersion,
            request.StateJson,
            []);
        return CreateData(envelope, request.ItemType, request.ContentType, request.ExternalRef);
    }

    public static TransactionData CreateStructuredData(
        string objectType,
        string schemaVersion,
        string stateJson,
        IReadOnlyList<BusinessObjectDataReference>? references = null,
        TransactionDataItemType itemType = TransactionDataItemType.BusinessObject,
        string contentType = "application/json",
        string? externalRef = null)
    {
        string envelope = CreateStructuredEnvelope(
            objectType,
            schemaVersion,
            stateJson,
            references ?? []);
        return CreateData(envelope, itemType, contentType, externalRef);
    }

    private static string CreateStructuredEnvelope(
        string objectType,
        string schemaVersion,
        string stateJson,
        IReadOnlyList<BusinessObjectDataReference> references)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(stateJson);
            BusinessObjectStructuredPayloadReference[] serializedReferences = references
                .Select(reference => new BusinessObjectStructuredPayloadReference(
                    reference.Relationship,
                    reference.ObjectId.Value))
                .ToArray();
            var envelope = new BusinessObjectStructuredPayloadEnvelope(
                objectType,
                schemaVersion,
                document.RootElement.Clone(),
                serializedReferences);
            return JsonSerializer.Serialize(envelope, SerializerOptions);
        }
        catch (JsonException)
        {
            BusinessObjectStructuredPayloadReference[] serializedReferences = references
                .Select(reference => new BusinessObjectStructuredPayloadReference(
                    reference.Relationship,
                    reference.ObjectId.Value))
                .ToArray();
            return JsonSerializer.Serialize(
                new BusinessObjectStringPayloadEnvelope(
                    objectType,
                    schemaVersion,
                    stateJson,
                    serializedReferences),
                SerializerOptions);
        }
    }

    private sealed record BusinessObjectStructuredPayloadEnvelope(
        string ObjectType,
        string SchemaVersion,
        JsonElement StateJson,
        IReadOnlyList<BusinessObjectStructuredPayloadReference> References);

    private sealed record BusinessObjectStringPayloadEnvelope(
        string ObjectType,
        string SchemaVersion,
        string StateJson,
        IReadOnlyList<BusinessObjectStructuredPayloadReference> References);

    private sealed record BusinessObjectStructuredPayloadReference(
        string Relationship,
        string ObjectId);
}
