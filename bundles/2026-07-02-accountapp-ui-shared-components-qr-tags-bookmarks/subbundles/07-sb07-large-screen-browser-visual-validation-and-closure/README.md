# SB07 Large Screen Browser Visual Validation And Closure

## Status

- `Completed`

## Objective

Run the final large-screen AccountApp validation pass, capture screenshots, review visual correctness against the request, repair any failures, and close the bundle with synchronized proof.

## Success Criteria

- Full solution builds and targeted tests pass.
- AccountApp large-screen screenshots prove the old card stack is gone and workspace density is improved.
- QR, tags, address manager, settings backup, and verification dialogs are visually validated.
- Execution report, workbook, traceability, and subbundle gates are updated.
- No overlay, overflow, clipped text, unreadable badge, or excessive long-scroll regression remains in the validated large-screen paths.

## Covered Inputs

- Real app testing requirement.
- Visual screenshot capture and analysis requirement.
- Repair-and-revalidate requirement.
- Large-screen-only scope.
- Whole-plan checklist closure requirement.

## Prerequisites

- SB02 completed.
- SB03 completed.
- SB04 completed.
- SB05 completed.
- SB06 completed.

## Exact Source References

- `repo://examples/CanDoItAll.Economy.AccountApp/Properties/launchSettings.json`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`
- `repo://CanDoItAll.Ledger.slnx`
- `bundle://inputs/artifacts/accountapp-after-login-current.png`
- `bundle://plan/accountapp-ui-shared-components-plan.xlsx`

## Deliverables

- Final test/build command log.
- Final screenshot set.
- Visual analysis summary.
- Repaired UI issues if screenshots fail review.
- Updated workbook checklist.
- Updated execution report with final gate decisions.
- Bundle closure recommendation.
- Docker compose rebuild/reset/seed/smoke/browser proof.

## Dependency Impact

- This is the final downstream closure gate.
- Any failed visual or semantic proof reopens the owning subbundle.

## Validation Depth

- `End-to-end regression and closure`
- Requires build proof, tests, browser screenshots, visual analysis, and artifact synchronization.

## Implementation Steps

1. Confirm SB02-SB06 execution report rows are not pending.
2. Run solution build and targeted tests.
3. Start AccountApp and dependencies using the established repo pattern.
4. Use Playwright/browser tooling to validate large-screen routes and dialogs.
5. Capture screenshots for shell, tags, address manager, settings backup, QR, and verification.
6. Analyze screenshots against spacing, scroll, overflow, tooltip, badge, and dialog criteria.
7. Repair issues and repeat validation when any criterion fails.
8. Update workbook, traceability, and execution report.
9. Mark subbundle gates completed only with evidence paths.

## Scope Exceptions

- Small and medium screen tuning remains deferred.
- Unavailable physical camera can be logged as a manual scan limitation if mocked scan tests pass.

## Do Not Do

- Do not close the bundle with pending production subbundle rows.
- Do not accept screenshots that still show the original vertical card sprawl.
- Do not skip visual review comments even when screenshots exist.

## Acceptance Checklist

- Build command succeeds.
- Targeted tests pass.
- All requested UI surfaces have large-screen screenshots.
- Screenshot review explicitly covers available space, scroll, overlays, overflow, and text clipping.
- Workbook checklist has current statuses and evidence references.
- Execution report raw-note closure rows are complete or explicitly deferred with reason.

## Proof Required

- `dotnet build repo://CanDoItAll.Ledger.slnx`
- Targeted `dotnet test` command proof.
- Screenshot set under `bundle://evidence/SB07/`.
- Docker compose build/up/reset/seed/smoke/browser proof under `bundle://proof/SB07/transcripts/` and `bundle://proof/SB07/screenshots/docker-accountapp-alice-imported-large.png`.
- Updated `bundle://plan/accountapp-ui-shared-components-plan.xlsx`.
- Updated `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route: AccountApp home after unlock, BO create/details, transaction details, send dialog, address manager, settings backup, verification.
- Viewport: 1920x1080 or maximized large-screen browser.
- Actions: run each required workflow, capture screenshots, inspect for overflow/overlay/clipping, repeat after repairs.
- Screenshots: `bundle://evidence/SB07/accountapp-final-home-large.png`, `bundle://evidence/SB07/dialogs-final-large.png`, `bundle://evidence/SB07/verification-final-large.png`.
- Review questions: Does the final UI satisfy the screenshot complaint; are management flows dialog-based; are controls compact/tooltipped; are shared components visible; are there regressions?

## Progression Gate

- Bundle final closure is allowed only when all evidence rows in the execution report have concrete proof paths and no critical screenshot review failure remains.

## Suggested Agent Prompt

```text
Implement SB07 only after SB02-SB06 are complete. Run the final build/tests, capture and review all large-screen screenshots, repair any visual failures, update the workbook and execution report, and close only when the evidence is complete.
```

