# SB02 Semantic Invariants

## Invariant IMPORT-STEPS-01

- Invariant ID: `IMPORT-STEPS-01`
- Source raw note: Import should be implemented as steps with the CanDoItAll BaseLib component.
- Expected behavior: Import uses `<Steps>` with `StepsItem` pages for Import, Password, and Finish; browser interaction can move from material entry to password entry to review.
- Disallowed shallow implementation: Static headings that look like steps, ad hoc tabs, or a single form with no guarded step progression.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.txt` asserts the old non-Steps assumption and records the expected non-zero result.
- Passing test: `bundle://proof/SB02/transcripts/source-assertions.txt` checks `IMPORT-STEPS-01`; `bundle://proof/SB03/transcripts/playwright-large-screen.txt` navigates backup/key-pair mode and the password/review steps.
- Changed source files: `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` with hash `61C865F348DDF2664FF05ABF21E5D729B877B47E34E64CA368FE53FFAEA57CA1`; `repo://examples/CanDoItAll.Economy.AccountApp/_Imports.razor` with hash `ED41E61CDC23E481531223AFCC4D178DD9D0A2CCE6A3E2EEBE32B52FBEFED7A9`.
- Production assertions: `Home.razor` renders BaseLib `Steps`, `StepsItem`, and step navigation test ids; `bundle://proof/SB03/screenshots/accountapp-import-password-step-large.png` and `bundle://proof/SB03/screenshots/accountapp-import-finish-step-large.png` capture the large-screen browser flow.
- Red-team negative case: A source check that accepts import without `Steps` fails in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Downstream dependency check: SB03 Playwright proof uses the real step buttons and proceeds only after required import material/password fields.

## Invariant RESET-GUARD-01

- Invariant ID: `RESET-GUARD-01`
- Source raw note: Account removal/reset must be possible but difficult to trigger accidentally.
- Expected behavior: Reset is disabled unless a vault exists, the user checks acknowledgement, and the user types `RESET LOCAL VAULT`; reset deletes only the local browser vault and returns the app to the no-vault locked state.
- Disallowed shallow implementation: A visible delete button enabled by default, a reset gated by acknowledgement alone, or a reset that clears UI fields without calling the vault store delete path.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.txt` asserts the old acknowledgement-only behavior and records the expected non-zero result.
- Passing test: `bundle://proof/SB02/transcripts/passing-tests.txt` covers `Account_vault_delete_removes_stored_record`; `bundle://proof/SB03/transcripts/playwright-large-screen.txt` verifies incomplete phrase disabled, exact phrase enabled, and reset disables unlock afterward.
- Changed source files: `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` with hash `C1082A79E1B64F719157C4ACF8A10F53019FE32D95FCB53939ECC876D37C6391`; `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` with hash `61C865F348DDF2664FF05ABF21E5D729B877B47E34E64CA368FE53FFAEA57CA1`; `repo://tests/CanDoItAll.Ledger.Tests/BrowserCryptoVaultTests.cs` with hash `D2DE13A27A3BE0097D2220FA3A92FCAFDA532AE26C50F2CC035FFB2C4AB9BA66`.
- Production assertions: `AccountAppSession.ResetLocalVaultAsync` calls `vaultStore.DeleteAsync(Options.VaultId, ...)`, clears session state, and sets `HasStoredVault` false; `Home.razor` binds reset controls to `CanResetLocalVault`.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` rejects a reset guard with no exact phrase requirement.
- Downstream dependency check: `bundle://proof/SB03/transcripts/playwright-large-screen.txt` proves the browser-level reset lifecycle before final Docker rebuild.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
|---|---|---|---|---|
| `ResetAcknowledged` and `ResetConfirmation` reset guard | `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` writes user inputs and `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` computes `CanResetLocalVault` | `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` disables/enables `reset-local-vault`; `ResetLocalVaultAsync` consumes the guard before deleting | Stored vault found, user acknowledges, exact phrase enables reset, vault delete runs, app returns to locked no-vault state | `bundle://proof/SB02/transcripts/failing-first.txt` and `bundle://proof/SB03/transcripts/playwright-large-screen.txt` reject reset without exact phrase |
