# SB05 Semantic Invariants - Browser settings backup import export

## Invariant SB05-INV-01

- Source raw note: R13/R14: localStorage/IndexedDB settings options must be exportable/importable through a dialog.
- Expected behavior: SettingsBackupDialogContent and AccountAppSettingsBackupService export/import versioned tag and address-directory settings with preview and section selection.
- Disallowed shallow implementation: status-only, placeholder-only, fixture-only, or UI-only code that does not exercise production components/services.
- Failing-first or adversarial proof: Import preview and invalid/sectioned import service tests reject blind overwrite behavior.
- Passing proof: bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt; bundle://proof/SB05/screenshots/settings-backup-export-large.png; bundle://proof/SB05/screenshots/settings-backup-import-preview-large.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt
- Red-team negative case: bundle://proof/SB07/red-team-verifier.md
- Downstream dependency check: bundle://reviews/01-execution-report.md

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| AccountAppSettingsBackup | repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs | repo://src/CanDoItAll.Economy.Components/Components/Account/SettingsBackupDialogContent.razor | bundle://proof/SB05/screenshots/settings-backup-import-preview-large.png | bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt |
