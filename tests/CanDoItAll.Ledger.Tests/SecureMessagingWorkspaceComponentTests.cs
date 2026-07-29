using System.Text;
using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Components.Samples;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class SecureMessagingWorkspaceComponentTests : BunitContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("secure-mail-ledger"));

    public SecureMessagingWorkspaceComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public async Task Mailbox_does_not_render_plaintext_until_message_is_opened()
    {
        ILedgerCryptoDriver driver = new DotNetLedgerCryptoDriver();
        EcdsaP256KeyPair aliceKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        EcdsaP256KeyPair bobKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        var alice = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "alice", aliceKey.PublicKeyMaterial, aliceKey.PrivateKeyMaterial);
        var bob = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "bob", bobKey.PublicKeyMaterial, bobKey.PrivateKeyMaterial);

        SecureLedgerMessageBuildResult built = await SecureLedgerMessages.CreateAsync(
            alice,
            bob.Address,
            bob.PublicKeyMaterial.ToArray(),
            "Hidden subject",
            "private body only after open",
            [],
            driver,
            uploadService: null,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            CancellationToken.None);
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace() with
        {
            Context = Context,
            AccountName = "Bob demo account",
            AccountAlias = "bob",
            Address = bob.Address,
            AddressBookmarks = [new LedgerAddressBookmark("Alice", alice.Address)],
            Transactions =
            [
                new LedgerTransactionSummary(
                    Context,
                    new TxId("tx-secure-message-1"),
                    TransactionKind.BusinessObject,
                    TransactionStatus.Confirmed,
                    built.Payload.CreatedAtUtc,
                    [alice.Address],
                    [bob.Address],
                    [],
                    [],
                    new TransactionData("application/json", Hex.Sha256Hex(Encoding.UTF8.GetBytes(built.StateJson)), built.StateJson, null, TransactionDataItemType.BusinessObject))
            ]
        };
        var openCalls = 0;

        IRenderedComponent<SecureMessagingWorkspace> component = Render<SecureMessagingWorkspace>(parameters => parameters
            .Add(x => x.Workspace, workspace)
            .Add(x => x.OpenRequested, (Func<SecureMessageOpenRequest, Task<SecureMessageOpenResult>>)(request =>
            {
                openCalls++;
                return Task.FromResult(SecureMessageOpenResult.Ok("Opened", built.PlainContent));
            })));

        Assert.Contains("Encrypted message", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Alice", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden subject", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private body only after open", component.Markup, StringComparison.Ordinal);

        await component.Find("[data-testid='secure-message-row']").ClickAsync(new MouseEventArgs());

        component.WaitForAssertion(() =>
        {
            Assert.Equal(1, openCalls);
            Assert.Contains("Hidden subject", component.Markup, StringComparison.Ordinal);
            Assert.Contains("private body only after open", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Decrypted_image_attachment_renders_as_image_with_download()
    {
        ILedgerCryptoDriver driver = new DotNetLedgerCryptoDriver();
        EcdsaP256KeyPair aliceKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        EcdsaP256KeyPair bobKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        var alice = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "alice", aliceKey.PublicKeyMaterial, aliceKey.PrivateKeyMaterial);
        var bob = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "bob", bobKey.PublicKeyMaterial, bobKey.PrivateKeyMaterial);
        byte[] imageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        var upload = new BusinessObjectDataItemUploadService(new AttachmentStorageDriver());

        SecureLedgerMessageBuildResult built = await SecureLedgerMessages.CreateAsync(
            alice,
            bob.Address,
            bob.PublicKeyMaterial.ToArray(),
            "Image attachment",
            "open the attached image",
            [
                new SecureLedgerMessageAttachmentSource(
                    new MemoryStream(imageBytes, writable: false),
                    "pixel.png",
                    "image/png",
                    imageBytes.LongLength)
            ],
            driver,
            upload,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            CancellationToken.None);
        SecureLedgerMessageAttachmentPlaintext plaintextAttachment = new(
            built.PlainContent.Attachments[0].AttachmentId,
            "pixel.png",
            "image/png",
            imageBytes);
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace() with
        {
            Context = Context,
            AccountName = "Bob demo account",
            AccountAlias = "bob",
            Address = bob.Address,
            AddressBookmarks = [new LedgerAddressBookmark("Alice", alice.Address)],
            Transactions =
            [
                new LedgerTransactionSummary(
                    Context,
                    new TxId("tx-secure-message-image"),
                    TransactionKind.BusinessObject,
                    TransactionStatus.Confirmed,
                    built.Payload.CreatedAtUtc,
                    [alice.Address],
                    [bob.Address],
                    [],
                    [],
                    new TransactionData("application/json", Hex.Sha256Hex(Encoding.UTF8.GetBytes(built.StateJson)), built.StateJson, null, TransactionDataItemType.BusinessObject))
            ]
        };

        IRenderedComponent<SecureMessagingWorkspace> component = Render<SecureMessagingWorkspace>(parameters => parameters
            .Add(x => x.Workspace, workspace)
            .Add(x => x.OpenRequested, (Func<SecureMessageOpenRequest, Task<SecureMessageOpenResult>>)(_ =>
                Task.FromResult(SecureMessageOpenResult.Ok("Opened", built.PlainContent))))
            .Add(x => x.AttachmentOpenRequested, (Func<SecureMessageAttachmentOpenRequest, Task<SecureMessageAttachmentOpenResult>>)(_ =>
                Task.FromResult(SecureMessageAttachmentOpenResult.Ok("Attachment opened", plaintextAttachment)))));

        await component.Find("[data-testid='secure-message-row']").ClickAsync(new MouseEventArgs());
        component.WaitForElement("[data-testid='secure-message-decrypt-attachment']");
        await component.Find("[data-testid='secure-message-decrypt-attachment']").ClickAsync(new MouseEventArgs());

        component.WaitForAssertion(() =>
        {
            IElement image = component.Find("[data-testid='secure-message-decrypted-image']");
            Assert.StartsWith("data:image/png;base64,", image.GetAttribute("src"), StringComparison.Ordinal);
            IElement download = component.Find("[data-testid='secure-message-download-attachment']");
            Assert.Equal("pixel.png", download.GetAttribute("download"));
            Assert.DoesNotContain("<textarea", component.Find("[data-testid='secure-message-decrypted-attachment']").InnerHtml, StringComparison.OrdinalIgnoreCase);
        });
    }

    private sealed class AttachmentStorageDriver : IBusinessObjectDataStorageDriver
    {
        public BusinessObjectDataStorageKind Storage => BusinessObjectDataStorageKind.Ipfs;

        public async Task<BusinessObjectStorageWriteResult> SaveAsync(
            BusinessObjectStorageWriteRequest request,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await request.Data.CopyToAsync(memory, cancellationToken);
            string hash = $"QmAttachment{memory.Length}";
            return new BusinessObjectStorageWriteResult(
                BusinessObjectDataStorageKind.Ipfs,
                hash,
                new Uri($"ipfs://{hash}"),
                request.FileName,
                request.ContentType,
                memory.Length);
        }

        public Task<BusinessObjectStorageReadResult> ReadAsync(
            string hashOrUri,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
