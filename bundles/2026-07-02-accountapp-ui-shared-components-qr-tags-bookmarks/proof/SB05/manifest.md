# SB05 Manifest - Browser settings backup import export

## Status

- Completed

## Owned Requirements And Raw Notes

- R13/R14: localStorage/IndexedDB settings options must be exportable/importable through a dialog.

## Shipped Behavior

SettingsBackupDialogContent and AccountAppSettingsBackupService export/import versioned tag and address-directory settings with preview and section selection.

## Artifact Paths

- Semantic invariant contract: bundle://proof/SB05/semantic-invariants.md
- Changed-file hashes: bundle://proof/SB07/changed-file-hashes.json
- Source assertions and anti-stub audit: bundle://proof/SB07/transcripts/source-assertions.txt
- Source proof: repo://src/CanDoItAll.Economy.Components/Components/Account/SettingsBackupDialogContent.razor; repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs
- Test proof: bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt
- Browser/visual proof: bundle://proof/SB05/screenshots/settings-backup-export-large.png; bundle://proof/SB05/screenshots/settings-backup-import-preview-large.png

## Failing-First Or Adversarial Proof

Import preview and invalid/sectioned import service tests reject blind overwrite behavior.

## Passing Proof

The shipped behavior is proven by the cited build/test transcripts, source assertions, and screenshot artifacts. The invariant id appears in this contract and the validator verifies the contract path exists.

## Anti-Stub Audit

bundle://proof/SB07/transcripts/source-assertions.txt records implementation symbols and a scoped scan with no TODO, FIXME, HACK, NotImplemented, or stubbed markers in touched implementation paths.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| AccountAppSettingsBackup | repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs | repo://src/CanDoItAll.Economy.Components/Components/Account/SettingsBackupDialogContent.razor | bundle://proof/SB05/screenshots/settings-backup-import-preview-large.png | bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt |
