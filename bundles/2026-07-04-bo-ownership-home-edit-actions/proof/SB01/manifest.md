# SB01 Proof Manifest

## Scope

- Subbundle: `SB01 ownership verifier home boundary`
- Invariant: `SB01-HOME-ONLY`
- Status: `Completed`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed Files

| File | Before | After |
| --- | --- | --- |
| `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` | `ddcc056895609b3df1989258cbb6e196e2ff552cf48fb311aa97b1b910b70ebf` | `22c86b69286fe6e69fefe76e741ea78b2d799fd856de59b3f0de488de952f4a8` |
| `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs` | `absent` | `1448df7a8a7233a1d1994dd43e7388c8caff0d785443d794dbf67b4cf1409299` |

## Evidence

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-bo-details-verifier-present.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing-tests-ownership-placement.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Integrated Docker browser proof: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt`
- Browser screenshots: `bundle://proof/SB03/browser/docker-home-security.png` and `bundle://proof/SB03/browser/docker-bo-edit-dialog.png`

| Evidence | Path | Result |
| --- | --- | --- |
| Failing-first red-team check | `bundle://proof/SB01/transcripts/failing-first-bo-details-verifier-present.txt` | `ExitCode: 1`, pre-change BO details scanner existed |
| Targeted component tests | `bundle://proof/SB01/transcripts/passing-tests-ownership-placement.txt` | `ExitCode: 0`, 2 tests passed |
| Source assertions | `bundle://proof/SB01/transcripts/source-assertions-ownership-placement.txt` | `ExitCode: 0`, BO scanner absent and Home verifier retained |
| Anti-stub audit | `bundle://proof/SB01/transcripts/anti-stub-audit.txt` | `ExitCode: 0`, no placeholder/stub markers |
| Hashes | `bundle://proof/SB01/transcripts/changed-file-hashes.txt` | `ExitCode: 0`, hashes recorded |
| Docker browser closure | `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt` | `ExitCode: 0`, Home verifier present and BO details scanner absent |

## Closure Notes

- BO details no longer exposes the ownership verifier scanner entry point.
- BO details still exposes `cda-bo-ownership-qr-button` for the owner to present signed ownership proof.
- Home still exposes `cda-action-security` and `MessageSecurityPanel` still includes ownership payload verification.
- Docker browser proof in SB03 confirms the final integrated app preserves the Home-only verifier boundary.
