using System.Text;

namespace CanDoItAll.Ledger.Components;

public static class BusinessObjectDisplaySlug
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "business-object";
        }

        var builder = new StringBuilder(value.Length);
        bool previousWasSeparator = false;
        foreach (char character in value.Trim())
        {
            char normalized = NormalizeCharacter(character);
            bool isSeparator = normalized == '-';
            if (isSeparator && previousWasSeparator)
            {
                continue;
            }

            builder.Append(normalized);
            previousWasSeparator = isSeparator;
        }

        string result = builder.ToString().Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(result) ? "business-object" : result;
    }

    private static char NormalizeCharacter(char character)
    {
        if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
        {
            return character;
        }

        if (character is >= 'A' and <= 'Z')
        {
            return char.ToLowerInvariant(character);
        }

        return character is '.' or '_' ? character : '-';
    }
}
