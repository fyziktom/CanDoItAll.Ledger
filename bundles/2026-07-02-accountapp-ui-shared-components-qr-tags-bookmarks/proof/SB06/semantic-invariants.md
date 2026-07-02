# SB06 Semantic Invariants - Verification driver QR scan and ticket ownership flows

## Invariant SB06-INV-01

- Source raw note: R07/R08: use QR for address, tx hash, signed ownership, scanning, BO/ticket verification, and Used flag confirmation.
- Expected behavior: QR buttons are wired into address/tx/BO/ticket surfaces; BusinessObjectVerificationService verifies payload format, owner, object identity, outpoint, data hash, and ticket state; scanner/manual-entry flows are exposed.
- Disallowed shallow implementation: status-only, placeholder-only, fixture-only, or UI-only code that does not exercise production components/services.
- Failing-first or adversarial proof: Verification tests and screenshots reject wrong payload/hash/object identity and show Used ticket state after explicit confirmation.
- Passing proof: bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt; bundle://proof/SB07/transcripts/source-assertions.txt; bundle://proof/SB06/screenshots/address-qr-large.png; bundle://proof/SB06/screenshots/tx-hash-qr-large.png; bundle://proof/SB06/screenshots/ownership-qr-large.png; bundle://proof/SB06/screenshots/scan-manual-entry-large.png; bundle://proof/SB06/screenshots/ownership-verification-large.png; bundle://proof/SB06/screenshots/ticket-ownership-signed-large.png; bundle://proof/SB06/screenshots/ticket-used-confirm-large.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt
- Red-team negative case: bundle://proof/SB07/red-team-verifier.md
- Downstream dependency check: bundle://reviews/01-execution-report.md

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| BusinessObjectOwnershipVerification | repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs | repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectVerificationDialogContent.razor | bundle://proof/SB06/screenshots/ownership-verification-large.png | bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt |
