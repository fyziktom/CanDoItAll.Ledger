using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Cryptography;

public static class Hex
{
    public static HashHex Sha256Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return new HashHex(Convert.ToHexString(hash).ToLowerInvariant());
    }

    public static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
