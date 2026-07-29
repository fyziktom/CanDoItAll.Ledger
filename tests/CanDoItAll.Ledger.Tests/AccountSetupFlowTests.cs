using System.Security.Cryptography;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class AccountSetupFlowTests
{
    [Fact]
    public async Task AccountSetup_CreateUnlockBackupAndImport_RoundTripsSameAddress()
    {
        AccountSetupService service = CreateService(out InMemoryAccountVaultStore store, out AccountVaultService vault);

        AccountSetupAccount created = await service.CreateAsync(
            new AccountSetupCreateRequest("primary", "Primary account", "vault-password", "vault-password"),
            CancellationToken.None);
        AccountSetupAccount unlocked = await service.UnlockAsync(
            new AccountSetupUnlockRequest("primary", "vault-password"),
            CancellationToken.None);
        AccountSetupBackupExportResult backup = await service.ExportBackupAsync(
            new AccountSetupBackupExportRequest(
                "primary",
                "vault-password",
                "backup-password",
                "Primary account",
                "backup note"),
            CancellationToken.None);
        AccountSetupAccount imported = await service.ImportBackupAsync(
            new AccountSetupBackupImportRequest(
                "imported",
                backup.Json,
                "backup-password",
                "new-vault-password",
                "new-vault-password"),
            CancellationToken.None);
        AccountSetupAccount importedUnlocked = await service.UnlockAsync(
            new AccountSetupUnlockRequest("imported", "new-vault-password"),
            CancellationToken.None);

        Assert.Equal(created.Address, unlocked.Address);
        Assert.Equal(created.Address, imported.Address);
        Assert.Equal(imported.Address, importedUnlocked.Address);
        Assert.Equal(AccountBackupService.PasswordProtection, backup.Document.PrivateKeyProtection);
        AccountVaultUnlockedKey rawKey = await vault.UnlockAsync("primary", "vault-password", CancellationToken.None);
        Assert.DoesNotContain(Convert.ToBase64String(rawKey.PrivateKeyMaterial), backup.Json, StringComparison.Ordinal);
        Assert.NotNull(await store.GetAsync("imported", CancellationToken.None));
    }

    [Fact]
    public async Task AccountSetup_RejectsMismatchedNewPasswordWithoutSavingImport()
    {
        AccountSetupService service = CreateService(out InMemoryAccountVaultStore store, out _);
        AccountSetupAccount created = await service.CreateAsync(
            new AccountSetupCreateRequest("primary", "Primary account", "vault-password", "vault-password"),
            CancellationToken.None);
        AccountSetupBackupExportResult backup = await service.ExportBackupAsync(
            new AccountSetupBackupExportRequest("primary", "vault-password", "backup-password", "Primary account"),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ImportBackupAsync(
                new AccountSetupBackupImportRequest("bad-import", backup.Json, "backup-password", "new-password", "different-password"),
                CancellationToken.None));

        Assert.Equal(created.Address, (await service.UnlockAsync(new AccountSetupUnlockRequest("primary", "vault-password"), CancellationToken.None)).Address);
        Assert.Null(await store.GetAsync("bad-import", CancellationToken.None));
    }

    [Fact]
    public async Task AccountSetup_RejectsWrongBackupPasswordWithoutSavingImport()
    {
        AccountSetupService service = CreateService(out InMemoryAccountVaultStore store, out _);
        await service.CreateAsync(
            new AccountSetupCreateRequest("primary", "Primary account", "vault-password", "vault-password"),
            CancellationToken.None);
        AccountSetupBackupExportResult backup = await service.ExportBackupAsync(
            new AccountSetupBackupExportRequest("primary", "vault-password", "backup-password", "Primary account"),
            CancellationToken.None);

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await service.ImportBackupAsync(
                new AccountSetupBackupImportRequest("bad-import", backup.Json, "wrong-backup-password", "new-password", "new-password"),
                CancellationToken.None));

        Assert.Null(await store.GetAsync("bad-import", CancellationToken.None));
    }

    [Fact]
    public async Task AccountSetup_ImportsPastedPrivateKeyUnderNewPassword()
    {
        AccountSetupService service = CreateService(out _, out _);
        (byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();

        AccountSetupAccount imported = await service.ImportPastedPrivateKeyAsync(
            new AccountSetupPastedPrivateKeyImportRequest(
                "pasted",
                "Pasted account",
                Convert.ToBase64String(privateKey),
                "new-password",
                "new-password"),
            CancellationToken.None);
        AccountSetupAccount unlocked = await service.UnlockAsync(
            new AccountSetupUnlockRequest("pasted", "new-password"),
            CancellationToken.None);

        Assert.Equal(Address.FromPublicKey(publicKey), imported.Address);
        Assert.Equal(imported.Address, unlocked.Address);
    }

    [Fact]
    public async Task AccountBackup_RejectsPrivateKeyThatDoesNotMatchPublicKey()
    {
        var backup = new AccountBackupService(new ManualTimeProvider(DateTimeOffset.Parse("2026-05-06T18:00:00Z")));
        IAccountKeySource first = CreateKeySource("first");
        IAccountKeySource second = CreateKeySource("second");
        AccountBackupDocument firstDocument = await backup.ExportAsync(
            first,
            new AccountBackupExportRequest("First"),
            CancellationToken.None);
        AccountBackupDocument secondDocument = await backup.ExportAsync(
            second,
            new AccountBackupExportRequest("Second"),
            CancellationToken.None);
        AccountBackupDocument forged = firstDocument with { PrivateKeyMaterial = secondDocument.PrivateKeyMaterial };

        Assert.Throws<CryptographicException>(() => backup.Import(new AccountBackupImportRequest(forged)));
    }

    private static AccountSetupService CreateService(out InMemoryAccountVaultStore store, out AccountVaultService vault)
    {
        var driver = new DotNetLedgerCryptoDriver();
        store = new InMemoryAccountVaultStore();
        vault = new AccountVaultService(driver, store);
        var backup = new AccountBackupService(new ManualTimeProvider(DateTimeOffset.Parse("2026-05-06T18:00:00Z")));
        return new AccountSetupService(vault, backup);
    }

    private static IAccountKeySource CreateKeySource(string keyId)
    {
        (byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
        return new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, keyId, publicKey, privateKey);
    }
}
