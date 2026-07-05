# Execution Report

## Status

- Execution state: `Complete`

## Outcome Check

- Requested outcome: home send dialog has reusable Send/Merge/Split forms, Merge/Split perform explicit UTXO transactions, docker-backed UI proof confirms all transaction types, and details dialogs handle many inputs/outputs.
- Current closure decision: `Pass`
- Evidence still missing: none.

## Commands

- SB01 failing-first domain/adapter tests: `bundle://proof/SB01/transcripts/01-failing-explicit-utxo-tests.txt`.
- SB01 passing domain/adapter tests: `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt`.
- SB01 source assertions: `bundle://proof/SB01/transcripts/04-source-assertions.txt`.
- SB01 anti-stub audit: `bundle://proof/SB01/transcripts/05-anti-stub-audit.txt`.
- SB02 final UI component tests: `bundle://proof/SB02/transcripts/05-passing-reusable-form-ui-tests-after-outpoint-polish.txt`.
- SB02 source assertions: `bundle://proof/SB02/transcripts/02-source-assertions-final.txt`.
- SB02 anti-stub audit: `bundle://proof/SB02/transcripts/03-anti-stub-audit-final.txt`.
- Final Docker rebuild/restart: `bundle://proof/SB03/transcripts/14-docker-compose-rebuild-accountapp-after-outpoint-polish.txt`.
- Final Docker status: `bundle://proof/SB03/transcripts/14b-docker-compose-ps-after-outpoint-polish.txt`.
- Final Send/Split/Merge browser proof: `bundle://proof/SB03/transcripts/16-playwright-accountapp-send-split-merge-final.txt`.
- Final targeted regression tests: `bundle://proof/transcripts/06-targeted-regression-tests-final.txt` with 34 passed, 0 failed.
- Completed-stage validator: `bundle://proof/transcripts/11-completed-stage-validator-after-final-status.txt`.

## Browser Artifacts

- Result JSON: `bundle://proof/SB03/playwright-results.json`.
- Send form screenshot: `bundle://proof/SB03/screenshots/00-send-dialog.png`.
- Send details screenshot: `bundle://proof/SB03/screenshots/00-send-details-io.png`.
- Split form screenshot: `bundle://proof/SB03/screenshots/01-split-dialog.png`.
- Split details screenshot: `bundle://proof/SB03/screenshots/02-split-details-io.png`.
- Merge form screenshot: `bundle://proof/SB03/screenshots/03-merge-dialog.png`.
- Merge details screenshot: `bundle://proof/SB03/screenshots/04-merge-details-io.png`.

## Transaction Proof

- Send transaction `2e960d76bee085b18a7450fd99e6f63458e00148a554f4316c5f1bb6c9bd3d4f`: 1 input, 2 outputs.
- Split transaction `f6783209dc6789805bd5feb0d22bddcdfa84b9c82ba8e1153a63641c2481f9b8`: 1 input, 6 outputs.
- Merge transaction `0bc89af693783810fd931c6e069430dbe080ee1de2d75f31b3689e8cc9de4ca9`: 3 inputs, 1 output.

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| `SB01` | `Pass` | `Pass` | `SB02 can bind to UtxoMerge/UtxoSplit draft fields and account methods` | `Proceed to SB02` | Semantic adequacy evidence: `bundle://proof/SB01/semantic-invariants.md`; manifest `bundle://proof/SB01/manifest.md`. |
| `SB02` | `Pass` | `Pass` | `SB03 can drive reusable Send/Merge/Split forms and details layout is contained` | `Proceed to SB03` | Semantic adequacy evidence: `bundle://proof/SB02/semantic-invariants.md`; manifest `bundle://proof/SB02/manifest.md`. |
| `SB03` | `Pass` | `Pass` | `All raw notes closed with docker/browser proof` | `Complete` | Semantic adequacy evidence: `bundle://proof/SB03/semantic-invariants.md`; manifest `bundle://proof/SB03/manifest.md`; red-team verifier `bundle://proof/SB03/red-team-verifier.md`. |

## SB01 Semantic Adequacy Evidence

- Raw note owned: N001-N004 explicit UTXO merge/split semantics.
- Shipped behavior: domain/account/session paths build UTXO Merge and UTXO Split transactions from explicit selected plain main-currency outpoints.
- Source proof: `bundle://proof/SB01/transcripts/04-source-assertions.txt` and `bundle://proof/SB01/semantic-invariants.md`.
- Test proof: `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt`.
- Shallow-pass trap: relabeling old BusinessObject merge/split tabs would not satisfy selected vin/vout tests.
- Adversarial negative proof: `Account_RejectsDisallowedOrInvalidExplicitUtxoSelections` rejects duplicate, missing, BO, token, and overspending selections.
- Semantic positive proof: `Account_MergesSelectedPlainUtxosIntoSingleOutput` and `Account_SplitsSelectedPlainUtxosIntoRequestedOutputsAndChange`.
- Anti-stub audit: no stubs in `bundle://proof/SB01/transcripts/05-anti-stub-audit.txt`.

