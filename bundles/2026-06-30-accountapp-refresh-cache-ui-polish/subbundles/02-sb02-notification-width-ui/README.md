# SB02 Notification And Desktop Width UI

## Status

- Status: Completed

## Objective

Remove persistent inline transaction/refresh alerts from the full shell and make the unlocked desktop AccountApp use the available browser width.

## Covered Inputs

- RN-03
- RN-04

## Prerequisites

- SB01 closure gate passed or is not contradicted by this UI work.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css`
- `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`

## Deliverables

- Full shell no longer renders `LastResult` or refresh notification inline.
- Notifications remain popup-only through `NotificationService`.
- Unlocked AccountApp uses full desktop width.

## Dependency Impact

- SB03 depends on SB02 for final browser proof.

## Validation Depth

- Critical UI behavior.
- Requires source assertions, tests where practical, and Playwright proof in SB03.

## Implementation Steps

1. Remove or stop passing inline alert parameters in the full-shell path.
2. Ensure successful/rejected tx and auto-refresh messages still use `NotificationService`.
3. Adjust AccountApp CSS so full-shell workspace is not capped at 1600px on large desktop screens.
4. Add source or component test coverage if feasible.

## Do Not Do

- Do not remove notifications entirely.
- Do not degrade setup/import form readability on narrow screens.

## Acceptance Checklist

- [x] No persistent inline `MainPayment ...` result alert in full shell.
- [x] No persistent inline `Auto refresh ...` alert in full shell.
- [x] Desktop full shell width matches available page width.

## Proof Required

- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- Source assertion transcript for removed inline alert rendering and width CSS.
- Passing tests/build transcript.

## Browser Validation Logging

- Large-screen browser screenshots and DOM assertions are captured in SB03.
- SB02 closure must record source assertions for removed inline alerts and width CSS.

## Progression Gate

- SB02 may close only when source/test proof confirms the inline alerts are removed and width cap is no longer applied to the unlocked full-shell surface.

## Suggested Agent Prompt

Implement SB02 only after SB01 proof. Keep notifications as popups and verify the large desktop wrapper is full width.
