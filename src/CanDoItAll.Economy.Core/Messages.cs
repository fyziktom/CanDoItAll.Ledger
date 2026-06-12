namespace CanDoItAll.Economy.Core;

public sealed record EconomyMessageSignature(
    SignatureAlgorithmId Algorithm,
    string KeyId,
    Address SignerAddress,
    HashHex PublicKeyHash,
    HashHex PayloadHash,
    byte[] SignatureBytes,
    byte[] PublicKeyMaterial);

public sealed record EconomyEncryptedMessage(
    string Algorithm,
    Address SenderAddress,
    Address ReceiverAddress,
    HashHex SenderPublicKeyHash,
    HashHex ReceiverPublicKeyHash,
    byte[] SenderPublicKeyMaterial,
    byte[] ReceiverPublicKeyMaterial,
    byte[] Nonce,
    byte[] CipherText,
    byte[] AuthenticationTag);
