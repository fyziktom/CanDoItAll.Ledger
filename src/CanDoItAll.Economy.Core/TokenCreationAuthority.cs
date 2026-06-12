namespace CanDoItAll.Economy.Core;

public static class TokenCreationAuthority
{
    public const string GrantObjectIdPrefix = "token-authority:";

    public static BusinessObjectId CreateGrantObjectId(string tokenNamespace)
        => new($"{GrantObjectIdPrefix}{NormalizeNamespace(tokenNamespace)}");

    public static string NormalizeNamespace(string tokenNamespace)
    {
        string normalized = tokenNamespace.Trim();
        if (!IsValidNamespace(normalized))
        {
            throw new ArgumentException("Token namespace must contain non-empty dot-separated segments.", nameof(tokenNamespace));
        }

        return normalized;
    }

    public static bool TryGetGrantNamespace(BusinessObjectRef? businessObject, out string tokenNamespace)
    {
        tokenNamespace = string.Empty;
        if (businessObject is null || !businessObject.ObjectId.Value.StartsWith(GrantObjectIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = businessObject.ObjectId.Value[GrantObjectIdPrefix.Length..];
        if (!IsValidNamespace(candidate))
        {
            return false;
        }

        tokenNamespace = candidate;
        return true;
    }

    public static bool IsTokenInNamespace(TokenId tokenId, string tokenNamespace)
    {
        string normalizedNamespace = NormalizeNamespace(tokenNamespace);
        string tokenValue = tokenId.Value.Trim();
        return string.Equals(tokenValue, normalizedNamespace, StringComparison.Ordinal)
            || tokenValue.StartsWith($"{normalizedNamespace}.", StringComparison.Ordinal);
    }

    public static bool IsValidNamespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] segments = value.Split('.');
        return segments.All(segment => segment.Length > 0 && segment.All(IsAllowedSegmentCharacter));
    }

    private static bool IsAllowedSegmentCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '_' or '-';
}
