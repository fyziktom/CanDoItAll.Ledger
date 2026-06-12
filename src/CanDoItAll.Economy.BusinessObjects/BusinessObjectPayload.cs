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
        string envelope = JsonSerializer.Serialize(
            new BusinessObjectPayloadEnvelope(request.ObjectType, request.SchemaVersion, request.StateJson),
            SerializerOptions);

        return CreateData(envelope, request.ItemType, request.ContentType, request.ExternalRef);
    }
}

public sealed record BusinessObjectPayloadEnvelope(string ObjectType, string SchemaVersion, string StateJson);
