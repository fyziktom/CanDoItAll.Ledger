# SB02 Semantic Invariants

## Shallow-Pass Trap

A shallow implementation could add buttons and a service named `AccountSetupService` while skipping password confirmation, accepting any imported private key, storing imported vaults without re-encryption, or only testing happy-path JSON serialization.

SB02 rejects that trap with account-flow tests, backup-regression tests, component callback tests, and a browser harness that runs real WebCrypto plus IndexedDB account setup operations.

## Invariants

| Invariant | Proof |
| --- | --- |
| Created accounts store encrypted private key material and unlock to the same address | `AccountSetupFlowTests.AccountSetup_CreateUnlockBackupAndImport_RoundTripsSameAddress`; `account-setup-browser-harness-result.json` |
| Backup export does not contain the raw unlocked private key | `AccountSetupFlowTests.AccountSetup_CreateUnlockBackupAndImport_RoundTripsSameAddress`; browser harness `backup:export-encrypted` |
| Backup import recovers the same address under a new vault password | `AccountSetupFlowTests.AccountSetup_CreateUnlockBackupAndImport_RoundTripsSameAddress`; browser harness `backup:import-and-reencrypt-vault` |
| Wrong backup password fails without saving an imported vault | `AccountSetupFlowTests.AccountSetup_RejectsWrongBackupPasswordWithoutSavingImport`; browser harness `backup:reject-wrong-password` |
| Mismatched new password fails before import persistence | `AccountSetupFlowTests.AccountSetup_RejectsMismatchedNewPasswordWithoutSavingImport` |
| Pasted private key import derives the public key/address and stores under a new password | `AccountSetupFlowTests.AccountSetup_ImportsPastedPrivateKeyUnderNewPassword`; browser harness `import:pasted-private-key` |
| Imported backup private key must match the stored public key | `AccountSetupFlowTests.AccountBackup_RejectsPrivateKeyThatDoesNotMatchPublicKey` |
| Account setup panel remains host-neutral and exposes callbacks instead of SSR/node coupling | `AccountSetupComponentTests.AccountBackupPanel_ExposesHostNeutralAccountFlowActions` |

## Adversarial Negative Proof

- Wrong encrypted-backup password: import throws a normalized `CryptographicException` and no `bad-import` record exists.
- Mismatched new password confirmation: import throws before backup decryption/persistence and no `bad-import` record exists.
- Forged backup private key: import throws `CryptographicException` when private key material does not export the stored public key.
- Stub/dependency audit: no `TODO`, `NotImplementedException`, `throw new NotImplemented`, `NBitcoin`, or `BouncyCastle` references in SB02 implementation/test scope.

## Semantic Positive Proof

- .NET path creates a vault, unlocks it, exports an encrypted backup, imports the backup into a clean vault under a new password, and unlocks the imported account to the same address.
- Browser path generates P-256 key material, stores encrypted vault data in IndexedDB, unlocks/signs, exports encrypted backup JSON, rejects wrong backup password, imports/re-encrypts the backup into a new vault, and imports a pasted private key.
- UI path wires every expected account-flow action through callbacks: finish setup, unlock account, backup account, import backup, import private key, and create subaccount.

## Raw-Note Literal Closure

- "Copy VEBlazor create account, backup, import steps without skipping anything": SB02 includes create, unlock, encrypted backup export, backup import with old and new passwords, pasted-key import, and negative password/key-integrity paths.
- "Use better approach for implementation": SB02 uses separated vault, backup, account setup service, and host-neutral component callbacks instead of cloning broad VEBlazor app state.
- "Encryption is superimportant first step": SB02 consumes the SB01 vault and proves encrypted export/import before downstream AccountApp wiring.
- "Shared Razor components for SSR and WASM": SB02 account panel is callback-driven and does not inject node/session services.
