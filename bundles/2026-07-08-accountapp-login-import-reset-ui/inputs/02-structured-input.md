# Structured Input

## Core Objective

- Remove redundant unlocked chrome and improve the browser-account entry flows so create, unlock, import, and reset are understandable on large desktop.

## Success Criteria

- The unlocked AccountApp no longer renders the screenshot top bar above the shared workspace.
- Locked large-screen account entry is readable, aligned, and task-oriented.
- Import is a staged BaseLib `Steps` flow.
- Reset/delete local account is possible but guarded by acknowledgement plus typed confirmation.
- Existing create, unlock, encrypted-backup import, and pasted-key import actions still work.
- Playwright proof and docker rebuild are captured before closure.

## Hard Constraints

- Use CanDoItAll BaseLib components where they fit, especially `Steps`.
- Use components MCP guidance before structural markup.
- Large-screen only; small/medium tuning is out of scope.
- Do not add VEF/Neblio import compatibility.
- Do not make reset a one-click destructive action.

## Allowed Side Effects

- AccountApp `Home.razor`, session state, tests, CSS, and bundle proof files may change.
- No ledger/node data model or cryptography changes are expected.

## Source Artifacts

- `bundle://inputs/screenshot-topbar-remove.png`
- VEF Create/Unlock/Import/MyAccount files listed in `inputs/01-source-artifacts.md`
- AccountApp `Home.razor`, `AccountAppSession.cs`, and `app.css`

## Input Coverage Signals

- `N001`: remove screenshot top bar.
- `N002`: improve login/create/import screens.
- `N003`: consult VEF Account components.
- `N004`: no stored account should offer create/import.
- `N005`: reset/remove account must be available but difficult to trigger accidentally.
- `N006`: use BaseLib `Steps` and prefer shared components.
- `N007`: large screen only.
- `N008`: real Playwright proof.
- `N009`: rebuild dockered apps.

## Dependency And Sequencing Signals

- Top-bar removal and locked layout foundation must land before final browser proof.
- Import steps and reset guard must land before docker/closure proof.
- Docker rebuild is a final closure dependency.

## Validation Expectations

- Targeted tests plus AccountApp build.
- Source assertions for top-bar removal, BaseLib `Steps`, and reset guard.
- Real browser proof at large desktop viewport.
- Docker compose rebuild.

## Evidence Contract

- `proof/SBxx/manifest.md` and `semantic-invariants.md` for critical subbundles.
- Command transcripts under `proof/SBxx/transcripts`.
- Browser screenshots under `proof/SB03/browser`.
- Raw-note closure table in `reviews/01-execution-report.md`.

## UI Validation Strategy

- First and only required viewport pass is large desktop, approximately `1600x1000`.
- Review screenshot for readability, overlap, alignment, active step clarity, destructive action distinction, and absence of redundant unlocked top bar.
- Smaller widths are intentionally not part of this request.

## Browser Validation Analytics

- Log route, viewport, Playwright actions, DOM assertions, screenshots, and result for locked setup, import steps, reset guard, and unlocked no-topbar state.

## Working Assumptions

- Reset means local browser vault/session reset, not ledger data deletion.
- AccountApp remains AccountApp-native for imports.

## Primary Risks

- Accidentally removing useful actions with the top bar.
- Reset guard too weak.
- Browser proof polluted by persistent IndexedDB state.
