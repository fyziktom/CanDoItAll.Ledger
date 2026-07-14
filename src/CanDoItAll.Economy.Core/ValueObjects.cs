using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Economy.Core;

public readonly record struct LedgerId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ScenarioId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct TxId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct Address(string Value)
{
    private const byte AddressVersion = 0x35;
    private const int PublicKeyHashLength = 20;
    private const int ChecksumLength = 4;
    private const string AddressPrefix = "cdia1";
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const int EncodedAddressByteLength = 1 + PublicKeyHashLength + ChecksumLength;

    public static Address FromPublicKey(ReadOnlySpan<byte> publicKeyMaterial)
    {
        byte[] body = BuildAddressBody(publicKeyMaterial);
        byte[] checksum = CalculateChecksum(body);
        byte[] addressBytes = new byte[body.Length + ChecksumLength];
        Buffer.BlockCopy(body, 0, addressBytes, 0, body.Length);
        Buffer.BlockCopy(checksum, 0, addressBytes, body.Length, ChecksumLength);
        return new Address(AddressPrefix + EncodeBase58(addressBytes));
    }

    public static HashHex CalculatePublicKeyHash(ReadOnlySpan<byte> publicKeyMaterial)
    {
        EnsurePublicKeyMaterial(publicKeyMaterial);
        byte[] hash = SHA256.HashData(publicKeyMaterial);
        return new HashHex(Convert.ToHexString(hash).ToLowerInvariant());
    }

    public static bool IsValid(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out Address address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != value.Trim().Length)
        {
            return false;
        }

        if (!value.StartsWith(AddressPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string encoded = value[AddressPrefix.Length..];
        if (encoded.Length == 0 || !TryDecodeBase58(encoded, out byte[] decoded))
        {
            return false;
        }

        if (decoded.Length != EncodedAddressByteLength || decoded[0] != AddressVersion)
        {
            return false;
        }

        byte[] body = decoded[..^ChecksumLength];
        byte[] expectedChecksum = CalculateChecksum(body);
        ReadOnlySpan<byte> actualChecksum = decoded.AsSpan(decoded.Length - ChecksumLength, ChecksumLength);
        if (!actualChecksum.SequenceEqual(expectedChecksum))
        {
            return false;
        }

        address = new Address(value);
        return true;
    }

    public bool MatchesPublicKey(ReadOnlySpan<byte> publicKeyMaterial)
        => Equals(FromPublicKey(publicKeyMaterial));

    public override string ToString() => Value;

    private static byte[] BuildAddressBody(ReadOnlySpan<byte> publicKeyMaterial)
    {
        EnsurePublicKeyMaterial(publicKeyMaterial);
        byte[] publicKeyHash = SHA256.HashData(publicKeyMaterial);
        byte[] body = new byte[1 + PublicKeyHashLength];
        body[0] = AddressVersion;
        Buffer.BlockCopy(publicKeyHash, 0, body, 1, PublicKeyHashLength);
        return body;
    }

    private static byte[] CalculateChecksum(byte[] body)
    {
        byte[] first = SHA256.HashData(body);
        byte[] second = SHA256.HashData(first);
        byte[] checksum = new byte[ChecksumLength];
        Buffer.BlockCopy(second, 0, checksum, 0, ChecksumLength);
        return checksum;
    }

    private static string EncodeBase58(ReadOnlySpan<byte> data)
    {
        var value = new BigInteger(data, isUnsigned: true, isBigEndian: true);
        var builder = new StringBuilder();

        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, 58, out BigInteger remainder);
            builder.Insert(0, Base58Alphabet[(int)remainder]);
        }

        foreach (byte item in data)
        {
            if (item != 0)
            {
                break;
            }

            builder.Insert(0, Base58Alphabet[0]);
        }

        return builder.ToString();
    }

    private static bool TryDecodeBase58(string encoded, out byte[] data)
    {
        data = [];
        var value = BigInteger.Zero;

        foreach (char character in encoded)
        {
            int index = Base58Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            value = (value * 58) + index;
        }

        byte[] decoded = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        int leadingZeroCount = encoded.TakeWhile(x => x == Base58Alphabet[0]).Count();
        if (leadingZeroCount > 0)
        {
            byte[] withLeadingZeros = new byte[leadingZeroCount + decoded.Length];
            Buffer.BlockCopy(decoded, 0, withLeadingZeros, leadingZeroCount, decoded.Length);
            decoded = withLeadingZeros;
        }

        data = decoded;
        return true;
    }

    private static void EnsurePublicKeyMaterial(ReadOnlySpan<byte> publicKeyMaterial)
    {
        if (publicKeyMaterial.IsEmpty)
        {
            throw new ArgumentException("Public key material is required.", nameof(publicKeyMaterial));
        }
    }
}

