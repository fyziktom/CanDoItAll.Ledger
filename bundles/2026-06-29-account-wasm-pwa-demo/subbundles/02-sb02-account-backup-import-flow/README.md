# SB02 Account Backup Import Flow

## Status

- `Complete`

## Objective

- Implement the create, unlock, backup, and import account flows using the browser vault from SB01 while preserving every user-story step from the VEBlazor reference and improving the service boundaries.

## Success Criteria

- Create account encrypts and stores a private key in the browser vault and loads the account address.
- Unlock decrypts the stored key with the user password and loads account session state.
- Backup exports recovery data through an explicit browser action.
- Import accepts a backup file or pasted key, handles old password when needed, asks for new password and confirmation, stores the imported account under the new password, and recovers the same address.
- Invalid backups, wrong passwords, and mismatched password confirmation fail clearly without corrupting stored vault data.

## Covered Inputs

- RQ17, RQ18, RQ19, RQ20, RQ21, RQ22, RQ30.
- Architect note to copy the VEBlazor create/account/backup/import flow without skipping steps, but improve implementation quality.

## Prerequisites

- SB01 progression gate passed.
- `proof/SB01/manifest.md` exists and confirms browser vault compatibility.
- `proof/SB02/manifest.md` exists and confirms account lifecycle proof.

## Completion Notes

- Closed on `2026-06-29`.
- Added `AccountSetupService` with create, unlock, backup export, backup import, and pasted private-key import.
- Extended vault import and backup import validation so imported accounts recover the same address and forged private/public key pairs fail.
- Made account backup components host-neutral by exposing explicit flow callbacks.
- Proved the browser path with WebCrypto and IndexedDB in `proof/SB02/account-setup-browser-harness-result.json`.
- Final focused regression: `13 passed`.
- Full ledger regression after SB02: `179 passed`.

## Exact Source References

- repo://src/CanDoItAll.Economy.BusinessObjects/AccountBackup.cs
- repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectAccountBackupTests.cs
- repo://src/CanDoItAll.Economy.Components/Components/Account/AccountBackupPanel.razor
- repo://src/CanDoItAll.Economy.Components/Components/Account/AccountSettingsDialogContent.razor
- bundle://evidence/veframework-reference-map.md

## Deliverables

- Account setup/application services that separate vault, account session, backup import/export, and UI state.
- Functional create-account, unlock, backup, and import components or adapted existing components.
- Compatibility between existing `AccountBackupService` format and browser backup/import where feasible.
- Negative tests for invalid backup, wrong old password, mismatched new password, and address/public-key mismatch.
- Browser proof for create, backup, import, and unlock flows.

## Dependency Impact

- SB05 needs these services to make shared components host-neutral.
- SB06 needs these flows for the first-run PWA experience.
- SB07 uses this proof to close the raw backup/import requirement.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Compare the VEBlazor create/unlock/import/backup user-story steps against existing Ledger components.
2. Define account setup/session interfaces that use the SB01 vault instead of broad AppData-style state.
3. Make the existing backup panel functional or replace it with a host-neutral component that follows the same flow.
4. Add backup export and import services that preserve address integrity.
5. Add unit tests for backup/import vectors and negative cases.
6. Add browser flow tests using the real vault.
7. Update workbook rows, execution report, and proof manifest.

## Scope Exceptions

- Do not wire transaction sending in this subbundle.
- Do not solve API data loading beyond the minimal account-loaded event needed for later subbundles.

## Do Not Do

- Do not copy VEFramework `AppData` as a single service.
- Do not skip backup-file import or pasted-key import paths.
- Do not accept import when password confirmation fails.
- Do not persist raw private key material.

## Acceptance Checklist

- Create account path stores encrypted vault data and reports an address.
- Unlock path recovers the same address.
- Backup export file can be imported in a clean browser profile.
- Pasted key import path works independently of backup upload.
- Wrong old password and tampered backup fail.
- Account setup components are not coupled to SSR node sessions.

## Proof Required

- `proof/SB02/manifest.md` with changed-file hashes, command transcript links, and anti-stub audit.
- Unit test command output for backup/import positive and negative vectors.
- Browser screenshots and trace artifacts for create, unlock, backup export, and import.
- Proof artifact comparing exported and re-imported account address/public-key hash.
- Production Behavior Artifact Matrix for account setup session states and backup import outcomes.

## Browser Validation Logging

- Target route: AccountApp setup/import route or component test host.
- Viewports: desktop 1440x1000 and mobile 390x844.
- Actions: create account, reload, unlock, export backup, clear browser profile, import backup, import pasted key, test wrong password.
- Screenshots/evidence: `proof/SB02/create-unlock.png`, `proof/SB02/backup-import.png`, `proof/SB02/mobile-import.png`.
- Review questions: Are all VEBlazor flow steps represented, and is the improved implementation split into vault/session/backup/UI boundaries?

## Progression Gate

- Passed. SB05 and SB06 may consume the SB02 account lifecycle services and component callback surface.

## Suggested Agent Prompt

```text
Implement SB02 only. Use the SB01 vault, copy the VEBlazor account-flow steps at user-story level, keep services separated, prove create/unlock/backup/import with unit and browser tests, and stop if imported accounts cannot recover the same address.
```
