using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Economy.Core;

public static class PermissionTokenAuthority
{
    public static TokenId PermissionTokenId { get; } = new("permission");

    public const string PermissionContentType = "application/vnd.candoitall.permission+json";

    public const string MintNewTokenTypeAction = "mint_new_tok_type";

    public static BusinessObjectId CreatePermissionObjectId(string action, string tokenNamespace, string? suffix = null)
    {
        string normalizedAction = NormalizeAction(action);
        string normalizedNamespace = NormalizeNamespace(tokenNamespace);
        string normalizedSuffix = string.IsNullOrWhiteSpace(suffix)
            ? Guid.NewGuid().ToString("N")
            : suffix.Trim();
        return new BusinessObjectId($"permission:{normalizedAction}:{normalizedNamespace}:{normalizedSuffix}");
    }

    public static TransactionData CreateData(PermissionGrant grant)
    {
        string json = JsonSerializer.Serialize(new PermissionGrantJson(
            grant.Action,
            grant.Namespace,
            grant.Repeatable));

        return new TransactionData(
            PermissionContentType,
            new HashHex(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant()),
            json,
            null,
            TransactionDataItemType.Permission);
    }

    public static PermissionGrant CreateMintNewTokenTypeGrant(string tokenNamespace, bool repeatable)
        => new(MintNewTokenTypeAction, NormalizeNamespace(tokenNamespace), repeatable);

    public static bool TryParseData(TransactionData? data, BusinessObjectRef? businessObject, out PermissionGrant grant)
    {
        grant = default;
        if (data?.InlineJson is null || businessObject is null)
        {
            return false;
        }

        if (data.DataHash is not null && data.DataHash != businessObject.DataHash)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(data.InlineJson);
            JsonElement root = document.RootElement;
            string action = ReadRequiredString(root, "act", "action");
            string tokenNamespace = ReadRequiredString(root, "ns", "namespace");
            bool repeatable = ReadBoolean(root, "rep", "repeatable");

            grant = new PermissionGrant(
                NormalizeAction(action),
                NormalizeNamespace(tokenNamespace),
                repeatable);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool IsPermissionToken(TokenTransfer? token)
        => token?.TokenId == PermissionTokenId;

    public static bool IsTokenInNamespace(TokenId tokenId, string tokenNamespace)
    {
        string normalizedNamespace = NormalizeNamespace(tokenNamespace);
        string tokenValue = tokenId.Value.Trim();
        return string.Equals(tokenValue, normalizedNamespace, StringComparison.Ordinal)
            || tokenValue.StartsWith($"{normalizedNamespace}.", StringComparison.Ordinal);
    }

    public static string NormalizeNamespace(string tokenNamespace)
    {
        string normalized = tokenNamespace.Trim();
        if (!TokenCreationAuthority.IsValidNamespace(normalized))
        {
            throw new ArgumentException("Permission namespace must contain non-empty dot-separated segments.", nameof(tokenNamespace));
        }

        return normalized;
    }

    public static string NormalizeAction(string action)
    {
        string normalized = action.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-')))
        {
            throw new ArgumentException("Permission action must contain only letters, digits, underscores, or hyphens.", nameof(action));
        }

        return normalized;
    }

    private static string ReadRequiredString(JsonElement root, params string[] names)
    {
        foreach (string name in names)
        {
            if (root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        throw new InvalidOperationException($"Permission data is missing '{names[0]}'.");
    }

    private static bool ReadBoolean(JsonElement root, params string[] names)
    {
        foreach (string name in names)
        {
            if (!root.TryGetProperty(name, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String
                && bool.TryParse(value.GetString(), out bool parsed))
            {
                return parsed;
            }
        }

        return false;
    }

    private sealed record PermissionGrantJson(
        [property: JsonPropertyName("act")] string Action,
        [property: JsonPropertyName("ns")] string Namespace,
        [property: JsonPropertyName("rep")] bool Repeatable);
}

public readonly record struct PermissionGrant(
    string Action,
    string Namespace,
    bool Repeatable);
