using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectAccountBackupTests
{
    [Fact]
    public async Task AccountBackup_ExportsEncryptedJsonAndImportsKey()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-06T18:00:00Z"));
        var service = new AccountBackupService(time);
        IAccountKeySource source = CreateKeySource("demo-key");

        AccountBackupDocument document = await service.ExportAsync(
            source,
            new AccountBackupExportRequest("Demo account", "strong-password", "backup note"),
            CancellationToken.None);

        Assert.Equal(AccountBackupService.PasswordProtection, document.PrivateKeyProtection);
        Assert.NotNull(document.Salt);
        Assert.NotNull(document.Nonce);
        Assert.NotNull(document.Tag);

        string json = service.Serialize(document);
        AccountBackupDocument restoredDocument = service.Deserialize(json);
        IAccountKeySource restored = service.Import(new AccountBackupImportRequest(restoredDocument, "strong-password"));

        Assert.Equal(source.Address, restored.Address);
        Assert.Equal(source.Algorithm, restored.Algorithm);
        Assert.Equal(source.KeyId, restored.KeyId);
        Assert.Equal(source.PublicKeyHash, restored.PublicKeyHash);
        Assert.Equal(source.PublicKeyMaterial.ToArray(), restored.PublicKeyMaterial.ToArray());
        Assert.Equal(
            (await source.GetPrivateKeyMaterialAsync(CancellationToken.None)).ToArray(),
            (await restored.GetPrivateKeyMaterialAsync(CancellationToken.None)).ToArray());
        Assert.Equal(Address.FromPublicKey(source.PublicKeyMaterial.Span), restored.Address);
    }

    [Fact]
    public async Task AccountBackup_RequiresPasswordForEncryptedBackup()
    {
        var service = new AccountBackupService(new ManualTimeProvider(DateTimeOffset.Parse("2026-05-06T18:00:00Z")));
        IAccountKeySource source = CreateKeySource("demo-key");

        AccountBackupDocument document = await service.ExportAsync(
            source,
            new AccountBackupExportRequest("Demo account", "strong-password"),
            CancellationToken.None);

        Assert.Throws<InvalidOperationException>(() => service.Import(new AccountBackupImportRequest(document)));
    }

    [Fact]
    public async Task AccountBackup_RejectsForgedAddress()
    {
        var service = new AccountBackupService(new ManualTimeProvider(DateTimeOffset.Parse("2026-05-06T18:00:00Z")));
        IAccountKeySource source = CreateKeySource("demo-key");

        AccountBackupDocument document = await service.ExportAsync(
            source,
            new AccountBackupExportRequest("Demo account"),
            CancellationToken.None);

        AccountBackupDocument forged = document with { Address = new Address("acct:forged") };

        Assert.Throws<ArgumentException>(() => service.Import(new AccountBackupImportRequest(forged)));
    }

    [Fact]
    public async Task AccountBackup_RejectsForgedPublicKeyHash()
    {
        var service = new AccountBackupService(new ManualTimeProvider(DateTimeOffset.Parse("2026-05-06T18:00:00Z")));
        IAccountKeySource source = CreateKeySource("demo-key");

        AccountBackupDocument document = await service.ExportAsync(
            source,
            new AccountBackupExportRequest("Demo account"),
            CancellationToken.None);

        AccountBackupDocument forged = document with { PublicKeyHash = new HashHex("public-hash") };

        Assert.Throws<ArgumentException>(() => service.Import(new AccountBackupImportRequest(forged)));
    }

    [Theory]
    [InlineData("QmDemoHash", "QmDemoHash")]
    [InlineData("http://127.0.0.1:8080/ipfs/QmDemoHash", "QmDemoHash")]
    [InlineData("https://example.test/gateway/ipfs/QmDemoHash?download=true", "QmDemoHash")]
    public void StorageUri_ExtractsIpfsHash(string input, string expected)
    {
        Assert.Equal(expected, BusinessObjectStorageUri.GetHash(input));
    }

    private static IAccountKeySource CreateKeySource(string keyId)
    {
        (byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
        return new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, keyId, publicKey, privateKey);
    }
}
