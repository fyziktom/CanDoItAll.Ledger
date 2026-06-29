# SB02 Proof Manifest - Account Backup Import Flow

## Status

- Closure result: `Passed`
- Date: `2026-06-29`
- Scope closed: account create/unlock service, encrypted backup export/import, pasted private-key import, backup key-integrity validation, host-neutral account setup component actions, browser WebCrypto/IndexedDB account-flow proof.
- Downstream gate: SB05 and SB06 may consume SB02 account lifecycle services/components; SB07 still requires final integrated browser proof.

## Implementation Summary

- Added `AccountSetupService` and `IAccountSetupService` for create, unlock, backup export, backup import, and pasted private-key import flows.
- Extended `AccountVaultService` to import existing key material, persist display name and key id metadata, and verify imported private/public key ownership before saving.
- Extended the browser IndexedDB vault DTO with `displayName` and `keyId` so imported/created accounts preserve account metadata in WASM storage.
- Hardened `AccountBackupService.Import` so ECDSA P-256 backups reject private key material that does not export the stored public key.
- Updated account backup tests to use real ECDSA P-256 key material rather than placeholder byte arrays.
- Made `AccountBackupPanel` and `AccountBackupWizard` expose account-flow event callbacks for SSR/WASM hosts without taking a node/session dependency.
- Added unit/component tests and browser proof for create, unlock, backup export, backup import, wrong password rejection, mismatched password rejection, pasted-key import, and forged-key rejection.

## Source Assertions

- Account setup validates password confirmation before vault creation or import persistence.
- Backup import decrypts with the backup password, then stores imported keys in a new vault encrypted by the new vault password.
- Pasted private-key import derives the public key and address from PKCS8 private key material before persistence.
- Imported key material is verified by public-key export plus sign/verify challenge before vault save.
- Account backup JSON produced by the tested service does not contain raw private key material when password protection is used.
- Account setup components are host-neutral callback surfaces; they do not directly call SSR node sessions or browser APIs.

## Production Behavior Artifact Matrix

| Artifact | Behavior Proven | Evidence |
| --- | --- | --- |
| `AccountSetupService.CreateAsync` | Creates encrypted vault and returns account identity | `transcripts/08-sb02-final-focused-regression-tests.txt` |
| `AccountSetupService.UnlockAsync` | Decrypts stored vault and returns the same account address | `transcripts/08-sb02-final-focused-regression-tests.txt` |
| `AccountSetupService.ExportBackupAsync` | Produces password-protected backup JSON without raw private key material | `transcripts/08-sb02-final-focused-regression-tests.txt` |
| `AccountSetupService.ImportBackupAsync` | Imports encrypted backup into a new encrypted vault and recovers same address | `transcripts/08-sb02-final-focused-regression-tests.txt`, `account-setup-browser-harness-result.json` |
| `AccountSetupService.ImportPastedPrivateKeyAsync` | Imports pasted PKCS8 private key under new password and derives address | `transcripts/08-sb02-final-focused-regression-tests.txt`, `account-setup-browser-harness-result.json` |
| `AccountBackupService.Import` | Rejects mismatched private/public key backups | `transcripts/08-sb02-final-focused-regression-tests.txt` |
| `AccountBackupPanel` / `AccountBackupWizard` | Expose host-neutral callbacks for create/unlock/backup/import/subaccount actions | `AccountSetupComponentTests`, `transcripts/08-sb02-final-focused-regression-tests.txt` |
| Browser harness | Executes account setup semantics with WebCrypto and IndexedDB | `account-setup-browser-harness-result.json`, `sb02-account-setup-browser-harness.png` |

## Negative Fixture Proof

| Fixture | Expected Result | Evidence |
| --- | --- | --- |
| Wrong backup password | Throws `CryptographicException`; no imported vault saved | `AccountSetup_RejectsWrongBackupPasswordWithoutSavingImport` |
| Mismatched new password | Throws `InvalidOperationException`; no imported vault saved | `AccountSetup_RejectsMismatchedNewPasswordWithoutSavingImport` |
| Forged backup private key | Throws `CryptographicException` | `AccountBackup_RejectsPrivateKeyThatDoesNotMatchPublicKey` |
| Browser wrong password | WebCrypto AES-GCM decrypt rejects wrong backup password | `account-setup-browser-harness-result.json` |
| Stub/forbidden dependency audit | Zero hits for stub markers and NBitcoin/BouncyCastle | `transcripts/07-sb02-stub-forbidden-dependency-audit.txt` |

