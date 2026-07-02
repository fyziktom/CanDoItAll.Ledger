# SB04 Semantic Invariants - Generic address manager dialog bookmarks and subaccounts

## Invariant SB04-INV-01

- Source raw note: R11/R12: generic address dialog for contacts/bookmarks, own/external addresses, favorites, tags, and selection reuse.
- Expected behavior: AddressDirectoryDialogContent and local address directory service support manage/select modes, own/external kinds, favorites, tags, cached profile hints, and reuse wrappers.
- Disallowed shallow implementation: status-only, placeholder-only, fixture-only, or UI-only code that does not exercise production components/services.
- Failing-first or adversarial proof: Manage and select screenshots plus service tests reject a home-only bookmark list or a selector that cannot persist favorite/tag metadata.
- Passing proof: bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt; bundle://proof/SB07/transcripts/source-assertions.txt; bundle://proof/SB04/screenshots/address-manager-large.png; bundle://proof/SB04/screenshots/address-select-send-large.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt
- Red-team negative case: bundle://proof/SB07/red-team-verifier.md
- Downstream dependency check: bundle://reviews/01-execution-report.md

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| AddressDirectoryEntry | repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs | repo://src/CanDoItAll.Economy.Components/Components/Account/AddressDirectoryDialogContent.razor | bundle://proof/SB04/screenshots/address-manager-large.png | bundle://proof/SB04/screenshots/address-select-send-large.png |
