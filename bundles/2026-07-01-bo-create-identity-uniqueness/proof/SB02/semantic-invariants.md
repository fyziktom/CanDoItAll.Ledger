# SB02 Semantic Invariants

| Invariant | Proof |
|---|---|
| New interactive BO creates use unique IDs independent of display name. | `BusinessObjectCreateIdentityTests.Factory_CreatesValidDistinctIdsForSameTypeAndName`. |
| Generated IDs remain valid ledger identifiers. | `BusinessObjectCreateIdentityTests.Factory_StaysInsideLedgerIdentifierLimitsForLongNames`. |
| Payload metadata is not rewritten to manufacture uniqueness. | `BusinessObjectCreateIdentityTests.Wizard_StagesDistinctCreateIdsForRepeatedSameNameWithoutChangingPayload`. |
| The repair is scoped to create drafts and does not alter preview/update/transfer IDs. | Source review in `src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` and unchanged ledger/builder tests. |
