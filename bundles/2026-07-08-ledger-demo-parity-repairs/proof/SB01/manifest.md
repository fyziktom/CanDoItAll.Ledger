# SB01 Proof Manifest - Console Demo Transaction Parity

## Status

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: `R001`, `R002`, `R003`, `R004`, `R005`, `R006`, `R007`, `R010`, `R011`
- Owned raw notes: `N001`, `N002`, `N003`, `N004`, `N005`, `N006`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs` | `0d23433a691a50399e44e17228d5db74002789de2806f00d892ddbc46b27aa7f` | `19d2c6a7ca95634bd7536a44c2da6b8938f4eaebed56beba52c7c0f3155b5733` |
| `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs` | `2d8f90bf9a482153ee30fcccc990aeda99e798f904b2d9ac6db363ca956eea54` | `e59ee7f86872883a58641e2903be9f6900d0ef77cc6171f6d8a47816253c2d71` |
| `repo://examples/CanDoItAll.Economy.ConsoleDemo/README.md` | `a72238bc79b9a7e003e055947131d9ef320f464864de1a6240e46c3c9f96c882` | `7431291412d5894cb12989c6f7e7576e59bec1b7f5094c8ebff0de9d32f874bb` |
| `repo://tests/CanDoItAll.Ledger.Tests/ConsoleDemoParityTests.cs` | `new-file` | `c5258e51d6abb6c3b8aee07fef1bcfa690b5848e0d15ab77620beb183467d66b` |

Supporting hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-console-parity.txt`
- Passing test proof: `bundle://proof/SB01/transcripts/passing-console-parity-tests.txt`
- Console project build proof: `bundle://proof/SB01/transcripts/console-demo-build.txt`
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions-console-parity.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Changed-file hashes: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Source-Level Assertions

- `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs` exposes `SplitUtxosAsync` and `MergeUtxosAsync`.
- `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs` delegates to `Account.SplitUtxosAsync` and `Account.MergeUtxosAsync`.
- `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs` calls the plain UTXO helpers before advanced BO split payment and BO merge examples.
- `repo://examples/CanDoItAll.Economy.ConsoleDemo/README.md` documents UTXO split, UTXO merge, one-token BO carrier, and zero main BO output semantics.

## Semantic Adequacy Gate

- Shallow-pass trap: a README-only change or relabeling old BO split/merge as UTXO split/merge.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-console-parity.txt` failed when helpers and lesson/source text were missing.
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing-console-parity-tests.txt` passed after helper, lesson, and README parity changes.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` found no `TODO`, `NotImplemented`, `throw new NotImplementedException`, or fixture-specific markers in SB01 scope.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ConsoleDemoUtxoFacade` | `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs`; `bundle://proof/SB01/transcripts/source-assertions-console-parity.txt` | `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs`; `bundle://proof/SB01/transcripts/passing-console-parity-tests.txt` | `bundle://proof/SB01/transcripts/console-demo-build.txt` | `bundle://proof/SB01/transcripts/failing-first-console-parity.txt` |

## Downstream Smoke

- SB02 may start because SB01 proof shows the console demo now uses the same shared account APIs that SSR sessions already route through.
