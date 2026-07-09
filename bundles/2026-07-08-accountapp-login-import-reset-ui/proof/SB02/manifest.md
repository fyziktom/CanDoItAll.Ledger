# SB02 Proof Manifest

## Subbundle

- Subbundle: `02-import-steps-and-guarded-reset-flow`
- Status: `Completed`
- Owned requirement: import is staged through the shared BaseLib `Steps` component and local-vault reset is deliberately guarded by acknowledgement plus the exact `RESET LOCAL VAULT` phrase.

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` | `61C865F348DDF2664FF05ABF21E5D729B877B47E34E64CA368FE53FFAEA57CA1` |
| `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` | `C1082A79E1B64F719157C4ACF8A10F53019FE32D95FCB53939ECC876D37C6391` |
| `repo://examples/CanDoItAll.Economy.AccountApp/_Imports.razor` | `ED41E61CDC23E481531223AFCC4D178DD9D0A2CCE6A3E2EEBE32B52FBEFED7A9` |
| `repo://tests/CanDoItAll.Ledger.Tests/BrowserCryptoVaultTests.cs` | `D2DE13A27A3BE0097D2220FA3A92FCAFDA532AE26C50F2CC035FFB2C4AB9BA66` |
| `repo://tests/CanDoItAll.Ledger.Tests/AccountAppUiSourceContractTests.cs` | `E614D51BC0584A08C6E262925E0C961B6630D4AA273614D3A68264FDF24690BD` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Browser transcript: `bundle://proof/SB03/transcripts/playwright-large-screen.txt`
- Browser screenshot: `bundle://proof/SB03/screenshots/accountapp-import-password-step-large.png`
- Browser screenshot: `bundle://proof/SB03/screenshots/accountapp-import-finish-step-large.png`
- Browser screenshot: `bundle://proof/SB03/screenshots/accountapp-reset-guard-large.png`
- Source assertion: `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- Source assertion: `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`
- Source assertion: `repo://tests/CanDoItAll.Ledger.Tests/BrowserCryptoVaultTests.cs`
- Source assertion: `repo://tests/CanDoItAll.Ledger.Tests/AccountAppUiSourceContractTests.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
|---|---|---|---|---|
| `ResetAcknowledged` and `ResetConfirmation` reset guard | `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` binds checkbox and phrase input; `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` evaluates `CanResetLocalVault` | `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` disables `reset-local-vault` until the service guard is true | User reloads with a stored vault, acknowledges, types `RESET LOCAL VAULT`, deletes the browser vault, and returns to locked no-vault state | `bundle://proof/SB02/transcripts/failing-first.txt` rejects the acknowledgement-only/old-flow assumption; `bundle://proof/SB03/transcripts/playwright-large-screen.txt` proves incomplete phrase stays disabled |

## Closure

- Failing-first: `bundle://proof/SB02/transcripts/failing-first.txt` records stale import/reset assumptions failing against current source.
- Semantic positive proof: `bundle://proof/SB02/transcripts/source-assertions.txt` records `IMPORT-STEPS-01` and `RESET-GUARD-01` source assertions.
- Passing test proof: `bundle://proof/SB02/transcripts/passing-tests.txt` records targeted AccountApp UI source and vault-delete tests passing.
- Browser proof: `bundle://proof/SB03/transcripts/playwright-large-screen.txt` records import step navigation, reset disabled/enabled rules, vault deletion, and zero page errors.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.
