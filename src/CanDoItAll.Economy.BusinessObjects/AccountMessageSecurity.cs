using System.Text;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.BusinessObjects;

public static class AccountMessageSecurity
{
    public static async Task<EconomyMessageSignature> SignMessageAsync(
        IAccountKeySource keySource,
        string message,
        CancellationToken cancellationToken)
        => await SignMessageAsync(keySource, message, SignatureProviderRegistry.CreateDefault(), cancellationToken).ConfigureAwait(false);

    public static async Task<EconomyMessageSignature> SignMessageAsync(
        IAccountKeySource keySource,
        string message,
        ISignatureProviderRegistry signatureProviderRegistry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be empty.", nameof(message));
        }

        HashHex payloadHash = CreateMessagePayloadHash(message);
        ISignatureProvider provider = signatureProviderRegistry.Get(keySource.Algorithm);
        ReadOnlyMemory<byte> privateKeyMaterial = await keySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        byte[] signature = provider.Sign(Encoding.UTF8.GetBytes(payloadHash.Value), privateKeyMaterial.Span);

        return new EconomyMessageSignature(
            keySource.Algorithm,
            keySource.KeyId,
            keySource.Address,
            keySource.PublicKeyHash,
            payloadHash,
            signature,
            keySource.PublicKeyMaterial.ToArray());
    }

    public static bool VerifyMessage(
        string message,
        EconomyMessageSignature signature,
        ISignatureProviderRegistry? signatureProviderRegistry = null)
    {
        if (string.IsNullOrEmpty(message) || signature.PublicKeyMaterial.Length == 0)
        {
            return false;
        }

        if (!signature.SignerAddress.MatchesPublicKey(signature.PublicKeyMaterial)
            || Address.CalculatePublicKeyHash(signature.PublicKeyMaterial) != signature.PublicKeyHash)
        {
            return false;
        }

        HashHex payloadHash = CreateMessagePayloadHash(message);
        if (signature.PayloadHash != payloadHash)
        {
            return false;
        }

        try
        {
            ISignatureProvider provider = (signatureProviderRegistry ?? SignatureProviderRegistry.CreateDefault()).Get(signature.Algorithm);
            return provider.Verify(Encoding.UTF8.GetBytes(payloadHash.Value), signature.SignatureBytes, signature.PublicKeyMaterial);
        }
        catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    public static async Task<HashHex> DeriveSharedSecretHashAsync(
        IAccountKeySource keySource,
        byte[] otherPublicKeyMaterial,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> privateKeyMaterial = await keySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        return EcdhP256MessageCipher.DeriveSharedSecretHash(privateKeyMaterial.Span, otherPublicKeyMaterial);
    }

    public static async Task<EconomyEncryptedMessage> EncryptMessageAsync(
        IAccountKeySource senderKeySource,
        string message,
        Address receiverAddress,
        byte[] receiverPublicKeyMaterial,
        CancellationToken cancellationToken)
    {
        if (!receiverAddress.MatchesPublicKey(receiverPublicKeyMaterial))
        {
            throw new InvalidOperationException("Receiver public key does not match the receiver address.");
        }

        ReadOnlyMemory<byte> privateKeyMaterial = await senderKeySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        return EcdhP256MessageCipher.Encrypt(
            senderKeySource.Address,
            privateKeyMaterial.Span,
            senderKeySource.PublicKeyMaterial.Span,
            receiverAddress,
            receiverPublicKeyMaterial,
            message);
    }

    public static async Task<string> DecryptMessageAsync(
        IAccountKeySource receiverKeySource,
        EconomyEncryptedMessage encryptedMessage,
        CancellationToken cancellationToken)
        => await DecryptMessageAsync(receiverKeySource, encryptedMessage, encryptedMessage.SenderPublicKeyMaterial, cancellationToken).ConfigureAwait(false);

    public static async Task<string> DecryptMessageAsync(
        IAccountKeySource receiverKeySource,
        EconomyEncryptedMessage encryptedMessage,
        byte[] senderPublicKeyMaterial,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> privateKeyMaterial = await receiverKeySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        return EcdhP256MessageCipher.Decrypt(
            receiverKeySource.Address,
            privateKeyMaterial.Span,
            receiverKeySource.PublicKeyMaterial.Span,
            encryptedMessage,
            senderPublicKeyMaterial);
    }

    private static HashHex CreateMessagePayloadHash(string message)
        => Hex.Sha256Hex(Encoding.UTF8.GetBytes(message));
}
