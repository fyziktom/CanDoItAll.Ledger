using System.Text;
using System.Text.Json;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.AccountApp.Services;

public sealed class AccountAppSession(
    AccountAppOptions options,
    AccountVaultService vaultService,
    IAccountVaultStore vaultStore,
    IEconomyCryptoDriver cryptoDriver,
    WasmAccountWorkspaceAdapter workspaceAdapter)
{
    public const string ResetConfirmationPhrase = "RESET LOCAL VAULT";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public AccountAppOptions Options { get; } = options;

    public WasmAccountWorkspaceAdapter WorkspaceAdapter => workspaceAdapter;

    public string AccountName { get; set; } = options.DefaultAccountName;

    public string CreatePassword { get; set; } = string.Empty;

    public string CreateConfirmPassword { get; set; } = string.Empty;

    public string UnlockPassword { get; set; } = string.Empty;

    public string BackupJson { get; private set; } = string.Empty;

    public string BackupStatus { get; private set; } = "No backup exported in this session.";

    public string ImportBackupJson { get; set; } = string.Empty;

    public string ImportBackupPassword { get; set; } = string.Empty;

    public string ImportNewPassword { get; set; } = string.Empty;

    public string ImportConfirmPassword { get; set; } = string.Empty;

    public string PastedPublicKeyBase64 { get; set; } = string.Empty;

    public string PastedPrivateKeyBase64 { get; set; } = string.Empty;

    public string PastedKeyPassword { get; set; } = string.Empty;

    public string PastedKeyConfirmPassword { get; set; } = string.Empty;

    public string PastedKeyName { get; set; } = options.DefaultAccountName;

    public bool ResetAcknowledged { get; set; }

    public string ResetConfirmation { get; set; } = string.Empty;

    public bool HasStoredVault { get; private set; }

    public bool IsUnlocked => UnlockedKey is not null;

    public bool CanResetLocalVault
        => HasStoredVault
            && ResetAcknowledged
            && string.Equals(ResetConfirmation.Trim(), ResetConfirmationPhrase, StringComparison.Ordinal);

    public AccountSetupAccount? Account { get; private set; }

    public AccountVaultUnlockedKey? UnlockedKey { get; private set; }

    public BusinessObjectWorkspaceModel? Workspace { get; private set; }

    public EconomyTransactionExecutionResult? LastTransactionResult { get; private set; }

    public EconomyTransactionDetailsModel? SelectedTransactionDetails { get; private set; }

    public string StatusMessage { get; private set; } = "Create, import, or unlock a browser vault to load the account workspace.";

    public string? ErrorMessage { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HasStoredVault = await vaultStore.GetAsync(Options.VaultId, cancellationToken).ConfigureAwait(false) is not null;
        StatusMessage = HasStoredVault
            ? "Encrypted browser vault found. Unlock it to load account data."
            : "No browser vault is stored yet. Create or import an account.";
    }

    public async Task CreateAccountAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            EnsurePassword(CreatePassword, CreateConfirmPassword);
            AccountVaultCreationResult created = await vaultService.CreateAsync(
                new AccountVaultCreateRequest(
                    Options.VaultId,
                    CreatePassword,
                    AccountName,
                    Options.VaultId),
                cancellationToken).ConfigureAwait(false);
            CreatePassword = string.Empty;
            CreateConfirmPassword = string.Empty;
            await ActivateAsync(created.UnlockedKey, cancellationToken).ConfigureAwait(false);
            StatusMessage = "Account created and unlocked from the encrypted browser vault.";
        }).ConfigureAwait(false);

    public async Task UnlockAccountAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            AccountVaultUnlockedKey unlocked = await vaultService
                .UnlockAsync(Options.VaultId, UnlockPassword, cancellationToken)
                .ConfigureAwait(false);
            UnlockPassword = string.Empty;
            await ActivateAsync(unlocked, cancellationToken).ConfigureAwait(false);
            StatusMessage = "Account unlocked from the encrypted browser vault.";
        }).ConfigureAwait(false);

    public async Task ExportVaultBackupAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            AccountVaultRecord record = await vaultStore.GetAsync(Options.VaultId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("No encrypted browser vault is available to back up.");
            var backup = new AccountAppVaultBackup(
                "candoitall-economy-accountapp-vault-backup-v1",
                DateTimeOffset.UtcNow,
                record);
            BackupJson = JsonSerializer.Serialize(backup, JsonOptions);
            BackupStatus = $"Encrypted backup exported for {record.Address.Value}.";
            StatusMessage = "Encrypted account vault backup is ready.";
        }).ConfigureAwait(false);

    public async Task ImportVaultBackupAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            EnsurePassword(ImportNewPassword, ImportConfirmPassword);
            AccountAppVaultBackup backup = JsonSerializer.Deserialize<AccountAppVaultBackup>(ImportBackupJson, JsonOptions)
                ?? throw new InvalidOperationException("Backup JSON did not contain a vault backup.");
            if (!string.Equals(backup.Format, "candoitall-economy-accountapp-vault-backup-v1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsupported AccountApp backup format.");
            }

            string sourceVaultId = backup.Record.VaultId;
            bool deleteSourceAfterImport = !string.Equals(sourceVaultId, Options.VaultId, StringComparison.Ordinal);
            try
            {
                await vaultStore.SaveAsync(backup.Record, cancellationToken).ConfigureAwait(false);
                AccountVaultUnlockedKey unlocked = await vaultService
                    .UnlockAsync(sourceVaultId, ImportBackupPassword, cancellationToken)
                    .ConfigureAwait(false);
                AccountVaultCreationResult imported = await vaultService.ImportAsync(
                    new AccountVaultImportRequest(
                        Options.VaultId,
                        ImportNewPassword,
                        unlocked.Address,
                        unlocked.Algorithm,
                        unlocked.KeyId,
                        unlocked.PublicKeyHash,
                        unlocked.PublicKeyMaterial,
                        unlocked.PrivateKeyMaterial,
                        unlocked.DisplayName),
                    cancellationToken).ConfigureAwait(false);
                ClearImportBackupFields();
                await ActivateAsync(imported.UnlockedKey, cancellationToken).ConfigureAwait(false);
                StatusMessage = "Encrypted backup imported, re-encrypted, and unlocked.";
            }
            finally
            {
                if (deleteSourceAfterImport)
                {
                    await vaultStore.DeleteAsync(sourceVaultId, cancellationToken).ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);

    public async Task ImportPastedKeyPairAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            EnsurePassword(PastedKeyPassword, PastedKeyConfirmPassword);
            byte[] publicKey = Convert.FromBase64String(PastedPublicKeyBase64);
            byte[] privateKey = Convert.FromBase64String(PastedPrivateKeyBase64);
            byte[] payload = Encoding.UTF8.GetBytes("CanDoItAll.Economy.AccountApp.PastedKeyImport.v1");
            byte[] signature = await cryptoDriver
                .SignEcdsaP256Sha256Async(payload, privateKey, cancellationToken)
                .ConfigureAwait(false);
            bool verified = await cryptoDriver
                .VerifyEcdsaP256Sha256Async(payload, signature, publicKey, cancellationToken)
                .ConfigureAwait(false);
            if (!verified)
            {
                throw new InvalidOperationException("Pasted private key does not match the public key.");
            }

            Address address = await EconomyAddressCodec.FromPublicKeyAsync(publicKey, cryptoDriver, cancellationToken).ConfigureAwait(false);
            HashHex publicKeyHash = await EconomyAddressCodec.CalculatePublicKeyHashAsync(publicKey, cryptoDriver, cancellationToken).ConfigureAwait(false);
            AccountVaultCreationResult imported = await vaultService.ImportAsync(
                new AccountVaultImportRequest(
                    Options.VaultId,
                    PastedKeyPassword,
                    address,
                    SignatureAlgorithmId.EcdsaP256Sha256,
                    Options.VaultId,
                    publicKeyHash,
                    publicKey,
                    privateKey,
                    string.IsNullOrWhiteSpace(PastedKeyName) ? Options.DefaultAccountName : PastedKeyName.Trim()),
                cancellationToken).ConfigureAwait(false);
            ClearPastedKeyFields();
            await ActivateAsync(imported.UnlockedKey, cancellationToken).ConfigureAwait(false);
            StatusMessage = "Pasted key pair imported into the encrypted browser vault.";
        }).ConfigureAwait(false);

    public async Task ResetLocalVaultAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            if (!CanResetLocalVault)
            {
                throw new InvalidOperationException($"Confirm reset by acknowledging the warning and typing {ResetConfirmationPhrase}.");
            }

            await vaultStore.DeleteAsync(Options.VaultId, cancellationToken).ConfigureAwait(false);
            ClearSessionState();
            HasStoredVault = false;
            StatusMessage = "Local browser vault reset. Create or import an account to continue.";
        }).ConfigureAwait(false);

    public async Task RefreshWorkspaceAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            EnsureUnlocked();
            Workspace = await workspaceAdapter.GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            StatusMessage = "Account data loaded from the configured node API.";
        }).ConfigureAwait(false);

    public async Task LoadLatestTransactionDetailsAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            EnsureUnlocked();
            LedgerTransactionSummary latest = Workspace?.Transactions.FirstOrDefault()
                ?? throw new InvalidOperationException("No transactions are available for this account.");
            SelectedTransactionDetails = await workspaceAdapter
                .GetTransactionDetailsAsync(latest.TxId, cancellationToken)
                .ConfigureAwait(false);
            StatusMessage = SelectedTransactionDetails is null
                ? "The selected transaction was not found by the node API."
                : "Transaction detail loaded from the node API.";
        }).ConfigureAwait(false);

    public async Task SubmitSignedPaymentSmokeAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async () =>
        {
            EnsureUnlocked();
            BusinessObjectWorkspaceModel workspace = Workspace
                ?? await workspaceAdapter.GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            var draft = workspace.TransactionDraft with
            {
                SelectedMode = BusinessObjectTransactionMode.MainPayment,
                PaymentReceiverAddress = workspace.Address,
                PaymentAmount = Amount.FromInt64(1)
            };
            LastTransactionResult = await workspaceAdapter
                .SubmitTransactionAsync(draft, cancellationToken)
                .ConfigureAwait(false);
            Workspace = await workspaceAdapter.GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            StatusMessage = LastTransactionResult.Accepted
                ? "Signed payment smoke transaction completed."
                : "Signed payment smoke transaction was rejected.";
        }).ConfigureAwait(false);

    public void ApplyWorkspace(BusinessObjectWorkspaceModel workspace)
    {
        Workspace = workspace;
    }

    public void ApplyTransactionResult(EconomyTransactionExecutionResult result)
    {
        LastTransactionResult = result;
    }

    private async Task ActivateAsync(AccountVaultUnlockedKey unlocked, CancellationToken cancellationToken)
    {
        UnlockedKey = unlocked;
        Account = new AccountSetupAccount(
            unlocked.VaultId,
            unlocked.DisplayName ?? Options.DefaultAccountName,
            unlocked.Address,
            unlocked.Algorithm,
            unlocked.KeyId,
            unlocked.PublicKeyHash,
            Convert.ToBase64String(unlocked.PublicKeyMaterial));
        AccountName = Account.DisplayName;
        HasStoredVault = true;
        ResetAcknowledged = false;
        ResetConfirmation = string.Empty;
        await workspaceAdapter.UseUnlockedAccountAsync(unlocked, cancellationToken).ConfigureAwait(false);
        Workspace = await workspaceAdapter.GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private void EnsureUnlocked()
    {
        if (UnlockedKey is null)
        {
            throw new InvalidOperationException("Unlock an account before loading workspace data.");
        }
    }

    private static void EnsurePassword(string password, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required.");
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password and confirmation do not match.");
        }
    }

    private void ClearImportBackupFields()
    {
        ImportBackupJson = string.Empty;
        ImportBackupPassword = string.Empty;
        ImportNewPassword = string.Empty;
        ImportConfirmPassword = string.Empty;
    }

    private void ClearPastedKeyFields()
    {
        PastedPublicKeyBase64 = string.Empty;
        PastedPrivateKeyBase64 = string.Empty;
        PastedKeyPassword = string.Empty;
        PastedKeyConfirmPassword = string.Empty;
    }

    private void ClearSessionState()
    {
        AccountName = Options.DefaultAccountName;
        CreatePassword = string.Empty;
        CreateConfirmPassword = string.Empty;
        UnlockPassword = string.Empty;
        BackupJson = string.Empty;
        BackupStatus = "No backup exported in this session.";
        ClearImportBackupFields();
        ClearPastedKeyFields();
        PastedKeyName = Options.DefaultAccountName;
        ResetAcknowledged = false;
        ResetConfirmation = string.Empty;
        Account = null;
        UnlockedKey = null;
        Workspace = null;
        LastTransactionResult = null;
        SelectedTransactionDetails = null;
    }

    private sealed record AccountAppVaultBackup(
        string Format,
        DateTimeOffset ExportedAtUtc,
        AccountVaultRecord Record);
}
