using System.Globalization;

namespace CanDoItAll.Ledger.Core;

public static class LedgerValidationLimits
{
    public const int MaxLedgerIdLength = 64;
    public const int MaxScenarioIdLength = 64;
    public const int MaxTxIdLength = 128;
    public const int MaxTokenIdLength = 128;
    public const int MaxBusinessObjectIdLength = 128;
    public const int MaxHashHexLength = 128;
    public const int MaxKeyIdLength = 128;
    public const int MaxAlgorithmLength = 64;
    public const int MaxTransactionInputs = 256;
    public const int MaxTransactionOutputs = 256;
    public const int MaxSignaturesPerInput = 8;
    public const int MaxSignatureBytes = 8192;
    public const int MaxPublicKeyMaterialBytes = 8192;
    public const int MaxTransactionSignatureBytes = 1_048_576;
    public const int MaxTransactionPublicKeyMaterialBytes = 1_048_576;
    public const int MaxInlineJsonBytes = 262_144;
    public const int MaxExternalRefLength = 2048;
    public const int MaxContentTypeLength = 128;
}

public static class LedgerValueValidation
{
    private const string AllowedIdentifierCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._:-";

    public static bool IsValidIdentifier(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length != value.Length || trimmed.Length > maxLength)
        {
            return false;
        }

        foreach (char character in trimmed)
        {
            if (AllowedIdentifierCharacters.IndexOf(character, StringComparison.Ordinal) < 0)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidHashHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length != value.Length
            || trimmed.Length > LedgerValidationLimits.MaxHashHexLength
            || trimmed.Length % 2 != 0)
        {
            return false;
        }

        foreach (char character in trimmed)
        {
            bool isHex =
                character is >= '0' and <= '9'
                || character is >= 'a' and <= 'f'
                || character is >= 'A' and <= 'F';
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsSafeText(string? value, int maxLength, bool allowEmpty = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return allowEmpty;
        }

        if (value.Length > maxLength || value.Length != value.Trim().Length)
        {
            return false;
        }

        foreach (char character in value)
        {
            UnicodeCategory category = char.GetUnicodeCategory(character);
            if (category is UnicodeCategory.Control or UnicodeCategory.OtherNotAssigned)
            {
                return false;
            }
        }

        return true;
    }
}
