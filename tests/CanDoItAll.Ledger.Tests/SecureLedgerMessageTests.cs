using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class SecureLedgerMessageTests
{
    [Fact]
    public async Task Secure_message_encrypts_body_and_attachments_without_plaintext_ledger_state()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        EcdsaP256KeyPair aliceKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        EcdsaP256KeyPair bobKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        var alice = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "alice", aliceKey.PublicKeyMaterial, aliceKey.PrivateKeyMaterial);
        var bob = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "bob", bobKey.PublicKeyMaterial, bobKey.PrivateKeyMaterial);
        var storage = new CapturingStorageDriver();
        var upload = new BusinessObjectDataItemUploadService(storage);
        byte[] attachmentBytes = Encoding.UTF8.GetBytes("attachment plaintext must stay private");

        SecureLedgerMessageBuildResult built = await SecureLedgerMessages.CreateAsync(
            alice,
            bob.Address,
            bob.PublicKeyMaterial.ToArray(),
            "Secret project",
            "hello bob, private body",
            [
                new SecureLedgerMessageAttachmentSource(
                    new MemoryStream(attachmentBytes, writable: false),
                    "plan.txt",
                    "text/plain",
                    attachmentBytes.LongLength)
            ],
            driver,
            upload,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            CancellationToken.None);

        Assert.DoesNotContain("Secret project", built.StateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("hello bob", built.StateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("plan.txt", built.StateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("attachment plaintext", built.StateJson, StringComparison.Ordinal);
        Assert.Single(built.Payload.Attachments);
        Assert.DoesNotContain(attachmentBytes, storage.StoredPayloads, ByteArrayComparer.Instance);

        SecureLedgerMessageContent bobContent = await SecureLedgerMessages.DecryptContentAsync(
            bob,
            built.Payload,
            driver,
            CancellationToken.None);
        SecureLedgerMessageContent aliceContent = await SecureLedgerMessages.DecryptContentAsync(
            alice,
            built.Payload,
            driver,
            CancellationToken.None);

        Assert.Equal("Secret project", bobContent.Subject);
        Assert.Equal("hello bob, private body", bobContent.Body);
        Assert.Equal(bobContent.Subject, aliceContent.Subject);
        Assert.Equal(bobContent.Body, aliceContent.Body);
        Assert.Equal(bobContent.Attachments.Select(item => item.AttachmentId), aliceContent.Attachments.Select(item => item.AttachmentId));
        Assert.Single(bobContent.Attachments);
        Assert.Equal("plan.txt", bobContent.Attachments[0].FileName);

        SecureLedgerMessageAttachmentPlaintext bobAttachment = await SecureLedgerMessages.DecryptAttachmentAsync(
            bob,
            built.Payload,
            bobContent.Attachments[0].AttachmentId,
            bobContent.Attachments[0],
            driver,
            storage,
            CancellationToken.None);
        SecureLedgerMessageAttachmentPlaintext aliceAttachment = await SecureLedgerMessages.DecryptAttachmentAsync(
            alice,
            built.Payload,
            aliceContent.Attachments[0].AttachmentId,
            aliceContent.Attachments[0],
            driver,
            storage,
            CancellationToken.None);

        Assert.Equal(attachmentBytes, bobAttachment.Data);
        Assert.Equal(attachmentBytes, aliceAttachment.Data);
    }

    private sealed class CapturingStorageDriver : IBusinessObjectDataStorageDriver
    {
        private readonly Dictionary<string, byte[]> payloads = new(StringComparer.Ordinal);

        public BusinessObjectDataStorageKind Storage => BusinessObjectDataStorageKind.Ipfs;

        public IReadOnlyCollection<byte[]> StoredPayloads => payloads.Values;

        public async Task<BusinessObjectStorageWriteResult> SaveAsync(
            BusinessObjectStorageWriteRequest request,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await request.Data.CopyToAsync(memory, cancellationToken);
            byte[] bytes = memory.ToArray();
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            payloads[hash] = bytes;
            return new BusinessObjectStorageWriteResult(
                BusinessObjectDataStorageKind.Ipfs,
                hash,
                new Uri($"ipfs://{hash}"),
                request.FileName,
                request.ContentType,
                bytes.LongLength);
        }

        public Task<BusinessObjectStorageReadResult> ReadAsync(
            string hashOrUri,
            CancellationToken cancellationToken)
        {
            string hash = BusinessObjectStorageUri.GetHash(hashOrUri);
            byte[] bytes = payloads[hash];
            return Task.FromResult(new BusinessObjectStorageReadResult(
                new MemoryStream(bytes, writable: false),
                hash,
                "application/octet-stream"));
        }
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
            => x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(byte[] obj)
            => obj.Aggregate(17, (current, value) => (current * 31) + value);
    }
}
