# SB04 Manifest - Generic address manager dialog bookmarks and subaccounts

## Status

- Completed

## Owned Requirements And Raw Notes

- R11/R12: generic address dialog for contacts/bookmarks, own/external addresses, favorites, tags, and selection reuse.

## Shipped Behavior

AddressDirectoryDialogContent and local address directory service support manage/select modes, own/external kinds, favorites, tags, cached profile hints, and reuse wrappers.

## Artifact Paths

- Semantic invariant contract: bundle://proof/SB04/semantic-invariants.md
- Changed-file hashes: bundle://proof/SB07/changed-file-hashes.json
- Source assertions and anti-stub audit: bundle://proof/SB07/transcripts/source-assertions.txt
- Source proof: repo://src/CanDoItAll.Economy.Components/Components/Account/AddressDirectoryDialogContent.razor; repo://src/CanDoItAll.Economy.Components/Components/Account/AddressBookmarkManager.razor; repo://src/CanDoItAll.Economy.Components/Components/Account/AddressBookPicker.razor
- Test proof: bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt; bundle://proof/SB07/transcripts/source-assertions.txt
- Browser/visual proof: bundle://proof/SB04/screenshots/address-manager-large.png; bundle://proof/SB04/screenshots/address-select-send-large.png

## Failing-First Or Adversarial Proof

Manage and select screenshots plus service tests reject a home-only bookmark list or a selector that cannot persist favorite/tag metadata.

## Passing Proof

The shipped behavior is proven by the cited build/test transcripts, source assertions, and screenshot artifacts. The invariant id appears in this contract and the validator verifies the contract path exists.

## Anti-Stub Audit

bundle://proof/SB07/transcripts/source-assertions.txt records implementation symbols and a scoped scan with no TODO, FIXME, HACK, NotImplemented, or stubbed markers in touched implementation paths.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| AddressDirectoryEntry | repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs | repo://src/CanDoItAll.Economy.Components/Components/Account/AddressDirectoryDialogContent.razor | bundle://proof/SB04/screenshots/address-manager-large.png | bundle://proof/SB04/screenshots/address-select-send-large.png |
