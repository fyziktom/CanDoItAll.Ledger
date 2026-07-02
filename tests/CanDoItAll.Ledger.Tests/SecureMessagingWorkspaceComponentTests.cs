using System.Text;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class SecureMessagingWorkspaceComponentTests : TestContext
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
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
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
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace() with
        {
            Context = Context,
            AccountName = "Bob demo account",
            AccountAlias = "bob",
            Address = bob.Address,
            AddressBookmarks = [new EconomyAddressBookmark("Alice", alice.Address)],
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

        IRenderedComponent<SecureMessagingWorkspace> component = RenderComponent<SecureMessagingWorkspace>(parameters => parameters
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
}