## SB02 Semantic Adequacy Evidence

- Raw note owned: N001, N002, N004, N006, and UI foundation for N008.
- Shipped behavior: Send, UTXO Merge, and UTXO Split are reusable Razor form components using workspace spendable outputs and draft fields.
- Source proof: `bundle://proof/SB02/transcripts/02-source-assertions-final.txt` and `bundle://proof/SB02/semantic-invariants.md`.
- Test proof: `bundle://proof/SB02/transcripts/05-passing-reusable-form-ui-tests-after-outpoint-polish.txt`.
- Shallow-pass trap: a monolithic switch or BO merge/split relabel would not render the reusable components or pass selector eligibility tests.
- Adversarial negative proof: component tests prove BO/token spendable outputs are excluded from UTXO selector eligibility.
- Semantic positive proof: final dialog screenshots `bundle://proof/SB03/screenshots/00-send-dialog.png`, `01-split-dialog.png`, and `03-merge-dialog.png`.
- Anti-stub audit: no stubs in `bundle://proof/SB02/transcripts/03-anti-stub-audit-final.txt`.

## SB03 Semantic Adequacy Evidence

- Raw note owned: N003, N005, N007, N008, and final closure for all notes.
- Shipped behavior: rebuilt Docker stack submits real Send, Split, and Merge transactions through AccountApp and opens confirmed transaction details.
- Source proof: `bundle://proof/SB03/playwright-results.json`, `bundle://proof/SB03/manifest.md`, and `bundle://proof/SB03/semantic-invariants.md`.
- Test proof: `bundle://proof/SB03/transcripts/16-playwright-accountapp-send-split-merge-final.txt`.
- Shallow-pass trap: screenshots alone or stale containers would not satisfy txid and vin/vout assertions.
- Adversarial negative proof: `bundle://proof/SB03/red-team-verifier.md`.
- Semantic positive proof: Send tx `2e960d76bee085b18a7450fd99e6f63458e00148a554f4316c5f1bb6c9bd3d4f`, Split tx `f6783209dc6789805bd5feb0d22bddcdfa84b9c82ba8e1153a63641c2481f9b8`, and Merge tx `0bc89af693783810fd931c6e069430dbe080ee1de2d75f31b3689e8cc9de4ca9`.
- Anti-stub audit: no stubs in `bundle://proof/SB02/transcripts/03-anti-stub-audit-final.txt`.

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| `SB02` | AccountApp home send dialog | `1920x1080` | Open dialog, select Send/Merge/Split tabs, inspect form layout | `bundle://proof/SB03/screenshots/00-send-dialog.png`, `01-split-dialog.png`, `03-merge-dialog.png` | `Pass` |
| `SB03` | AccountApp home and transaction details dialogs | `1920x1080` | Submit Send/Split/Merge, open transaction details, assert vin/vout counts | `bundle://proof/SB03/screenshots/*.png` | `Pass` |

## Analytics Review

- Large desktop only, per request.
- No browser console warnings/errors recorded in `playwright-results.json`.
- Details cards contain long outpoints/addresses without horizontal overflow in the proof runner DOM assertions.
- Visual review of final screenshots shows scrollable output rows for Split and multiple input cards for Merge without overlay or clipped lateral content.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| `N001` Merge/Split tabs are not correct UTXO operations | `Solved` | SB01 explicit transaction modes and SB02 reusable UTXO forms. |
| `N002` Merge selector excludes BO unique outputs and calculates one output amount | `Solved` | `TransactionUtxoMergeForm`, `UtxoSelectionList`, SB02 tests, final merge screenshot. |
| `N003` Merge sends selected UTXOs as vin and one vout to self/external address | `Solved` | Merge tx `0bc89af693783810fd931c6e069430dbe080ee1de2d75f31b3689e8cc9de4ca9` with 3 inputs and 1 output. |
| `N004` Split selects one or more inputs and creates equal/specific outputs | `Solved` | `TransactionUtxoSplitForm`, SB02 tests, split tx `f6783209dc6789805bd5feb0d22bddcdfa84b9c82ba8e1153a63641c2481f9b8` with 1 input and 6 outputs. |
| `N005` Split supports preparing many UTXOs for parallel operations | `Solved` | Split proof creates five requested outputs plus change and displays six outputs in details. |
| `N006` Send/Merge/Split forms are reusable isolated components | `Solved` | SB02 manifest and component tests. |
| `N007` UI tests all transaction types after docker rebuild/restart | `Solved` | Final Docker rebuild and `16-playwright-accountapp-send-split-merge-final.txt`. |
| `N008` Transaction details screenshots prove many inputs/outputs do not overflow/overlay | `Solved` | Final send/split/merge details screenshots and DOM overflow assertions. |

## Residual Risks

- Existing package warnings remain: `NU1701` for `ncalc`, `NU1903` for `SQLitePCLRaw.lib.e_sqlite3`, and `NU1510` for an unnecessary package reference. They predate this bundle and did not block tests or Docker proof.
