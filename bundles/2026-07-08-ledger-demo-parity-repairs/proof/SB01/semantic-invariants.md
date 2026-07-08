# SB01 Semantic Invariants - Console Demo Transaction Parity

## Invariant ID: CONSOLE-PARITY-001

- Invariant ID: CONSOLE-PARITY-001
- Source raw note: `N004` - "We improved split and merge transaction" and `N005` - "analyze our console app demo ... updated".
- Expected behavior: the console demo exposes plain UTXO split and merge through helper methods that call `BusinessObjectAccount.SplitUtxosAsync` and `BusinessObjectAccount.MergeUtxosAsync`.
- Disallowed shallow implementation: adding console text that says "split" and "merge" while still calling only `SplitBusinessObjectPaymentAsync` and `MergeBusinessObjectsAsync`.
- Failing-first test and transcript: `bundle://proof/SB01/transcripts/failing-first-console-parity.txt` shows `ConsoleDemoDriver_ExposesExplicitUtxoSplitMergeFacadeOverAccount` failed before helper implementation.
- Passing test and transcript: `bundle://proof/SB01/transcripts/passing-console-parity-tests.txt` shows `ConsoleDemoParityTests` passed after implementation.
- Changed source files and hashes: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-console-parity.txt` proves `DemoAccountDriver` delegates to shared account APIs and does not assemble raw `LedgerTransaction`.
- Red-team negative case: `ConsoleDemoLesson7_DistinguishesPlainUtxoSplitMergeFromBusinessObjectOperations` rejects the shallow implementation where plain UTXO split/merge are absent or conflated with BO split/merge.
- Downstream dependency check: SB02 can rely on the same parity language and shared account API ownership when closing SSR parity.

## Invariant ID: CONSOLE-PARITY-002

- Invariant ID: CONSOLE-PARITY-002
- Source raw note: `N003` - "how BO works (1 token without main currency amount)".
- Expected behavior: console documentation and lesson text state that BO outputs are one-token carrier outputs with zero main amount, while main currency is represented by separate plain outputs.
- Disallowed shallow implementation: changing only tests or source comments without teaching the console demo user the BO carrier rule.
- Failing-first test and transcript: `bundle://proof/SB01/transcripts/failing-first-console-parity.txt` shows `ConsoleDemoReadme_DocumentsWasmParityTransactionSemantics` failed before README parity text.
- Passing test and transcript: `bundle://proof/SB01/transcripts/passing-console-parity-tests.txt` shows README/source parity assertions passed.
- Changed source files and hashes: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-console-parity.txt` proves README and lesson text contain the required parity semantics.
- Red-team negative case: the source assertion fails when README omits `one-token` or `zero main`.
- Downstream dependency check: SB02 must independently prove the SSR development draft uses `TokenAmount: 1`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ConsoleDemoUtxoFacade` | `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs`; `bundle://proof/SB01/transcripts/source-assertions-console-parity.txt` | `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs`; `bundle://proof/SB01/transcripts/passing-console-parity-tests.txt` | `bundle://proof/SB01/transcripts/console-demo-build.txt` proves the console project compiles with the facade | `bundle://proof/SB01/transcripts/failing-first-console-parity.txt` proves missing facade failed |