## Browser Proof

- Harness: `account-setup-browser-harness.html`
- Browser result: `account-setup-browser-harness-result.json`
- Screenshot: `sb02-account-setup-browser-harness.png`
- Semantic invariants: `semantic-invariants.md`
- Result summary:
  - `account:create-indexeddb-encrypted-vault`
  - `account:unlock-and-sign`
  - `backup:export-encrypted`
  - `backup:reject-wrong-password`
  - `backup:import-and-reencrypt-vault`
  - `import:pasted-private-key`
  - raw signature length: `64`
  - public key material length: `91`
  - private key material length: `138`
- The only browser console error during proof was a local proof-server `favicon.ico` 404.

## Command Transcripts

| Transcript | Result |
| --- | --- |
| `transcripts/01-failing-account-setup-flow-tests.txt` | Expected red test before SB02 implementation; missing `AccountSetupService` |
| `transcripts/02-account-setup-flow-tests-after-implementation.txt` | `AccountSetupFlowTests`: 5 passed |
| `transcripts/03-account-setup-flow-and-component-tests.txt` | Account setup flow and component callbacks: 6 passed |
| `transcripts/04-account-backup-regression-tests.txt` | Account backup regression tests: 7 passed |
| `transcripts/05-browser-crypto-project-build-after-account-setup.txt` | Browser crypto project build succeeded |
| `transcripts/06-account-setup-browser-harness.txt` | Browser account setup harness passed |
| `transcripts/07-sb02-stub-forbidden-dependency-audit.txt` | No stub markers or forbidden crypto dependency hits |
| `transcripts/08-sb02-final-focused-regression-tests.txt` | Final SB02 focused regression: 13 passed |
| `transcripts/09-full-ledger-tests-after-sb02.txt` | Full `CanDoItAll.Ledger.Tests`: 179 passed |
| `transcripts/10-workbook-regeneration-after-sb02.txt` | XLSX regenerated; formula error scan matched 0 entries |
| `transcripts/11-git-diff-check-after-sb02.txt` | `git diff --check` exit `0`; only LF-to-CRLF normalization warnings |

## Validation Commands

```powershell
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore --filter AccountSetupFlowTests
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore --filter "AccountSetupFlowTests|AccountSetupComponentTests|BusinessObjectAccountBackupTests"
dotnet build src/CanDoItAll.Economy.Cryptography.Browser/CanDoItAll.Economy.Cryptography.Browser.csproj --no-restore
rg -n "TODO|NotImplementedException|throw new NotImplemented|NBitcoin|BouncyCastle|Org\.BouncyCastle|Portable\.BouncyCastle" src/CanDoItAll.Economy.BusinessObjects/AccountSetup.cs src/CanDoItAll.Economy.BusinessObjects/AccountBackup.cs src/CanDoItAll.Economy.Cryptography/AccountVault.cs src/CanDoItAll.Economy.Cryptography.Browser src/CanDoItAll.Economy.Components/Components/Account/AccountBackupPanel.razor tests/CanDoItAll.Ledger.Tests/AccountSetupFlowTests.cs tests/CanDoItAll.Ledger.Tests/AccountSetupComponentTests.cs
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore
node bundles/2026-06-29-account-wasm-pwa-demo/workbook/build-planning-workbook.mjs
git diff --check
```

## Known Non-SB02 Blockers

- Full solution build blockers recorded in SB01 still apply outside SB02: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl` references missing `CanDoItAll.Components` namespaces/projects, and a live `CanDoItAll.Economy.Components.Demo` process was observed locking copied DLLs during SB01.
- These blockers do not affect the SB02 project builds or `CanDoItAll.Ledger.Tests` proof.

## Changed File Hashes

- See `changed-file-hashes.txt`.
