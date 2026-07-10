using System.Text.Json;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public sealed class DefaultTokenDefinitionCanonicalizer : ITokenDefinitionCanonicalizer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public byte[] GetCanonicalBytes(TokenDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var dto = new
        {
            Domain = "cdia.economy.token-definition.v2",
            definition.ProtocolVersion,
            definition.Namespace,
            definition.Symbol,
            Issuer = definition.Issuer.Value,
            InitialSupply = definition.InitialSupply.ToString(),
            definition.DecimalPlaces,
            Classification = definition.Classification.ToString()
        };
        return JsonSerializer.SerializeToUtf8Bytes(dto, Options);
    }
}

public sealed class TokenDefinitionHasher(ITokenDefinitionCanonicalizer canonicalizer) : ITokenDefinitionHasher
{
    public HashHex CalculateDefinitionHash(TokenDefinition definition)
        => Hex.Sha256Hex(canonicalizer.GetCanonicalBytes(definition));

    public TokenId CalculateTokenId(TokenDefinition definition)
        => new(TokenDefinitionIdentity.V2TokenIdPrefix + CalculateDefinitionHash(definition).Value);
}
