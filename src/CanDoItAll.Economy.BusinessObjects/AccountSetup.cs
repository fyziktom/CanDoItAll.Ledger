using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed record AccountSetupCreateRequest(
    string VaultId,
    string DisplayName,
    string Password,
    string ConfirmPassword);

public sealed record AccountSetupUnlockRequest(
    string VaultId,
    string Password);

public sealed record AccountSetupBackupExportRequest(
    string VaultId,
    string VaultPassword,
    string? BackupPassword,
    string DisplayName,
    string? Notes = null);

public sealed record AccountSetupBackupExportResult(
    AccountBackupDocument Document,
    string Json,
    string FileName);

public sealed record AccountSetupBackupImportRequest(
    string VaultId,
    string BackupJson,
    string? BackupPassword,
    string NewPassword,
    string ConfirmNewPassword,
    string? DisplayName = null);

public sealed record AccountSetupPastedPrivateKeyImportRequest(
    string VaultId,
    string DisplayName,
    string PrivateKeyMaterialBase64,
    string NewPassword,
    string ConfirmNewPassword,
    string? KeyId = null);

public sealed record AccountSetupAccount(
    string VaultId,
    string DisplayName,
    Address Address,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    HashHex PublicKeyHash,
    string PublicKeyMaterialBase64);

public interface IAccountSetupService
{
    ValueTask<AccountSetupAccount> CreateAsync(AccountSetupCreateRequest request, CancellationToken cancellationToken);

    ValueTask<AccountSetupAccount> UnlockAsync(AccountSetupUnlockRequest request, CancellationToken cancellationToken);

    ValueTask<AccountSetupBackupExportResult> ExportBackupAsync(AccountSetupBackupExportRequest request, CancellationToken cancellationToken);

    ValueTask<AccountSetupAccount> ImportBackupAsync(AccountSetupBackupImportRequest request, CancellationToken cancellationToken);

    ValueTask<AccountSetupAccount> ImportPastedPrivateKeyAsync(AccountSetupPastedPrivateKeyImportRequest request, CancellationToken cancellationToken);
}

public sealed class AccountSetupService(
    AccountVaultService vaultService,
    IAccountBackupService backupService) : IAccountSetupService
{
    public async ValueTask<AccountSetupAccount> CreateAsync(AccountSetupCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsurePasswordConfirmed(request.Password, request.ConfirmPassword);
        EnsureDisplayName(request.DisplayName);

        AccountVaultCreationResult created = await vaultService.CreateAsync(
            new AccountVaultCreateRequest(
                request.VaultId,
                request.Password,
                request.DisplayName,
                request.VaultId),
            cancellationToken).ConfigureAwait(false);

        return ToAccount(created.UnlockedKey, request.DisplayName);
    }

    public async ValueTask<AccountSetupAccount> UnlockAsync(AccountSetupUnlockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        AccountVaultUnlockedKey unlocked = await vaultService.UnlockAsync(
            request.VaultId,
            request.Password,
            cancellationToken).ConfigureAwait(false);
        return ToAccount(unlocked, unlocked.DisplayName ?? unlocked.VaultId);
    }

    public async ValueTask<AccountSetupBackupExportResult> ExportBackupAsync(
        AccountSetupBackupExportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureDisplayName(request.DisplayName);

        AccountVaultUnlockedKey unlocked = await vaultService.UnlockAsync(
            request.VaultId,
            request.VaultPassword,
            cancellationToken).ConfigureAwait(false);
        AccountBackupDocument document = await backupService.ExportAsync(
            ToKeySource(unlocked),
            new AccountBackupExportRequest(request.DisplayName, request.BackupPassword, request.Notes),
            cancellationToken).ConfigureAwait(false);
        string json = backupService.Serialize(document);
        return new AccountSetupBackupExportResult(document, json, BuildBackupFileName(document));
    }

    public async ValueTask<AccountSetupAccount> ImportBackupAsync(
        AccountSetupBackupImportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsurePasswordConfirmed(request.NewPassword, request.ConfirmNewPassword);

        AccountBackupDocument document = backupService.Deserialize(request.BackupJson);
        IAccountKeySource keySource;
        try
        {
            keySource = backupService.Import(new AccountBackupImportRequest(document, request.BackupPassword));
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException("Unable to import account backup.", exception);
        }

        AccountVaultCreationResult imported = await ImportKeySourceAsync(
            request.VaultId,
            request.DisplayName ?? document.DisplayName,
            request.NewPassword,
            keySource,
            cancellationToken).ConfigureAwait(false);
        return ToAccount(imported.UnlockedKey, request.DisplayName ?? document.DisplayName);
    }

    public async ValueTask<AccountSetupAccount> ImportPastedPrivateKeyAsync(
        AccountSetupPastedPrivateKeyImportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureDisplayName(request.DisplayName);
        EnsurePasswordConfirmed(request.NewPassword, request.ConfirmNewPassword);

        byte[] privateKeyMaterial = Convert.FromBase64String(request.PrivateKeyMaterialBase64);
        byte[] publicKeyMaterial = EcdsaP256KeyMaterial.ExportPublicKeyFromPrivateKey(privateKeyMaterial);
        var keySource = new InMemoryAccountKeySource(
            SignatureAlgorithmId.EcdsaP256Sha256,
            request.KeyId ?? request.VaultId,
            publicKeyMaterial,
            privateKeyMaterial);

        AccountVaultCreationResult imported = await ImportKeySourceAsync(
            request.VaultId,
            request.DisplayName,
            request.NewPassword,
            keySource,
            cancellationToken).ConfigureAwait(false);
        return ToAccount(imported.UnlockedKey, request.DisplayName);
    }

    private static AccountSetupAccount ToAccount(AccountVaultUnlockedKey unlocked, string displayName)
        => new(
            unlocked.VaultId,
            displayName,
            unlocked.Address,
            unlocked.Algorithm,
            unlocked.KeyId,
            unlocked.PublicKeyHash,
            Convert.ToBase64String(unlocked.PublicKeyMaterial));

    private static IAccountKeySource ToKeySource(AccountVaultUnlockedKey unlocked)
        => new AccountVaultKeySource(unlocked);

    private async ValueTask<AccountVaultCreationResult> ImportKeySourceAsync(
        string vaultId,
        string displayName,
        string password,
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> privateKeyMaterial = await keySource.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
        return await vaultService.ImportAsync(
            new AccountVaultImportRequest(
                vaultId,
                password,
                keySource.Address,
                keySource.Algorithm,
                keySource.KeyId,
                keySource.PublicKeyHash,
                keySource.PublicKeyMaterial.ToArray(),
                privateKeyMaterial.ToArray(),
                displayName),
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildBackupFileName(AccountBackupDocument document)
    {
        string name = Regex.Replace(document.DisplayName.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "account";
        }

        return $"{name}-{document.Address.Value}.json";
    }

    private static void EnsureDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Account display name is required.", nameof(displayName));
        }
    }

    private static void EnsurePasswordConfirmed(string password, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password and confirmation do not match.");
        }
    }
}
