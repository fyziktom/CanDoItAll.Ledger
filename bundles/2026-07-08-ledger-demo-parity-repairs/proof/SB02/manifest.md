# SB02 Proof Manifest - SSR Demo Transaction Parity And Closure

## Status

- Subbundle: `SB02`
- Status: `Completed`
- Owned requirements: `R001`, `R002`, `R003`, `R004`, `R005`, `R006`, `R007`, `R008`, `R009`, `R010`, `R011`
- Owned raw notes: `N001`, `N002`, `N003`, `N004`, `N005`, `N006`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` | `17ae251f6db2f5f259a030f4bd932c321ea60de8419770063e82917e7e700cd4` | `f4066226e21b34d766972efd97b62c4bbd894d6e1c2f877f5462ce443022cbd6` |
| `repo://tests/CanDoItAll.Ledger.Tests/SsrDemoParityTests.cs` | `new-file` | `24a53c53d6c30574d0f4315081a6fcdbf6283a507a3065985831d7bcbbabaef4` |

Supporting hash transcript: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-ssr-parity.txt`
- Passing test proof: `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt`
- SSR demo build proof: `bundle://proof/SB02/transcripts/ssr-demo-build.txt`
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions-ssr-parity.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Changed-file hashes: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

## Source-Level Assertions

- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` defaults the SSR BO create draft to `TokenAmount: Amount.FromInt64(1)`.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` seeds the warehouse account with a reusable `USD-CREDIT` token source before creating a warehouse BO.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` delegates UTXO merge/split to `BusinessObjectAccount`.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` already delegates UTXO merge/split to `BusinessObjectAccount` and uses the one-token default for real-node drafts.
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor` exposes distinct `UtxoMerge` and `UtxoSplit` modes.

## Semantic Adequacy Gate

- Shallow-pass trap: an SSR text-only update or a default-only update that leaves seeded SSR BO creation broken.
- Adversarial negative proof: `bundle://proof/SB02/transcripts/failing-first-ssr-parity.txt` failed while the SSR development workspace could not load under the shared carrier-token policy.
- Semantic positive proof: `bundle://proof/SB02/transcripts/passing-ssr-parity-tests.txt` passed with behavior-level one-token draft and explicit UTXO split/merge assertions.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt` found no `TODO`, `NotImplemented`, `throw new NotImplementedException`, or fixture-specific markers in SB02 scope.

## Browser Validation

- Result: `N/A`.
- Rationale: SB02 changed service seeding/defaults and added tests; it did not change rendered markup. The composer mode surface was covered by source and component test assertions.

## Downstream Smoke

- Final closure may proceed because SB01 and SB02 both have manifests, semantic invariant contracts, failing-first proof, passing tests, build proof, source assertions, stub audits, and hashes.
