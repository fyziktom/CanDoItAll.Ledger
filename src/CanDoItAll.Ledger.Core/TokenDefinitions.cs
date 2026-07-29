namespace CanDoItAll.Ledger.Core;

public enum TokenClassification
{
    LegacyUnspecified = 0,
    Fungible = 1,
    BusinessObjectCarrier = 2
}

public sealed record TokenDefinition(
    int ProtocolVersion,
    string Namespace,
    string Symbol,
    Address Issuer,
    Amount InitialSupply,
    int DecimalPlaces,
    TokenClassification Classification);

public sealed record TokenDisplayMetadata(
    string? DisplayName,
    string? Description,
    string? ExternalRef);

public sealed record TokenDefinitionEnvelope(
    TokenDefinition Definition,
    TokenDisplayMetadata? DisplayMetadata = null);

public sealed record RegisteredTokenDefinition(
    TokenId TokenId,
    int ProtocolVersion,
    string? Namespace,
    string? Symbol,
    Address? Issuer,
    Amount? InitialSupply,
    int? DecimalPlaces,
    TokenClassification Classification,
    HashHex? DefinitionHash,
    TokenDisplayMetadata? DisplayMetadata,
    TxId CreatedTxId,
    int CreatedOutputIndex,
    DateTimeOffset CreatedAt);

public interface ITokenDefinitionCanonicalizer
{
    byte[] GetCanonicalBytes(TokenDefinition definition);
}

public interface ITokenDefinitionHasher
{
    HashHex CalculateDefinitionHash(TokenDefinition definition);

    TokenId CalculateTokenId(TokenDefinition definition);
}

public static class TokenDefinitionIdentity
{
    public const int CurrentProtocolVersion = 2;
    public const string V2TokenIdPrefix = "token:v2:";

    public static bool IsV2TokenId(TokenId tokenId)
        => tokenId.Value.Length == V2TokenIdPrefix.Length + 64
            && tokenId.Value.StartsWith(V2TokenIdPrefix, StringComparison.Ordinal)
            && LedgerValueValidation.IsValidHashHex(tokenId.Value[V2TokenIdPrefix.Length..]);
}
