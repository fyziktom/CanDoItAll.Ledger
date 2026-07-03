using System.Globalization;
using System.Text;
using System.Threading;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components;

public static class BusinessObjectCreateIdFactory
{
    private const int MaxObjectTypeSegmentLength = 32;
    private const int GuidSegmentLength = 12;
    private static long sequence;

    public static BusinessObjectId CreateNewObjectId(string objectType, string displayName)
    {
        string typeSegment = TrimSegment(NormalizeName(objectType), MaxObjectTypeSegmentLength, "business-object");
        string nameSegment = NormalizeName(displayName);
        string nonce = CreateNonce();
        string prefix = $"bo:{typeSegment}:";
        int maxNameLength = LedgerValidationLimits.MaxBusinessObjectIdLength - prefix.Length - nonce.Length - 1;
        nameSegment = TrimSegment(nameSegment, Math.Max(1, maxNameLength), "business-object");

        string value = $"{prefix}{nameSegment}:{nonce}";
        if (!LedgerValueValidation.IsValidIdentifier(value, LedgerValidationLimits.MaxBusinessObjectIdLength))
        {
            throw new InvalidOperationException($"Generated BusinessObject id is not valid: {value}.");
        }

        return new BusinessObjectId(value);
    }

    public static BusinessObjectId CreateOpaqueObjectId(string objectType)
    {
        string typeSegment = TrimSegment(NormalizeName(objectType), MaxObjectTypeSegmentLength, "business-object");
        string value = $"bo:{typeSegment}:{CreateNonce()}";
        if (!LedgerValueValidation.IsValidIdentifier(value, LedgerValidationLimits.MaxBusinessObjectIdLength))
        {
            throw new InvalidOperationException($"Generated BusinessObject id is not valid: {value}.");
        }

        return new BusinessObjectId(value);
    }

    public static string NormalizeName(string value)
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

    private static string TrimSegment(string value, int maxLength, string fallback)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        string trimmed = value[..maxLength].Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static string CreateNonce()
    {
        long ticks = DateTime.UtcNow.Ticks;
        long count = Interlocked.Increment(ref sequence) & 0xFFFF;
        string guidSegment = Guid.NewGuid().ToString("N")[..GuidSegmentLength].ToLowerInvariant();
        return string.Concat(
            ticks.ToString("x", CultureInfo.InvariantCulture),
            "-",
            count.ToString("x4", CultureInfo.InvariantCulture),
            "-",
            guidSegment);
    }
}
