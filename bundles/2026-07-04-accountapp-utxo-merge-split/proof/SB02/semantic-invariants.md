# SB02 Semantic Invariants

## Invariant ID: UTXO-FORMS-001

- Invariant ID: UTXO-FORMS-001
- Source raw note: Send, Merge, and Split forms must be reusable isolated components.
- Expected behavior: `TransactionComposerShell` delegates Payment, UTXO Merge, and UTXO Split tab bodies to independently renderable Razor components.
- Disallowed shallow implementation: leaving all fields embedded in one monolithic switch or relabeling old BusinessObject tabs.
- Failing-first test: process/non-production exemption; SB02 is a UI refactor built on SB01 failing-first domain proof, with component tests added for reusable components and draft behavior.
- Passing test: `bundle://proof/SB02/transcripts/05-passing-reusable-form-ui-tests-after-outpoint-polish.txt`.
- Changed source files: `bundle://proof/changed-source-test-file-hashes-final.txt`.
- Production assertions: `bundle://proof/SB02/transcripts/02-source-assertions-final.txt`.
- Red-team negative case: component tests prove the UTXO selector excludes BusinessObject/token spendable outputs from Merge/Split eligibility.
- Downstream dependency check: SB03 browser proof drives the reusable Send, Split, and Merge forms through the AccountApp send dialog.

## Invariant ID: UTXO-FORMS-002

- Invariant ID: UTXO-FORMS-002
- Source raw note: Merge and Split selectors must use available plain UTXOs and calculate totals/output plans.
- Expected behavior: Merge shows selected total and one output; Split supports equal-output generation and editable specific output rows.
- Disallowed shallow implementation: using normal payment auto-selection while only changing labels or allowing BO/token outputs into the selector.
- Failing-first test: process/non-production exemption; SB01 failing-first tests covered missing selected-UTXO semantics, and SB02 validates UI binding to those semantics.
- Passing test: `TransactionComposerShell_RendersUtxoMergeWithOnlyPlainSpendableInputs` and `TransactionUtxoSplitForm_AppliesEqualOutputPlanToDraft` in `bundle://proof/SB02/transcripts/05-passing-reusable-form-ui-tests-after-outpoint-polish.txt`.
- Changed source files: `bundle://proof/changed-source-test-file-hashes-final.txt`.
- Production assertions: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/UtxoSelectionList.razor`, `TransactionUtxoMergeForm.razor`, and `TransactionUtxoSplitForm.razor`.
- Red-team negative case: final browser proof rejects horizontal overflow in the send dialog before submit.
- Downstream dependency check: SB03 proves split details `In 1 / Out 6` and merge details `In 3 / Out 1`.