public readonly record struct TokenId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct BusinessObjectId(string Value)
{
    public static BusinessObjectId FromOutPoint(TxId txId, int outputIndex)
        => FromOutPoint(new OutPoint(txId, outputIndex));

    public static BusinessObjectId FromOutPoint(OutPoint outPoint)
    {
        ArgumentNullException.ThrowIfNull(outPoint);
        if (string.IsNullOrWhiteSpace(outPoint.TxId.Value))
        {
            throw new ArgumentException("BusinessObject outpoint transaction id is required.", nameof(outPoint));
        }

        if (outPoint.OutputIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outPoint), "BusinessObject output index must be non-negative.");
        }

        return new BusinessObjectId(outPoint.ToString());
    }

    public bool TryGetOutPoint(out OutPoint outPoint)
    {
        outPoint = default!;
        if (string.IsNullOrWhiteSpace(Value))
        {
            return false;
        }

        int separator = Value.LastIndexOf(':');
        if (separator <= 0
            || separator == Value.Length - 1
            || !int.TryParse(
                Value.AsSpan(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int outputIndex)
            || outputIndex < 0)
        {
            return false;
        }

        string txId = Value[..separator];
        if (string.IsNullOrWhiteSpace(txId))
        {
            return false;
        }

        outPoint = new OutPoint(new TxId(txId), outputIndex);
        return true;
    }

    public OutPoint RequireOutPoint()
        => TryGetOutPoint(out OutPoint outPoint)
            ? outPoint
            : throw new FormatException($"BusinessObject id must be a UTXO id in 'txid:outputIndex' format: '{Value}'.");

    public bool IsCanonicalOutPointId()
        => TryGetOutPoint(out OutPoint outPoint)
            && outPoint.OutputIndex < LedgerValidationLimits.MaxTransactionOutputs
            && outPoint.TxId.Value.Length == 64
            && LedgerValueValidation.IsValidHashHex(outPoint.TxId.Value);

    public override string ToString() => Value;
}

/// <summary>
/// Stable correlation key serialized by the V1/V2 BusinessObject protocol.
/// This is internal lineage state, not the public BusinessObject id.
/// </summary>
public readonly record struct BusinessObjectLineageKey(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct HashHex(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct Amount(decimal Value)
{
    public static Amount Zero { get; } = new(0m);

    public static Amount FromInt64(long value) => new(value);

    public static Amount FromDecimal(decimal value) => new(value);

    public void EnsureNonNegative(string fieldName)
    {
        if (Value < 0m)
        {
            throw new ArgumentOutOfRangeException(fieldName, "Amount must be non-negative.");
        }
    }

    public override string ToString() => Value.ToString("0.#############################", CultureInfo.InvariantCulture);
}

public sealed record OutPoint(TxId TxId, int OutputIndex)
{
    public override string ToString() => $"{TxId}:{OutputIndex}";
}

public readonly record struct OutPointKey(HashHex Value)
{
    public override string ToString() => Value.Value;
}
