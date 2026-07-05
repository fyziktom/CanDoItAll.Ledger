# SB01 Semantic Invariants - Explicit UTXO Merge/Split Domain

## Invariant ID: UTXO-MERGE-SPLIT-001

- Invariant ID: UTXO-MERGE-SPLIT-001
- Source raw note: "Merge" and "Split" transactions must merge or split UTXOs, not BusinessObject operations.
- Expected behavior: `UtxoMerge` and `UtxoSplit` use selected plain main-currency UTXOs as transaction inputs and create requested plain transfer outputs.
- Disallowed shallow implementation: relabeling existing `MergeObjects` or `SplitBusinessObjectPayment` tabs without changing submitted vin/vout semantics.
- Failing-first test: `bundle://proof/SB01/transcripts/01-failing-explicit-utxo-tests.txt` shows missing UTXO merge/split request, draft, and account APIs before production changes.
- Passing test: `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt` shows `UTXO-MERGE-SPLIT-001` coverage through `BusinessObjectAdvancedTransactionTests` and `AccountWorkspaceAdapterTests` filter, 28 tests passed.
- Changed source files: `bundle://proof/SB01/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/transcripts/04-source-assertions.txt` cites `UtxoMergeRequest`, `UtxoSplitRequest`, `BuildUnsignedUtxoMergeAsync`, `BuildUnsignedUtxoSplitAsync`, `SelectExplicitPlainMainUtxosAsync`, adapter/session routing, and tests.
- Red-team negative case: `Account_RejectsDisallowedOrInvalidExplicitUtxoSelections` rejects duplicate, token, BO, and overspending selections.
- Downstream dependency check: SB02 can now bind reusable forms to `BusinessObjectTransactionMode.UtxoMerge`, `BusinessObjectTransactionMode.UtxoSplit`, `SelectedUtxos`, and `UtxoSplitOutputs`.

## Invariant ID: UTXO-MERGE-SPLIT-002

- Invariant ID: UTXO-MERGE-SPLIT-002
- Source raw note: Merge "takes all selected utxos as vin and vout is just one to yourself or selected external address."
- Expected behavior: merge preserves the selected input outpoint order and emits exactly one plain output with the selected total to the requested receiver or self.
- Disallowed shallow implementation: using normal payment auto-selection or generating a change output.
- Failing-first test: `bundle://proof/SB01/transcripts/01-failing-explicit-utxo-tests.txt`.
- Passing test: `Account_MergesSelectedPlainUtxosIntoSingleOutput` in `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt`.
- Changed source files: `bundle://proof/SB01/changed-file-hashes.txt`.
- Production assertions: `BuildUnsignedUtxoMergeAsync` in `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`.
- Red-team negative case: token and BO selected UTXOs are rejected before transaction assembly.
- Downstream dependency check: SB03 can assert real transaction details show selected input count and `Out 1`.

## Invariant ID: UTXO-MERGE-SPLIT-003

- Invariant ID: UTXO-MERGE-SPLIT-003
- Source raw note: Split can select one or multiple inputs and create equal or specific output amounts for parallel operations.
- Expected behavior: split consumes selected plain UTXOs and emits requested output rows, returning only positive remainder as self-change.
- Disallowed shallow implementation: splitting one auto-selected UTXO while ignoring selected inputs or silently overspending.
- Failing-first test: `bundle://proof/SB01/transcripts/01-failing-explicit-utxo-tests.txt`.
- Passing test: `Account_SplitsSelectedPlainUtxosIntoRequestedOutputsAndChange` in `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt`.
- Changed source files: `bundle://proof/SB01/changed-file-hashes.txt`.
- Production assertions: `BuildUnsignedUtxoSplitAsync` in `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`.
- Red-team negative case: split output total above selected total is rejected with a "cannot exceed" error.
- Downstream dependency check: SB02 can build equal/specific UI modes by producing `UtxoSplitOutput` rows.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `UtxoMerge` transaction mode and request | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`, `bundle://proof/SB01/transcripts/04-source-assertions.txt` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`, `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` | `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt` submits through account and WASM adapter | `Account_RejectsDisallowedOrInvalidExplicitUtxoSelections` |
| `UtxoSplit` transaction mode and request | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`, `bundle://proof/SB01/transcripts/04-source-assertions.txt` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`, `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` | `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt` submits through account and WASM adapter | Overspend and BO/token selection rejection in `Account_RejectsDisallowedOrInvalidExplicitUtxoSelections` |
