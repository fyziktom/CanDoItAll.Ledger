# import-steps-and-guarded-reset-flow

## Status

- `Completed`

## Objective

- Convert import account into a staged BaseLib `Steps` flow and add a guarded local-vault reset/remove-account path.

## Success Criteria

- Import UI uses `Steps` and `StepsItem` with material, password, and finish stages.
- Existing backup JSON and pasted key pair import modes remain available.
- Reset cannot be submitted until acknowledgement and typed `RESET LOCAL VAULT` are present.
- Session reset deletes the configured local vault and clears account/session state.

## Covered Inputs

- `N003`, `N005`, `N006`
- Requirements: `R003`, `R004`, `R005`, `R006`

## Prerequisites

- `SB01` entry layout foundation has source/build proof or no blocking compile errors.
- Components MCP evidence for `Steps`, `StepsItem`, `Grid`, `Stack`, and `FormSection` is recorded.

## Exact Source References

- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css`
- `repo://src/CanDoItAll.Economy.Cryptography/AccountVault.cs`
- `repo://src/CanDoItAll.Economy.Cryptography.Browser/BrowserIndexedDbAccountVaultStore.cs`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Account\ImportAccount.razor`

## Deliverables

- Import UI uses `Steps`/`StepsItem` with material, passwords, and finish stages.
- Encrypted backup JSON and pasted key pair import remain available and call existing session methods.
- Reset UI exposes a destructive local-vault reset only after acknowledgement plus typed `RESET LOCAL VAULT`.
- Session reset method deletes the local vault and clears account/workspace/transient form state.
- Tests cover reset guard/session behavior and source assertions cover shared component usage.

## Dependency Impact

- `SB03` cannot close without this phase because browser proof must navigate import steps and prove reset cannot be triggered accidentally.

## Validation Depth

- `Critical UI and destructive-behavior foundation`

## Implementation Steps

1. Add reset confirmation state and method to `AccountAppSession`.
2. Add import-mode and step-index state to `Home.razor`.
3. Replace the inline import cards with a BaseLib `Steps` import flow.
4. Add guarded reset markup and disabled/enabled logic.
5. Add or update targeted tests for reset/session and source-level UI contracts.

## Scope Exceptions

- VEF/Neblio backup file parsing remains out of scope; only AccountApp backup JSON and pasted key material are supported.
- Browser proof is captured in `SB03` after all UI changes are in place.

## Do Not Do

- Do not make reset a one-click action.
- Do not delete ledger/node data.
- Do not weaken password confirmation checks.
- Do not invent another wizard component instead of BaseLib `Steps`.

## Acceptance Checklist

- `Home.razor` contains BaseLib `Steps` and `StepsItem` for import.
- Import step 1 separates encrypted backup and pasted key material inputs.
- Import step 2 contains backup/source password plus new vault password and confirmation.
- Import step 3 calls `ImportBackupAsync` or `ImportPastedKeyAsync` based on selected mode.
- Reset button remains disabled until acknowledgement and typed `RESET LOCAL VAULT`.
- `AccountAppSession.ResetLocalVaultAsync` calls `vaultStore.DeleteAsync(Options.VaultId)` and clears account/session state.

## Proof Required

- Targeted test transcript under `bundle://proof/SB02/transcripts/passing-tests.txt`.
- Source assertion transcript under `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Anti-stub audit transcript under `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.
- `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- Target route: AccountApp `/` locked setup.
- Required viewport: large desktop.
- Required Playwright proof: navigate import steps, toggle import modes, assert reset button disabled before confirmation and enabled after acknowledgement plus `RESET LOCAL VAULT`.
- Screenshot paths: `bundle://proof/SB03/screenshots/accountapp-locked-large.png`, `bundle://proof/SB03/screenshots/accountapp-import-password-step-large.png`, `bundle://proof/SB03/screenshots/accountapp-import-finish-step-large.png`, `bundle://proof/SB03/screenshots/accountapp-reset-guard-large.png`.
- Review questions: step labels readable, active step obvious, fields not clipped, reset danger text visible and not visually confused with primary actions.

## Progression Gate

- Proceed to `SB03` only when source/tests prove shared `Steps` import and two-factor reset guard behavior.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
