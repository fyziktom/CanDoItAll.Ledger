# SB01 Semantic Invariants

## SB01-HOME-ONLY

- Invariant ID: `SB01-HOME-ONLY`
- Source raw note: `N001` says the ownership verification tool must be only on the Home page as a wallet-owner tool.
- Expected behavior: the ownership verifier scanner remains available from Home Security and is absent from the BO details Actions tab.
- Disallowed shallow implementation: hiding only button text while leaving a BO details scanner launcher, verifier dialog entry point, or `BusinessObjectVerificationDialogContent` callback in BO Actions fails the invariant.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-bo-details-verifier-present.txt` recorded the pre-change BO details scanner action and exited non-zero.
- Passing test: `bundle://proof/SB01/transcripts/passing-tests-ownership-placement.txt` passed the targeted component tests for Home verifier availability and BO details scanner absence.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` and `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-ownership-placement.txt` proves BO details has no verifier launcher, keeps `cda-bo-ownership-qr-button`, and Home still opens `MessageSecurityPanel`.
- Red-team negative case: reintroducing `Verify ownership`, `OpenVerificationDialogAsync`, or a verifier dialog reference in BO details fails `SB01-HOME-ONLY`.
- Downstream dependency check: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt` verifies the rebuilt Docker AccountApp exposes Home ownership verification while BO details Actions has owner QR and no scanner.

## Evidence

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first red-team transcript: `bundle://proof/SB01/transcripts/failing-first-bo-details-verifier-present.txt`
- Passing targeted component test transcript: `bundle://proof/SB01/transcripts/passing-tests-ownership-placement.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions-ownership-placement.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Docker browser transcript: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt`
