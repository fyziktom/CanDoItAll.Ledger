using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record SecureLedgerMessageAttachmentSource(
    Stream Data,
    string FileName,
    string ContentType,
    long Size,
    string? Description = null);

public sealed record SecureLedgerMessageContent(
    string Subject,
    string Body,
    IReadOnlyList<SecureLedgerMessageContentAttachment> Attachments);

public sealed record SecureLedgerMessageContentAttachment(
    string AttachmentId,
    string FileName,
    string ContentType,
    long Size,
    string? Description = null,
    BusinessObjectDataItem? EncryptedDataItem = null,
    byte[]? Nonce = null,
    byte[]? AuthenticationTag = null,
    HashHex? CipherTextHash = null);

public sealed record SecureLedgerMessagePayload(
    string Format,
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    Address SenderAddress,
    Address ReceiverAddress,
    EconomyEncryptedMessage Content,
    IReadOnlyList<SecureLedgerMessageAttachment> Attachments);

public sealed record SecureLedgerMessageAttachment(
    string AttachmentId,
    BusinessObjectDataItem EncryptedDataItem,
    byte[] Nonce,
    byte[] AuthenticationTag,
    HashHex CipherTextHash);

public sealed record SecureLedgerMessageBuildResult(
    SecureLedgerMessagePayload Payload,
    SecureLedgerMessageContent PlainContent,
    string StateJson);

public sealed record SecureLedgerMessageAttachmentPlaintext(
    string AttachmentId,
    string FileName,
    string ContentType,
    byte[] Data,
    string? Description = null);

