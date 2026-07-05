# SB02 Semantic Invariants

## SB02-EDIT-UPDATE

- Invariant ID: `SB02-EDIT-UPDATE`
- Source raw note: `N002` says each BO must be updatable with the same form used for minting and staged as a transaction using that BO UTXO.
- Expected behavior: BO details Actions exposes an Edit action, opens `BusinessObjectCreationWizard` in edit mode, preloads the selected BO, and stages an update-mode draft.
- Disallowed shallow implementation: adding a cosmetic Edit button, duplicating the mint form, staging a create draft, generating a replacement object id, or dropping the edit callback fails the invariant.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-no-bo-edit-update.txt` recorded that pre-change BO details had no edit/update path and exited non-zero.
- Passing test: `bundle://proof/SB02/transcripts/passing-tests-edit-update.txt` passed the targeted edit/update and ownership-placement component tests.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`, `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectCreationWizardModel.cs`, and the BO details/host callback wiring files listed in `bundle://proof/SB02/manifest.md`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-edit-update.txt` proves Edit opens the shared wizard, stages `BusinessObjectTransactionMode.UpdateObject` with `editSource.ObjectId`, and forwards drafts through the existing submit flow.
- Red-team negative case: create-mode staging, a duplicated form, missing selected BO id, or reintroduced BO ownership scanner fails `SB02-EDIT-UPDATE`.
- Downstream dependency check: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt` verifies the rebuilt Docker AccountApp opens the edit wizard from BO details and exposes `Stage BO Update`.

## Evidence

- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first red-team transcript: `bundle://proof/SB02/transcripts/failing-first-no-bo-edit-update.txt`
- Passing targeted component test transcript: `bundle://proof/SB02/transcripts/passing-tests-edit-update.txt`
- Source assertion transcript: `bundle://proof/SB02/transcripts/source-assertions-edit-update.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Docker browser transcript: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt`
