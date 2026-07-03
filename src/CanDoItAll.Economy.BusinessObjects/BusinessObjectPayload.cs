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
        string envelope = ShouldWriteStructuredState(request.ObjectType)
            ? CreateStructuredEnvelope(request)
            : JsonSerializer.Serialize(
                new BusinessObjectPayloadEnvelope(request.ObjectType, request.SchemaVersion, request.StateJson),
                SerializerOptions);

        return CreateData(envelope, request.ItemType, request.ContentType, request.ExternalRef);
    }

    private static bool ShouldWriteStructuredState(string objectType)
        => string.Equals(objectType, SecureLedgerMessages.ObjectType, StringComparison.OrdinalIgnoreCase);

    private static string CreateStructuredEnvelope(CreateBusinessObjectRequest request)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(request.StateJson);
            var envelope = new BusinessObjectStructuredPayloadEnvelope(
                request.ObjectType,
                request.SchemaVersion,
                document.RootElement.Clone());
            return JsonSerializer.Serialize(envelope, SerializerOptions);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(
                new BusinessObjectPayloadEnvelope(request.ObjectType, request.SchemaVersion, request.StateJson),
                SerializerOptions);
        }
    }

    private sealed record BusinessObjectStructuredPayloadEnvelope(
        string ObjectType,
        string SchemaVersion,
        JsonElement StateJson);
}

public sealed record BusinessObjectPayloadEnvelope(string ObjectType, string SchemaVersion, string StateJson);

public static class BusinessObjectPayloadEnvelopeReader
{
    public static bool TryRead(string? inlineJson, out BusinessObjectPayloadEnvelope envelope)
    {
        envelope = default!;
        if (string.IsNullOrWhiteSpace(inlineJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(inlineJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryReadString(root, "objectType", out string? objectType)
                || !TryReadString(root, "schemaVersion", out string? schemaVersion))
            {
                return false;
            }

            if (!TryReadStateJson(root, out string? stateJson))
            {
                return false;
            }

            envelope = new BusinessObjectPayloadEnvelope(objectType, schemaVersion, stateJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryReadStateJson(JsonElement root, out string stateJson)
    {
        stateJson = string.Empty;
        if (!root.TryGetProperty("stateJson", out JsonElement state)
            && !root.TryGetProperty("state", out state))
        {
            return false;
        }

        if (state.ValueKind == JsonValueKind.String)
        {
            string? text = state.GetString();
            if (text is null)
            {
                return false;
            }

            stateJson = text;
            return true;
        }

        if (state.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        stateJson = state.GetRawText();
        return true;
    }
}
