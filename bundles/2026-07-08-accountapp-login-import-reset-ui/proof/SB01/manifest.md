# SB01 Proof Manifest

## Subbundle

- Subbundle: `01-account-entry-layout-and-topbar-removal`
- Status: `Completed`
- Owned requirement: remove the redundant unlocked AccountApp top frame and provide the large-screen account-entry baseline used by later import/reset work.

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` | `61C865F348DDF2664FF05ABF21E5D729B877B47E34E64CA368FE53FFAEA57CA1` |
| `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css` | `63F5275A3BE29114CB9E526B1154FD6213847ECD0AB79C069D7B5C14D8E583D6` |
| `repo://tests/CanDoItAll.Ledger.Tests/AccountAppUiSourceContractTests.cs` | `E614D51BC0584A08C6E262925E0C961B6630D4AA273614D3A68264FDF24690BD` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/accountapp-build.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Browser screenshot: `bundle://proof/SB03/screenshots/accountapp-locked-large.png`
- Browser screenshot: `bundle://proof/SB03/screenshots/accountapp-unlocked-no-topbar-large.png`
- Source assertion: `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- Source assertion: `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css`
- Source assertion: `repo://tests/CanDoItAll.Ledger.Tests/AccountAppUiSourceContractTests.cs`

## Closure

- Failing-first: `bundle://proof/SB01/transcripts/failing-first.txt` records the stale-frame assumption failing against current source.
- Semantic positive proof: `bundle://proof/SB01/transcripts/source-assertions.txt` records `TOPBAR-REMOVAL-01` by checking `account-entry`, `Steps`, reset guard hooks, and absence of `account-app__workspace-frame`.
- Passing build proof: `bundle://proof/SB01/transcripts/accountapp-build.txt` records AccountApp build exit code 0.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.
