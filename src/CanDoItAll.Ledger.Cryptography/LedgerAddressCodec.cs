using System.Numerics;
using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Cryptography;

public static class LedgerAddressCodec
{
    private const byte AddressVersion = 0x35;
    private const int PublicKeyHashLength = 20;
    private const int ChecksumLength = 4;
    private const string AddressPrefix = "cdia1";
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static async ValueTask<HashHex> CalculatePublicKeyHashAsync(
        ReadOnlyMemory<byte> publicKeyMaterial,
        ILedgerCryptoDriver cryptoDriver,
        CancellationToken cancellationToken = default)
    {
        EnsurePublicKeyMaterial(publicKeyMaterial);
        ArgumentNullException.ThrowIfNull(cryptoDriver);

        byte[] publicKeyHash = await cryptoDriver.Sha256Async(publicKeyMaterial, cancellationToken).ConfigureAwait(false);
        return new HashHex(Convert.ToHexString(publicKeyHash).ToLowerInvariant());
    }

    public static async ValueTask<Address> FromPublicKeyAsync(
        ReadOnlyMemory<byte> publicKeyMaterial,
        ILedgerCryptoDriver cryptoDriver,
        CancellationToken cancellationToken = default)
    {
        EnsurePublicKeyMaterial(publicKeyMaterial);
        ArgumentNullException.ThrowIfNull(cryptoDriver);

        byte[] publicKeyHash = await cryptoDriver.Sha256Async(publicKeyMaterial, cancellationToken).ConfigureAwait(false);
        byte[] body = new byte[1 + PublicKeyHashLength];
        body[0] = AddressVersion;
        Buffer.BlockCopy(publicKeyHash, 0, body, 1, PublicKeyHashLength);

        byte[] checksum = await CalculateChecksumAsync(body, cryptoDriver, cancellationToken).ConfigureAwait(false);
        byte[] addressBytes = new byte[body.Length + ChecksumLength];
        Buffer.BlockCopy(body, 0, addressBytes, 0, body.Length);
        Buffer.BlockCopy(checksum, 0, addressBytes, body.Length, ChecksumLength);
        return new Address(AddressPrefix + EncodeBase58(addressBytes));
    }

    private static async ValueTask<byte[]> CalculateChecksumAsync(
        byte[] body,
        ILedgerCryptoDriver cryptoDriver,
        CancellationToken cancellationToken)
    {
        byte[] first = await cryptoDriver.Sha256Async(body, cancellationToken).ConfigureAwait(false);
        byte[] second = await cryptoDriver.Sha256Async(first, cancellationToken).ConfigureAwait(false);
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

    private static void EnsurePublicKeyMaterial(ReadOnlyMemory<byte> publicKeyMaterial)
    {
        if (publicKeyMaterial.IsEmpty)
        {
            throw new ArgumentException("Public key material is required.", nameof(publicKeyMaterial));
        }
    }
}
