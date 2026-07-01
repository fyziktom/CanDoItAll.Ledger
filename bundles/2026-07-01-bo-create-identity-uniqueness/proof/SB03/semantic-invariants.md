# Semantic Invariants

| Invariant | Proof |
|---|---|
| A BO create cannot overwrite a current BO with the same object id. | `LedgerCriticalInvariantTests` included in `proof/SB02/transcripts/01-focused-tests.txt`. |
| A user-facing BO name is metadata and may repeat. | `BusinessObjectCreateIdentityTests.Wizard_StagesDistinctCreateIdsForRepeatedSameNameWithoutChangingPayload`. |
| New interactive BO identities are valid ledger identifiers and bounded to the max ID length. | `BusinessObjectCreateIdentityTests.Factory_CreatesValidDistinctIdsForSameTypeAndName` and `Factory_StaysInsideLedgerIdentifierLimitsForLongNames`. |
| Browser AccountApp can create two image BOs with the same title without the reported transition error. | `proof/SB03/transcripts/04-playwright-duplicate-image-name.txt` and `proof/SB03/screenshots/alice-duplicate-image-name-gallery.png`. |
