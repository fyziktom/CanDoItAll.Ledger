# account-entry-layout-and-topbar-removal

## Status

- `Completed`

## Objective

- Remove the unlocked AccountApp top workspace frame from the screenshot and establish the large-screen locked account-entry layout foundation for create/unlock/import/reset flows.

## Success Criteria

- Unlocked `Home.razor` no longer emits `account-app__workspace-frame`.
- Locked large-screen account entry has clear create and unlock sections and leaves import/reset placement ready for SB02.
- AccountApp still builds or any compile issue is explicitly caused by planned SB02 follow-on work.

## Covered Inputs

- `N001`, `N002`, `N003`, `N004`, `N007`
- Requirements: `R001`, `R002`

## Prerequisites

- Prepared-stage bundle validator passes.
- Components MCP evidence for BaseLib layout/Steps is recorded in `analysis/01-current-state.md`.

## Exact Source References

- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css`
- `repo://examples/CanDoItAll.Economy.AccountApp/_Imports.razor`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Pages\MyAccount.razor`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Account\CreateAccount.razor`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Account\UnlockAccount.razor`

## Deliverables

- `account-app__workspace-frame` markup removed from unlocked state.
- Locked state reorganized into a large-screen account-entry workspace with clear create/unlock/import/reset areas.
- Existing create and unlock session actions remain wired.
- CSS revised to support the large-screen layout and remove stale top-frame styling when unused.

## Dependency Impact

- `SB02` depends on this foundation because import/reset controls land inside the locked account-entry workspace.
- `SB03` browser proof depends on this subbundle because the top-bar removal is the primary visual repair.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Remove the unlocked `account-app__workspace-frame` branch and render only backup export, workspace host, and transaction detail below the unlocked root.
2. Redesign locked account-entry markup around shared layout components and stable large-screen areas.
3. Preserve current create and unlock methods and test IDs where practical.
4. Update AccountApp CSS for the large-screen entry surface and remove dead unlocked frame styling if no longer used.

## Scope Exceptions

- Import steps and reset behavior are completed in `SB02`.
- Small and medium screen-specific tuning is out of scope by user instruction.

## Do Not Do

- Do not change cryptographic account creation or unlock behavior.
- Do not add VEF/Neblio compatibility.
- Do not add a new landing page or marketing hero.

## Acceptance Checklist

- Unlocked markup no longer includes `account-app__workspace-frame`.
- Large-screen locked UI has readable create and unlock sections plus import/reset placement for SB02.
- Existing create/unlock button test IDs still exist or are intentionally updated in tests/proof.
- AccountApp project builds after this phase.

## Proof Required

- Source assertion transcript under `bundle://proof/SB01/transcripts/source-assertions.txt`.
- AccountApp build transcript under `bundle://proof/SB01/transcripts/accountapp-build.txt` or deferred to SB03 with a clear note.
- Anti-stub audit transcript under `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.
- `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- Target route: AccountApp `/`.
- Required viewport: large desktop, first pass at approximately `1600x1000`.
- Required Playwright proof: locked page renders account-entry workspace; unlocked page has no `.account-app__workspace-frame`.
- Screenshot paths: `bundle://proof/SB03/screenshots/accountapp-locked-large.png`, `bundle://proof/SB03/screenshots/accountapp-unlocked-no-topbar-large.png`.
- Review questions: text readable, no overlap, no redundant top frame, shared workspace starts at top when unlocked.

## Progression Gate

- Proceed to `SB02` only when source assertions confirm top-frame removal and AccountApp still builds or compile errors are isolated to planned SB02 markup.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