public static class SecureLedgerMessages
{
    public const string Format = "candoitall-secure-message-v1";
    public const string ObjectType = "secure-message";
    public const string SchemaVersion = "1.0";

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] AttachmentAadLabel = Encoding.UTF8.GetBytes("cdia.economy.secure-message.attachment.v1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static async Task<SecureLedgerMessageBuildResult> CreateAsync(
        IAccountKeySource senderKeySource,
        Address receiverAddress,
        byte[] receiverPublicKeyMaterial,
        string subject,
        string body,
        IReadOnlyList<SecureLedgerMessageAttachmentSource> attachments,
        IEconomyCryptoDriver cryptoDriver,
        IBusinessObjectDataItemUploadService? uploadService,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Message body cannot be empty.", nameof(body));
        }

        if (attachments.Count > 0 && uploadService is null)
        {
            throw new InvalidOperationException("Encrypted attachments require an IPFS upload service.");
        }

        var attachmentDescriptors = attachments
            .Select((attachment, index) => new SecureLedgerMessageContentAttachment(
                CreateAttachmentId(index),
                SafeFileName(attachment.FileName),
                SafeContentType(attachment.ContentType),
                attachment.Size,
                attachment.Description))
            .ToArray();
        IReadOnlyList<SecureLedgerMessageContentAttachment> encryptedAttachmentDescriptors = await EncryptAttachmentsAsync(
            senderKeySource,
            receiverAddress,
            receiverPublicKeyMaterial,
            attachments,
            attachmentDescriptors,
            cryptoDriver,
            uploadService,
            cancellationToken).ConfigureAwait(false);
        var plainContent = new SecureLedgerMessageContent(
            string.IsNullOrWhiteSpace(subject) ? "(no subject)" : subject.Trim(),
            body,
            encryptedAttachmentDescriptors);
        string contentJson = JsonSerializer.Serialize(plainContent, JsonOptions);

        EconomyEncryptedMessage encryptedContent = await AccountMessageSecurity
            .EncryptMessageAsync(
                senderKeySource,
                contentJson,
                receiverAddress,
                receiverPublicKeyMaterial,
                cryptoDriver,
                cancellationToken)
            .ConfigureAwait(false);

        var payload = new SecureLedgerMessagePayload(
            Format,
            SchemaVersion,
            createdAtUtc,
            senderKeySource.Address,
            receiverAddress,
            encryptedContent,
            []);
        string stateJson = JsonSerializer.Serialize(payload, JsonOptions);
        return new SecureLedgerMessageBuildResult(payload, plainContent, stateJson);
    }

    public static bool TryParsePayload(string? stateJson, out SecureLedgerMessagePayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return false;
        }

        try
        {
            SecureLedgerMessagePayload? parsed = JsonSerializer.Deserialize<SecureLedgerMessagePayload>(stateJson, JsonOptions);
            if (parsed?.Format != Format)
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryParseTransactionData(TransactionData? data, out SecureLedgerMessagePayload payload)
    {
        payload = default!;
        if (data?.InlineJson is null)
        {
            return false;
        }

        string? stateJson = TryReadPayloadEnvelope(data.InlineJson, out BusinessObjectPayloadEnvelope? envelope)
            && string.Equals(envelope.ObjectType, ObjectType, StringComparison.OrdinalIgnoreCase)
            ? envelope.StateJson
            : data.InlineJson;
        return TryParsePayload(stateJson, out payload);
    }

    public static async Task<SecureLedgerMessageContent> DecryptContentAsync(
        IAccountKeySource participantKeySource,
        SecureLedgerMessagePayload payload,
        IEconomyCryptoDriver cryptoDriver,
        CancellationToken cancellationToken = default)
    {
        ReadOnlyMemory<byte> privateKey = await participantKeySource
            .GetPrivateKeyMaterialAsync(cancellationToken)
            .ConfigureAwait(false);
        string contentJson = await EcdhP256MessageCipher
            .DecryptForParticipantAsync(
                cryptoDriver,
                participantKeySource.Address,
                privateKey,
                participantKeySource.PublicKeyMaterial,
                payload.Content,
                cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Deserialize<SecureLedgerMessageContent>(contentJson, JsonOptions)
            ?? throw new InvalidOperationException("Encrypted message content could not be parsed.");
    }

    public static async Task<SecureLedgerMessageAttachmentPlaintext> DecryptAttachmentAsync(
        IAccountKeySource participantKeySource,
        SecureLedgerMessagePayload payload,
        string attachmentId,
        SecureLedgerMessageContentAttachment descriptor,
        IEconomyCryptoDriver cryptoDriver,
        IBusinessObjectDataStorageDriver storageDriver,
        CancellationToken cancellationToken = default)
    {
        if (!descriptor.AttachmentId.Equals(attachmentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Attachment descriptor does not match the requested attachment id.");
        }

        BusinessObjectDataItem? encryptedDataItem = descriptor.EncryptedDataItem;
        byte[]? nonce = descriptor.Nonce;
        byte[]? authenticationTag = descriptor.AuthenticationTag;
        HashHex? cipherTextHash = descriptor.CipherTextHash;
        if (encryptedDataItem is null || nonce is null || authenticationTag is null || cipherTextHash is null)
        {
            SecureLedgerMessageAttachment legacyAttachment = payload.Attachments
                .FirstOrDefault(item => item.AttachmentId.Equals(attachmentId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Attachment was not found in the encrypted message payload.");
            encryptedDataItem = legacyAttachment.EncryptedDataItem;
            nonce = legacyAttachment.Nonce;
            authenticationTag = legacyAttachment.AuthenticationTag;
            cipherTextHash = legacyAttachment.CipherTextHash;
        }

        BusinessObjectStorageReadResult stored = await ReadStorageAsync(
            storageDriver,
            encryptedDataItem,
            cancellationToken).ConfigureAwait(false);
        await using Stream storedStream = stored.Data;
        using var memory = new MemoryStream();
        await storedStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        byte[] cipherText = memory.ToArray();
        if (Hex.Sha256Hex(cipherText) != cipherTextHash)
        {
            throw new InvalidOperationException("Encrypted attachment hash does not match the message descriptor.");
        }

        ReadOnlyMemory<byte> privateKey = await participantKeySource
            .GetPrivateKeyMaterialAsync(cancellationToken)
            .ConfigureAwait(false);
        if (participantKeySource.Address != payload.SenderAddress
            && participantKeySource.Address != payload.ReceiverAddress)
        {
            throw new InvalidOperationException("Encrypted attachment does not include the account as sender or receiver.");
        }

        byte[] peerPublicKey = participantKeySource.Address == payload.SenderAddress
            ? payload.Content.ReceiverPublicKeyMaterial
            : payload.Content.SenderPublicKeyMaterial;
        byte[] key = await cryptoDriver
            .DeriveEcdhP256Sha256KeyAsync(privateKey, peerPublicKey, EcdhP256MessageCipher.SharedSecretLabelBytes, cancellationToken)
            .ConfigureAwait(false);
        byte[] cipherTextWithTag = new byte[cipherText.Length + authenticationTag.Length];
        Buffer.BlockCopy(cipherText, 0, cipherTextWithTag, 0, cipherText.Length);
        Buffer.BlockCopy(authenticationTag, 0, cipherTextWithTag, cipherText.Length, authenticationTag.Length);
        byte[] plain = await cryptoDriver
            .AesGcmDecryptAsync(
                key,
                nonce,
                cipherTextWithTag,
                CreateAttachmentAssociatedData(payload, attachmentId),
                cancellationToken)
            .ConfigureAwait(false);

        return new SecureLedgerMessageAttachmentPlaintext(
            attachmentId,
            descriptor.FileName,
            descriptor.ContentType,
            plain,
            descriptor.Description);
    }

    private static async Task<IReadOnlyList<SecureLedgerMessageContentAttachment>> EncryptAttachmentsAsync(
        IAccountKeySource senderKeySource,
        Address receiverAddress,
        byte[] receiverPublicKeyMaterial,
        IReadOnlyList<SecureLedgerMessageAttachmentSource> attachments,
        IReadOnlyList<SecureLedgerMessageContentAttachment> descriptors,
        IEconomyCryptoDriver cryptoDriver,
        IBusinessObjectDataItemUploadService? uploadService,
        CancellationToken cancellationToken)
    {
        if (attachments.Count == 0)
        {
            return [];
        }

        ReadOnlyMemory<byte> privateKey = await senderKeySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        byte[] key = await cryptoDriver
            .DeriveEcdhP256Sha256KeyAsync(privateKey, receiverPublicKeyMaterial, EcdhP256MessageCipher.SharedSecretLabelBytes, cancellationToken)
            .ConfigureAwait(false);
        var result = new List<SecureLedgerMessageContentAttachment>(attachments.Count);
        HashHex senderPublicKeyHash = Address.CalculatePublicKeyHash(senderKeySource.PublicKeyMaterial.ToArray());
        HashHex receiverPublicKeyHash = Address.CalculatePublicKeyHash(receiverPublicKeyMaterial);

        for (int index = 0; index < attachments.Count; index++)
        {
            SecureLedgerMessageAttachmentSource attachment = attachments[index];
            SecureLedgerMessageContentAttachment descriptor = descriptors[index];
            using var plainMemory = new MemoryStream();
            await attachment.Data.CopyToAsync(plainMemory, cancellationToken).ConfigureAwait(false);
            byte[] nonce = await cryptoDriver.GetRandomBytesAsync(NonceSize, cancellationToken).ConfigureAwait(false);
            byte[] cipherTextWithTag = await cryptoDriver
                .AesGcmEncryptAsync(
                    key,
                    nonce,
                    plainMemory.ToArray(),
                    CreateAttachmentAssociatedData(
                        senderKeySource.Address,
                        receiverAddress,
                        senderPublicKeyHash,
                        receiverPublicKeyHash,
                        descriptor.AttachmentId),
                    cancellationToken)
                .ConfigureAwait(false);
            byte[] cipherText = cipherTextWithTag[..^TagSize];
            byte[] tag = cipherTextWithTag[^TagSize..];
            using var encryptedStream = new MemoryStream(cipherText, writable: false);
            BusinessObjectDataItem encryptedDataItem = await uploadService!.SaveToIpfsAsync(
                new BusinessObjectDataItemUploadRequest(
                    encryptedStream,
                    $"{descriptor.AttachmentId}.bin",
                    "application/octet-stream",
                    cipherText.Length,
                    IsMain: false,
                    Description: "Encrypted secure-message attachment"),
                cancellationToken).ConfigureAwait(false);
            result.Add(descriptor with
            {
                EncryptedDataItem = encryptedDataItem,
                Nonce = nonce,
                AuthenticationTag = tag,
                CipherTextHash = Hex.Sha256Hex(cipherText)
            });
        }

        return result;
    }

    private static async Task<BusinessObjectStorageReadResult> ReadStorageAsync(
        IBusinessObjectDataStorageDriver storageDriver,
        BusinessObjectDataItem item,
        CancellationToken cancellationToken)
        => await storageDriver.ReadAsync(
            item.Uri?.ToString() ?? item.Hash,
            cancellationToken).ConfigureAwait(false);

    private static byte[] CreateAttachmentAssociatedData(
        SecureLedgerMessagePayload payload,
        string attachmentId)
        => CreateAttachmentAssociatedData(
            payload.SenderAddress,
            payload.ReceiverAddress,
            payload.Content.SenderPublicKeyHash,
            payload.Content.ReceiverPublicKeyHash,
            attachmentId);

    private static byte[] CreateAttachmentAssociatedData(
        Address senderAddress,
        Address receiverAddress,
        HashHex senderPublicKeyHash,
        HashHex receiverPublicKeyHash,
        string attachmentId)
    {
        string value = string.Join('|',
            Encoding.UTF8.GetString(AttachmentAadLabel),
            senderAddress.Value,
            receiverAddress.Value,
            senderPublicKeyHash.Value,
            receiverPublicKeyHash.Value,
            attachmentId);
        return Encoding.UTF8.GetBytes(value);
    }

    private static bool TryReadPayloadEnvelope(
        string inlineJson,
        out BusinessObjectPayloadEnvelope envelope)
    {
        return BusinessObjectPayloadEnvelopeReader.TryRead(inlineJson, out envelope);
    }

    private static string CreateAttachmentId(int index)
        => $"att-{index + 1}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}";

    private static string SafeFileName(string fileName)
        => string.IsNullOrWhiteSpace(fileName) ? "attachment.bin" : fileName.Trim();

    private static string SafeContentType(string contentType)
        => string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
}
