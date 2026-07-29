namespace CanDoItAll.Ledger.Core;

public static class TokenCreationAuthority
{
    public const string GrantLineageKeyPrefix = "token-authority:";

    public static BusinessObjectLineageKey CreateGrantLineageKey(string tokenNamespace)
        => new($"{GrantLineageKeyPrefix}{NormalizeNamespace(tokenNamespace)}");

    [Obsolete("This returns an internal legacy lineage marker, not a public BusinessObject id. Use CreateGrantLineageKey.")]
    public static BusinessObjectId CreateGrantObjectId(string tokenNamespace)
        => new(CreateGrantLineageKey(tokenNamespace).Value);

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
        if (businessObject is null || !businessObject.LineageKey.Value.StartsWith(GrantLineageKeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = businessObject.LineageKey.Value[GrantLineageKeyPrefix.Length..];
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
