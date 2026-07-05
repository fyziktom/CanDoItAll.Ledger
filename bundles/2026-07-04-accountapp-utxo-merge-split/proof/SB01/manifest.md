# SB01 Proof Manifest - Explicit UTXO Merge/Split Domain

## Status

- Completed

## Owned Requirements

- R003 exclude BO/token outputs.
- R004 Merge selected vins to one vout.
- R005 Split selected vins.
- R006/R007 support split output rows for equal/specific UI modes.
- R008 reject overspend.
- Transaction-semantics portion of R012.

## Raw Notes

- `N001` Merge/Split tabs are not correct UTXO operations.
- `N002` Merge selector excludes BO unique outputs and calculates one output amount.
- `N003` Merge sends selected UTXOs as vin and one vout to self/external address.
- `N004` Split selects one or more inputs and creates equal/specific outputs.

## Semantic Invariant Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Changed-File Manifest

- `bundle://proof/SB01/changed-file-hashes.txt`
- SHA-256 sample: `53aa63decde12c5cf1dbcb3d399878fe39d814983d1798ae759dbf569cea7ff4` for `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectRequests.cs`.

## Command Transcripts

- Failing-first: `bundle://proof/SB01/transcripts/01-failing-explicit-utxo-tests.txt`
- Passing: `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt`
- Source assertions: `bundle://proof/SB01/transcripts/04-source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/05-anti-stub-audit.txt`

## Source-Level Assertions

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectRequests.cs` defines `UtxoMergeRequest`, `UtxoSplitOutput`, and `UtxoSplitRequest`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` implements `BuildUnsignedUtxoMergeAsync`, `BuildUnsignedUtxoSplitAsync`, and `SelectExplicitPlainMainUtxosAsync`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs` exposes signed/unsigned UTXO merge/split methods.
- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTransactionDraft.cs` exposes `UtxoMerge`, `UtxoSplit`, `SelectedUtxos`, and `UtxoSplitOutputs`.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`, `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs`, and `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` route the new modes to account methods.

## Failing-First Proof

- `bundle://proof/SB01/transcripts/01-failing-explicit-utxo-tests.txt` failed with missing `UtxoMerge`, `UtxoSplit`, `SelectedUtxos`, `UtxoSplitOutputs`, request types, and account builder methods.

## Passing Proof

- `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt` passed the targeted test filter with 28 tests.

## Anti-Stub Audit

- `bundle://proof/SB01/transcripts/05-anti-stub-audit.txt` found no `TODO`, `NotImplemented`, fixture-only, stub, or template markers in SB01 production files.

## Browser, Screenshot, Or Host Proof

- N/A for SB01 domain foundation. Browser proof is required in SB02 and SB03.

## Downstream Smoke Proof

- `WasmWorkspaceAdapter_SubmitsExplicitUtxoSplitAndMergeWithoutNodeSession` in the passing transcript proves the adapter path can split and merge via browser-safe signing.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `UtxoMerge` transaction mode and request | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`, `bundle://proof/SB01/transcripts/04-source-assertions.txt` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`, `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` | `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt` | `Account_RejectsDisallowedOrInvalidExplicitUtxoSelections` |
| `UtxoSplit` transaction mode and request | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`, `bundle://proof/SB01/transcripts/04-source-assertions.txt` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`, `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` | `bundle://proof/SB01/transcripts/03-passing-explicit-utxo-tests-clean.txt` | Overspend and BO/token selection rejection in `Account_RejectsDisallowedOrInvalidExplicitUtxoSelections` |
