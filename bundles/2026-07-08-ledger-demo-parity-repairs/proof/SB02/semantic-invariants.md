# SB02 Semantic Invariants - SSR Demo Transaction Parity

## Invariant ID: SSR-PARITY-001

- Invariant ID: SSR-PARITY-001
- Source raw note: `N003` - "how BO works (1 token without main currency amount)" and `N005` - "analyze our ... blazor ssr demo ... updated".
- Expected behavior: the seeded SSR development workspace loads successfully under the shared BO carrier-token policy and defaults BO creation drafts to one carrier token with zero main amount on the draft.
- Disallowed shallow implementation: changing only visible text or only the draft amount while seeded SSR accounts still cannot create BOs with reusable carrier-token sources.
- Failing-first test and transcript: `bundle://proof/SB02/transcripts/failing-first-ssr-parity.txt` shows `SsrDevelopmentWorkspace_DefaultsBusinessObjectDraftToOneCarrierToken` and the split/merge behavior test failed before implementation because the seeded warehouse account lacked a reusable non-BO carrier-token source.
- Passing test and transcript: `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt` shows `SsrDemoParityTests` passed after the development draft default changed to `1` and the warehouse seed received a reusable `USD-CREDIT` carrier source.
- Changed source files and hashes: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-ssr-parity.txt` proves the development draft uses `TokenAmount: Amount.FromInt64(1)` and the warehouse seed has a reusable token source.
- Red-team negative case: the failing-first transcript rejects an SSR demo that cannot load its development workspace after the shared BO one-token policy is enforced.
- Downstream dependency check: final bundle closure can rely on the SSR demo workspace loading and using BO carrier-token defaults because `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt` exercises the seeded session end to end.

## Invariant ID: SSR-PARITY-002

- Invariant ID: SSR-PARITY-002
- Source raw note: `N004` - "We improved split and merge transaction" and `N005` - "analyze our ... blazor ssr demo ... updated".
- Expected behavior: SSR development and real-node session paths route explicit UTXO split/merge through `BusinessObjectAccount` and produce the same selected vin/vout shape as the working WASM AccountApp path.
- Disallowed shallow implementation: hand-assembling SSR-only transactions, relabeling BO split/merge as UTXO split/merge, or accepting UTXO modes without passing selected outpoints to shared account APIs.
- Failing-first test and transcript: `bundle://proof/SB02/transcripts/failing-first-ssr-parity.txt` shows the development workspace could not reach the split/merge path before the seed repair.
- Passing test and transcript: `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt` proves an explicit plain UTXO split selects one input and creates requested `40` and `60` outputs, then an explicit merge of those outputs creates exactly one plain `100` output to the selected receiver.
- Changed source files and hashes: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-ssr-parity.txt` proves development and real-node sessions delegate `UtxoMerge` and `UtxoSplit` to `account.MergeUtxosAsync` and `account.SplitUtxosAsync`.
- Red-team negative case: the test would fail if the SSR path ignored selected outpoints, returned token/BO outputs for plain UTXO merge, or produced more than one merge vout.
- Downstream dependency check: final closure can rely on shared `BusinessObjectAccount` semantics because SSR development and real-node session source assertions cite shared account API delegation.

## Invariant ID: SSR-PARITY-003

- Invariant ID: SSR-PARITY-003
- Source raw note: `N001` - working WASM AccountApp is the reference path.
- Expected behavior: the shared transaction composer keeps `UtxoMerge` and `UtxoSplit` as distinct modes for SSR/demo consumers.
- Disallowed shallow implementation: hiding UTXO modes behind generic transfer UI or collapsing them into old BO split/merge modes.
- Passing test and transcript: `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt` includes `SsrTransactionComposer_ExposesDistinctUtxoMergeAndSplitModes`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-ssr-parity.txt` proves the composer has distinct merge and split mode options and render cases.
- Downstream dependency check: no browser run is required because no rendered markup changed and component/source assertions prove the existing SSR surface still exposes both modes.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `SsrDevelopmentWorkspaceCarrierSeed` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs`; `bundle://proof/SB02/transcripts/source-assertions-ssr-parity.txt` | `repo://tests/CanDoItAll.Ledger.Tests/SsrDemoParityTests.cs`; `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt` | `bundle://proof/SB02/transcripts/ssr-demo-build.txt` | `bundle://proof/SB02/transcripts/failing-first-ssr-parity.txt` |
| `SsrExplicitUtxoSplitMerge` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs`; `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` | `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor`; `repo://tests/CanDoItAll.Ledger.Tests/SsrDemoParityTests.cs` | `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt`; `bundle://proof/SB02/transcripts/ssr-demo-build.txt` | `bundle://proof/SB02/transcripts/failing-first-ssr-parity.txt` |
