using System.Globalization;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using Microsoft.JSInterop;

namespace CanDoItAll.Ledger.Cryptography.Browser;

public sealed class BrowserIndexedDbAccountVaultStore : IAccountVaultStore
{
    private readonly BrowserLedgerCryptoDriver browserCryptoDriver;

    public BrowserIndexedDbAccountVaultStore(BrowserLedgerCryptoDriver browserCryptoDriver)
    {
        this.browserCryptoDriver = browserCryptoDriver ?? throw new ArgumentNullException(nameof(browserCryptoDriver));
    }

    public async ValueTask SaveAsync(AccountVaultRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        IJSObjectReference module = await browserCryptoDriver.GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync("saveVaultRecord", cancellationToken, AccountVaultRecordDto.FromRecord(record)).ConfigureAwait(false);
    }

    public async ValueTask<AccountVaultRecord?> GetAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        EnsureVaultId(vaultId);
        IJSObjectReference module = await browserCryptoDriver.GetModuleAsync(cancellationToken).ConfigureAwait(false);
        AccountVaultRecordDto? dto = await module.InvokeAsync<AccountVaultRecordDto?>(
            "getVaultRecord",
            cancellationToken,
            vaultId).ConfigureAwait(false);
        return dto?.ToRecord();
    }

    public async ValueTask DeleteAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        EnsureVaultId(vaultId);
        IJSObjectReference module = await browserCryptoDriver.GetModuleAsync(cancellationToken).ConfigureAwait(false);
        await module.InvokeVoidAsync("deleteVaultRecord", cancellationToken, vaultId).ConfigureAwait(false);
    }

    private static void EnsureVaultId(string vaultId)
    {
        if (string.IsNullOrWhiteSpace(vaultId))
        {
            throw new ArgumentException("Vault id is required.", nameof(vaultId));
        }
    }

    private sealed class AccountVaultRecordDto
    {
        public string VaultId { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string KeyId { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public int Algorithm { get; set; }

        public string PublicKeyHash { get; set; } = string.Empty;

        public string PublicKeyMaterialBase64 { get; set; } = string.Empty;

        public string EncryptedPrivateKeyBase64 { get; set; } = string.Empty;

        public string KdfSaltBase64 { get; set; } = string.Empty;

        public string NonceBase64 { get; set; } = string.Empty;

        public int KdfIterations { get; set; }

        public string KdfAlgorithm { get; set; } = string.Empty;

        public string EncryptionAlgorithm { get; set; } = string.Empty;

        public string CreatedAtUtc { get; set; } = string.Empty;

        public string UpdatedAtUtc { get; set; } = string.Empty;

        public static AccountVaultRecordDto FromRecord(AccountVaultRecord record)
            => new()
            {
                VaultId = record.VaultId,
                DisplayName = record.DisplayName,
                KeyId = record.KeyId,
                Address = record.Address.Value,
                Algorithm = (int)record.Algorithm,
                PublicKeyHash = record.PublicKeyHash,
                PublicKeyMaterialBase64 = record.PublicKeyMaterialBase64,
                EncryptedPrivateKeyBase64 = record.EncryptedPrivateKeyBase64,
                KdfSaltBase64 = record.KdfSaltBase64,
                NonceBase64 = record.NonceBase64,
                KdfIterations = record.KdfIterations,
                KdfAlgorithm = record.KdfAlgorithm,
                EncryptionAlgorithm = record.EncryptionAlgorithm,
                CreatedAtUtc = record.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                UpdatedAtUtc = record.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture)
            };

        public AccountVaultRecord ToRecord()
        {
            if (!CanDoItAll.Ledger.Core.Address.TryParse(Address, out CanDoItAll.Ledger.Core.Address address))
            {
                throw new FormatException("Stored account vault address is invalid.");
            }

            return new AccountVaultRecord(
                VaultId,
                DisplayName,
                KeyId,
                address,
                (SignatureAlgorithmId)Algorithm,
                PublicKeyHash,
                PublicKeyMaterialBase64,
                EncryptedPrivateKeyBase64,
                KdfSaltBase64,
                NonceBase64,
                KdfIterations,
                KdfAlgorithm,
                EncryptionAlgorithm,
                DateTimeOffset.Parse(CreatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                DateTimeOffset.Parse(UpdatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }
    }
}
