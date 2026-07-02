using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public static class EcdhP256MessageCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] SharedSecretLabel = Encoding.UTF8.GetBytes("cdia.economy.ecdh.p256.v1");
    private static readonly byte[] MessageAadLabel = Encoding.UTF8.GetBytes("cdia.economy.message.aesgcm.v1");

    public static byte[] SharedSecretLabelBytes => SharedSecretLabel.ToArray();

    public static HashHex DeriveSharedSecretHash(
        ReadOnlySpan<byte> privateKeyMaterial,
        ReadOnlySpan<byte> otherPublicKeyMaterial)
    {
        byte[] key = DeriveSharedKey(privateKeyMaterial, otherPublicKeyMaterial);
        return Hex.Sha256Hex(key);
    }

    public static async ValueTask<HashHex> DeriveSharedSecretHashAsync(
        IEconomyCryptoDriver cryptoDriver,
        ReadOnlyMemory<byte> privateKeyMaterial,
        ReadOnlyMemory<byte> otherPublicKeyMaterial,
        CancellationToken cancellationToken = default)
    {
        byte[] key = await cryptoDriver
            .DeriveEcdhP256Sha256KeyAsync(privateKeyMaterial, otherPublicKeyMaterial, SharedSecretLabel, cancellationToken)
            .ConfigureAwait(false);
        return Hex.Sha256Hex(key);
    }

    public static EconomyEncryptedMessage Encrypt(
        Address senderAddress,
        ReadOnlySpan<byte> senderPrivateKeyMaterial,
        ReadOnlySpan<byte> senderPublicKeyMaterial,
        Address receiverAddress,
        ReadOnlySpan<byte> receiverPublicKeyMaterial,
        string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be empty.", nameof(message));
        }

        byte[] key = DeriveSharedKey(senderPrivateKeyMaterial, receiverPublicKeyMaterial);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] plainText = Encoding.UTF8.GetBytes(message);
        byte[] cipherText = new byte[plainText.Length];
        byte[] tag = new byte[TagSize];
        byte[] senderPublicKey = senderPublicKeyMaterial.ToArray();
        byte[] receiverPublicKey = receiverPublicKeyMaterial.ToArray();
        byte[] aad = CreateAssociatedData(senderAddress, receiverAddress, senderPublicKey, receiverPublicKey);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainText, cipherText, tag, aad);

        return new EconomyEncryptedMessage(
            "ECDH-P256+A256GCM",
            senderAddress,
            receiverAddress,
            Address.CalculatePublicKeyHash(senderPublicKey),
            Address.CalculatePublicKeyHash(receiverPublicKey),
            senderPublicKey,
            receiverPublicKey,
            nonce,
            cipherText,
            tag);
    }

    public static async ValueTask<EconomyEncryptedMessage> EncryptAsync(
        IEconomyCryptoDriver cryptoDriver,
        Address senderAddress,
        ReadOnlyMemory<byte> senderPrivateKeyMaterial,
        ReadOnlyMemory<byte> senderPublicKeyMaterial,
        Address receiverAddress,
        ReadOnlyMemory<byte> receiverPublicKeyMaterial,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be empty.", nameof(message));
        }

        byte[] key = await cryptoDriver
            .DeriveEcdhP256Sha256KeyAsync(senderPrivateKeyMaterial, receiverPublicKeyMaterial, SharedSecretLabel, cancellationToken)
            .ConfigureAwait(false);
        byte[] nonce = await cryptoDriver.GetRandomBytesAsync(NonceSize, cancellationToken).ConfigureAwait(false);
        byte[] plainText = Encoding.UTF8.GetBytes(message);
        byte[] senderPublicKey = senderPublicKeyMaterial.ToArray();
        byte[] receiverPublicKey = receiverPublicKeyMaterial.ToArray();
        byte[] aad = CreateAssociatedData(senderAddress, receiverAddress, senderPublicKey, receiverPublicKey);
        byte[] ciphertextWithTag = await cryptoDriver
            .AesGcmEncryptAsync(key, nonce, plainText, aad, cancellationToken)
            .ConfigureAwait(false);
        byte[] cipherText = ciphertextWithTag[..^TagSize];
        byte[] tag = ciphertextWithTag[^TagSize..];

        return new EconomyEncryptedMessage(
            "ECDH-P256+A256GCM",
            senderAddress,
            receiverAddress,
            Address.CalculatePublicKeyHash(senderPublicKey),
            Address.CalculatePublicKeyHash(receiverPublicKey),
            senderPublicKey,
            receiverPublicKey,
            nonce,
            cipherText,
            tag);
    }

    public static string Decrypt(
        Address receiverAddress,
        ReadOnlySpan<byte> receiverPrivateKeyMaterial,
        ReadOnlySpan<byte> receiverPublicKeyMaterial,
        EconomyEncryptedMessage encryptedMessage,
        ReadOnlySpan<byte> senderPublicKeyMaterial)
    {
        if (encryptedMessage.ReceiverAddress != receiverAddress)
        {
            throw new InvalidOperationException("Encrypted message receiver does not match the account address.");
        }

        byte[] senderPublicKey = senderPublicKeyMaterial.ToArray();
        byte[] receiverPublicKey = receiverPublicKeyMaterial.ToArray();
        if (Address.CalculatePublicKeyHash(senderPublicKey) != encryptedMessage.SenderPublicKeyHash)
        {
            throw new InvalidOperationException("Sender public key does not match the encrypted message metadata.");
        }

        if (Address.CalculatePublicKeyHash(receiverPublicKey) != encryptedMessage.ReceiverPublicKeyHash)
        {
            throw new InvalidOperationException("Receiver public key does not match the encrypted message metadata.");
        }

        byte[] key = DeriveSharedKey(receiverPrivateKeyMaterial, senderPublicKey);
        byte[] plainText = new byte[encryptedMessage.CipherText.Length];
        byte[] aad = CreateAssociatedData(encryptedMessage.SenderAddress, encryptedMessage.ReceiverAddress, senderPublicKey, receiverPublicKey);

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(encryptedMessage.Nonce, encryptedMessage.CipherText, encryptedMessage.AuthenticationTag, plainText, aad);
        return Encoding.UTF8.GetString(plainText);
    }

    public static async ValueTask<string> DecryptAsync(
        IEconomyCryptoDriver cryptoDriver,
        Address receiverAddress,
        ReadOnlyMemory<byte> receiverPrivateKeyMaterial,
        ReadOnlyMemory<byte> receiverPublicKeyMaterial,
        EconomyEncryptedMessage encryptedMessage,
        ReadOnlyMemory<byte> senderPublicKeyMaterial,
        CancellationToken cancellationToken = default)
    {
        if (encryptedMessage.ReceiverAddress != receiverAddress)
        {
            throw new InvalidOperationException("Encrypted message receiver does not match the account address.");
        }

        byte[] senderPublicKey = senderPublicKeyMaterial.ToArray();
        byte[] receiverPublicKey = receiverPublicKeyMaterial.ToArray();
        if (Address.CalculatePublicKeyHash(senderPublicKey) != encryptedMessage.SenderPublicKeyHash)
        {
            throw new InvalidOperationException("Sender public key does not match the encrypted message metadata.");
        }

        if (Address.CalculatePublicKeyHash(receiverPublicKey) != encryptedMessage.ReceiverPublicKeyHash)
        {
            throw new InvalidOperationException("Receiver public key does not match the encrypted message metadata.");
        }

        byte[] key = await cryptoDriver
            .DeriveEcdhP256Sha256KeyAsync(receiverPrivateKeyMaterial, senderPublicKey, SharedSecretLabel, cancellationToken)
            .ConfigureAwait(false);
        byte[] ciphertextWithTag = new byte[encryptedMessage.CipherText.Length + encryptedMessage.AuthenticationTag.Length];
        Buffer.BlockCopy(encryptedMessage.CipherText, 0, ciphertextWithTag, 0, encryptedMessage.CipherText.Length);
        Buffer.BlockCopy(encryptedMessage.AuthenticationTag, 0, ciphertextWithTag, encryptedMessage.CipherText.Length, encryptedMessage.AuthenticationTag.Length);
        byte[] aad = CreateAssociatedData(encryptedMessage.SenderAddress, encryptedMessage.ReceiverAddress, senderPublicKey, receiverPublicKey);
        byte[] plainText = await cryptoDriver
            .AesGcmDecryptAsync(key, encryptedMessage.Nonce, ciphertextWithTag, aad, cancellationToken)
            .ConfigureAwait(false);
        return Encoding.UTF8.GetString(plainText);
    }

    public static async ValueTask<string> DecryptForParticipantAsync(
        IEconomyCryptoDriver cryptoDriver,
        Address participantAddress,
        ReadOnlyMemory<byte> participantPrivateKeyMaterial,
        ReadOnlyMemory<byte> participantPublicKeyMaterial,
        EconomyEncryptedMessage encryptedMessage,
        CancellationToken cancellationToken = default)
    {
        bool participantIsSender = encryptedMessage.SenderAddress == participantAddress;
        bool participantIsReceiver = encryptedMessage.ReceiverAddress == participantAddress;
        if (!participantIsSender && !participantIsReceiver)
        {
            throw new InvalidOperationException("Encrypted message does not include the account as sender or receiver.");
        }

        byte[] senderPublicKey = encryptedMessage.SenderPublicKeyMaterial;
        byte[] receiverPublicKey = encryptedMessage.ReceiverPublicKeyMaterial;
        byte[] participantPublicKey = participantPublicKeyMaterial.ToArray();
        HashHex participantPublicKeyHash = Address.CalculatePublicKeyHash(participantPublicKey);

        if (participantIsSender && participantPublicKeyHash != encryptedMessage.SenderPublicKeyHash)
        {
            throw new InvalidOperationException("Participant public key does not match the encrypted message sender metadata.");
        }

        if (participantIsReceiver && participantPublicKeyHash != encryptedMessage.ReceiverPublicKeyHash)
        {
            throw new InvalidOperationException("Participant public key does not match the encrypted message receiver metadata.");
        }

        byte[] peerPublicKey = participantIsSender ? receiverPublicKey : senderPublicKey;
        byte[] key = await cryptoDriver
            .DeriveEcdhP256Sha256KeyAsync(participantPrivateKeyMaterial, peerPublicKey, SharedSecretLabel, cancellationToken)
            .ConfigureAwait(false);
        byte[] ciphertextWithTag = new byte[encryptedMessage.CipherText.Length + encryptedMessage.AuthenticationTag.Length];
        Buffer.BlockCopy(encryptedMessage.CipherText, 0, ciphertextWithTag, 0, encryptedMessage.CipherText.Length);
        Buffer.BlockCopy(encryptedMessage.AuthenticationTag, 0, ciphertextWithTag, encryptedMessage.CipherText.Length, encryptedMessage.AuthenticationTag.Length);
        byte[] aad = CreateAssociatedData(encryptedMessage.SenderAddress, encryptedMessage.ReceiverAddress, senderPublicKey, receiverPublicKey);
        byte[] plainText = await cryptoDriver
            .AesGcmDecryptAsync(key, encryptedMessage.Nonce, ciphertextWithTag, aad, cancellationToken)
            .ConfigureAwait(false);
        return Encoding.UTF8.GetString(plainText);
    }

    private static byte[] DeriveSharedKey(ReadOnlySpan<byte> privateKeyMaterial, ReadOnlySpan<byte> otherPublicKeyMaterial)
    {
        using ECDiffieHellman privateKey = EcdsaP256KeyMaterial.CreateAgreementPrivateKey(privateKeyMaterial);
        using ECDiffieHellman publicKey = EcdsaP256KeyMaterial.CreateAgreementPublicKey(otherPublicKeyMaterial);
        return privateKey.DeriveKeyFromHash(publicKey.PublicKey, HashAlgorithmName.SHA256, null, SharedSecretLabel);
    }

    private static byte[] CreateAssociatedData(
        Address senderAddress,
        Address receiverAddress,
        byte[] senderPublicKeyMaterial,
        byte[] receiverPublicKeyMaterial)
    {
        string value = string.Join('|',
            Encoding.UTF8.GetString(MessageAadLabel),
            senderAddress.Value,
            receiverAddress.Value,
            Address.CalculatePublicKeyHash(senderPublicKeyMaterial).Value,
            Address.CalculatePublicKeyHash(receiverPublicKeyMaterial).Value);

        return Encoding.UTF8.GetBytes(value);
    }
}
