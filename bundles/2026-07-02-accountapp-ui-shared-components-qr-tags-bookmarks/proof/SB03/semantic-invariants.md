# SB03 Semantic Invariants - Tag occurrence service and shared tag rendering

## Invariant SB03-INV-01

- Source raw note: R09/R10: keep tag occurrence counts, unique counts, stable readable colors, TagEditor suggestions, and colored badges.
- Expected behavior: AccountWorkspaceLocalServices adds TagOccurrenceService and persisted TagVisualMetadata; BO forms use TagEditor; details use TagBadge.
- Disallowed shallow implementation: status-only, placeholder-only, fixture-only, or UI-only code that does not exercise production components/services.
- Failing-first or adversarial proof: Tests separate repeated opens from unique BO occurrence and verify readable color contrast; this rejects count-only or random-color implementations.
- Passing proof: bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt; bundle://proof/SB03/screenshots/tag-editor-suggestions-large.png; bundle://proof/SB03/screenshots/tag-badges-after-reload-large.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt
- Red-team negative case: bundle://proof/SB07/red-team-verifier.md
- Downstream dependency check: bundle://reviews/01-execution-report.md

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| TagOccurrenceRecord | repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs | repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor | bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt | bundle://proof/SB03/transcripts/account-workspace-local-services-tests.txt |
