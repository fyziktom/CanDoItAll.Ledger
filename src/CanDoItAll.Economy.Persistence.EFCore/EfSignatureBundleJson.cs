using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Persistence.EFCore;

internal static class EfSignatureBundleJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static string Serialize(SignatureBundle bundle)
    {
        SignatureEnvelopeDto[] dto = bundle.Signatures
            .Select(x => new SignatureEnvelopeDto(
                (int)x.Algorithm,
                x.KeyId,
                x.PublicKeyHash.Value,
                x.PayloadHash.Value,
                Convert.ToBase64String(x.SignatureBytes),
                x.PublicKeyMaterial is null ? null : Convert.ToBase64String(x.PublicKeyMaterial)))
            .ToArray();

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static SignatureBundle Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SignatureBundle.Empty;
        }

        SignatureEnvelopeDto[]? dto = JsonSerializer.Deserialize<SignatureEnvelopeDto[]>(json, JsonOptions);
        if (dto is null || dto.Length == 0)
        {
            return SignatureBundle.Empty;
        }

        return new SignatureBundle(dto
            .Select(x => new SignatureEnvelope(
                (SignatureAlgorithmId)x.Algorithm,
                x.KeyId,
                new HashHex(x.PublicKeyHash),
                new HashHex(x.PayloadHash),
                Convert.FromBase64String(x.SignatureBase64),
                string.IsNullOrWhiteSpace(x.PublicKeyMaterialBase64) ? null : Convert.FromBase64String(x.PublicKeyMaterialBase64)))
            .ToArray());
    }

    private sealed record SignatureEnvelopeDto(
        int Algorithm,
        string KeyId,
        string PublicKeyHash,
        string PayloadHash,
        string SignatureBase64,
        string? PublicKeyMaterialBase64);
}
