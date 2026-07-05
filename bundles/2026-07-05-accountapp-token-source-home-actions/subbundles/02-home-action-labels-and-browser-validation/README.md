# Home Action Labels And Browser Validation

## Status

- `Completed`

## Objective

- Show short visible labels on the in-home action button group, preserve icon-only top-frame window actions, and complete build/browser validation for the bundle.

## Covered Inputs

- N003: home page button group should have short labels.
- N003: top bar of window must remain icons only.
- N003: app is now large-screen only.
- N002: final browser proof should confirm the one-card balance/source layout saves space.

## Prerequisites

- SB01 closure gate passes or its blocker is explicitly recorded.
- AccountApp can be built locally.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`
- `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs`

## Deliverables

- In-home action group shows visible short labels for Send, Token, Addresses, Security, Settings, Backup, Refresh, and Subaccount.
- Top-frame AccountApp actions remain icon-only.
- Targeted tests and AccountApp build pass.
- Large-screen browser proof and final raw-note closure are recorded.

## Dependency Impact

- This phase closes the user-visible design request and proves SB01 in the actual AccountApp home composition.
- If top-frame icon-only behavior regresses, N003 cannot close even if labels are visible in the home action group.

## Validation Depth

- UI, component-test, build, browser-proof, and final closure.

## Implementation Steps

1. Update action-group markup or CSS so only `.cda-vef-account-actions` exposes short button labels.
2. Keep `.account-app__frame-actions` icon-only through source or DOM proof.
3. Add or update tests for visible home labels and top-frame constraint where practical.
4. Run targeted tests and AccountApp build.
5. Start or reuse the app, capture large-screen Playwright evidence, and save screenshot artifacts.
6. Update execution report, raw-note closure, proof paths, and completed-stage validator transcript.

## Scope Exceptions

- No mobile-first redesign is required because the raw note explicitly says large-screen only.
- Do not overhaul the top AccountApp shell beyond preserving its icon-only state.

## Do Not Do

- Do not globally make all `PageHeaderActionButton` instances visible text buttons unless the behavior is opt-in.
- Do not add landing-page or marketing UI.
- Do not add nested cards or reintroduce a separate Token Sources card.

## Acceptance Checklist

- Home action buttons display short labels.
- Top-frame action buttons remain icon-only.
- Large-screen screenshot shows readable labels and no incoherent overlap.
- AccountApp build passes.
- Raw notes N001-N003 have closure rows with proof paths.

## Proof Required

- `bundle://proof/SB02/transcripts/passing-component-tests.txt`.
- `bundle://proof/SB02/transcripts/accountapp-build.txt`.
- `bundle://proof/SB02/transcripts/source-assertions.txt`.
- `bundle://proof/SB02/transcripts/playwright-large-home.txt`.
- `bundle://proof/SB02/browser/accountapp-home-large.png`.
- `bundle://proof/SB02/transcripts/completed-stage-validator.txt`.

## Browser Validation Logging

- Route: AccountApp `/` after unlock/import.
- Viewport: `1600x1000` or wider equivalent first pass.
- Playwright actions/assertions: navigate, ensure unlocked workspace, assert the Balances panel exists, assert no standalone Token Sources panel text, assert action labels are visible, assert top-frame toolbar remains icon-only if accessible, screenshot.
- Screenshot review questions: Are home labels readable? Is the card count reduced? Are token-source rows inside Balances? Is there any text overlap or broken large-screen spacing?
- Evidence paths must be recorded in `reviews/01-execution-report.md`.

## Progression Gate

- Final closure may proceed only after tests/build pass and browser validation is recorded or an explicit blocker is documented with replacement evidence.

## Suggested Agent Prompt

```text
Implement SB02 only after SB01 passes. Make home action labels visible without changing top-frame icon-only actions, run tests and build, capture large-screen browser proof, update proof and execution report artifacts, and run the completed-stage validator.
```
