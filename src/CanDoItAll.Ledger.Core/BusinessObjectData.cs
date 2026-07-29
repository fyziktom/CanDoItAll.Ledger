using System.Text.Json;

namespace CanDoItAll.Ledger.Core;

public static class BusinessObjectReferenceRelationships
{
    public const string PreviousVersion = "previous-version";
    public const string CloneSource = "clone-source";
    public const string MergeSource = "merge-source";
}

public sealed record BusinessObjectDataReference(string Relationship, BusinessObjectId ObjectId);

public sealed record BusinessObjectPayloadEnvelope(
    string ObjectType,
    string SchemaVersion,
    string StateJson,
    IReadOnlyList<BusinessObjectDataReference> References);

public static class BusinessObjectPayloadEnvelopeReader
{
    public static bool TryRead(string? inlineJson, out BusinessObjectPayloadEnvelope envelope)
        => TryRead(inlineJson, out envelope, out _);

    /// <summary>
    /// Reads a structured BusinessObject payload and reports whether the payload claimed to
    /// contain source references that could not be parsed safely. Callers enforcing reference
    /// semantics must reject <paramref name="hasInvalidReferences"/> instead of treating it as
    /// an older, unstructured payload.
    /// </summary>
    public static bool TryRead(
        string? inlineJson,
        out BusinessObjectPayloadEnvelope envelope,
        out bool hasInvalidReferences)
    {
        envelope = default!;
        hasInvalidReferences = false;
        if (string.IsNullOrWhiteSpace(inlineJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(inlineJson);
            JsonElement root = document.RootElement;
            bool hasReferencesProperty = root.ValueKind == JsonValueKind.Object
                && root.EnumerateObject().Any(property => property.NameEquals("references"));
            if (root.ValueKind != JsonValueKind.Object
                || HasAmbiguousEnvelopeProperties(root)
                || !TryReadString(root, "objectType", out string? objectType)
                || !TryReadString(root, "schemaVersion", out string? schemaVersion)
                || !TryReadStateJson(root, out string? stateJson)
                || !TryReadReferences(root, out IReadOnlyList<BusinessObjectDataReference>? references))
            {
                hasInvalidReferences = hasReferencesProperty;
                return false;
            }

            envelope = new BusinessObjectPayloadEnvelope(objectType, schemaVersion, stateJson, references);
            return true;
        }
        catch (JsonException)
        {
            hasInvalidReferences = inlineJson.Contains("\"references\"", StringComparison.Ordinal);
            return false;
        }
    }

    private static bool HasAmbiguousEnvelopeProperties(JsonElement root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not ("objectType" or "schemaVersion" or "stateJson" or "state" or "references"))
            {
                continue;
            }

            if (!seen.Add(property.Name))
            {
                return true;
            }
        }

        return seen.Contains("stateJson") && seen.Contains("state");
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

    private static bool TryReadReferences(
        JsonElement root,
        out IReadOnlyList<BusinessObjectDataReference> references)
    {
        references = [];
        if (!root.TryGetProperty("references", out JsonElement property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<BusinessObjectDataReference>();
        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || HasDuplicateReferenceProperties(item)
                || !TryReadString(item, "relationship", out string? relationship)
                || !TryReadString(item, "objectId", out string? objectIdValue))
            {
                return false;
            }

            var objectId = new BusinessObjectId(objectIdValue);
            if (!objectId.IsCanonicalOutPointId())
            {
                return false;
            }

            parsed.Add(new BusinessObjectDataReference(relationship, objectId));
        }

        references = parsed;
        return true;
    }

    private static bool HasDuplicateReferenceProperties(JsonElement item)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in item.EnumerateObject())
        {
            if (property.Name is not ("relationship" or "objectId"))
            {
                continue;
            }

            if (!seen.Add(property.Name))
            {
                return true;
            }
        }

        return false;
    }
}
