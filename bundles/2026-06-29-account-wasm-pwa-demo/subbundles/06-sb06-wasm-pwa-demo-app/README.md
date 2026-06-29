# SB06 WASM PWA Demo App

## Status

- `Completed`

## Objective

- Add the Blazor WASM PWA AccountApp demo that composes the browser vault, account flows, API client, transaction signing path, and shared account UI into a usable application.

## Success Criteria

- New AccountApp project builds and runs as a Blazor WASM PWA.
- App can be launched against a configured node API base URL.
- First-run flow supports create/import/unlock and then loads account API data.
- Account workspace shows transactions, transaction details, spendable outputs, and transaction actions through shared components.
- PWA manifest/service worker assets are valid for installability in the intended demo mode.

## Covered Inputs

- RQ01, RQ02, RQ09, RQ10, RQ18, RQ19, RQ20, RQ23, RQ24, RQ25, RQ26, RQ29, RQ30.

## Prerequisites

- SB04 progression gate passed.
- SB05 progression gate passed.

## Exact Source References

- repo://CanDoItAll.Ledger.slnx
- repo://examples/CanDoItAll.Economy.Components.Demo
- repo://src/CanDoItAll.Economy.Components
- repo://src/CanDoItAll.Economy.Sdk
- repo://src/CanDoItAll.Economy.BusinessObjects
- repo://src/CanDoItAll.Economy.Node
- bundle://architecture/01-target-solution.md
- bundle://plan/account-wasm-pwa-planning.xlsx

## Deliverables

- New Blazor WASM PWA demo project under `examples/`.
- App configuration for node API base URL and browser-safe auth mode.
- DI setup for browser vault, account session, API client, transaction assembly/signing, and shared UI adapter.
- PWA manifest, icons, service worker behavior, and run instructions.
- Browser tests for launch, create/import/unlock, data loading, and signed broadcast smoke flow.
- Workbook status updates.

## Dependency Impact

- SB07 final proof validates this app end to end.
- The app becomes the target integration surface for future account workflows.

## Validation Depth

- UI, PWA, browser-proof, and integration.

## Implementation Steps

1. Create the new Blazor WASM PWA project and add it to the solution.
2. Register required services from SB01 through SB05.
3. Add app shell/routes for setup, unlock, account workspace, settings, and transaction details.
4. Configure PWA manifest/service worker/assets and node API settings.
5. Wire shared components through WASM adapters.
6. Add browser tests and run instructions.
7. Start a local dev server for verification and update proof artifacts.

## Scope Exceptions

- Do not add unrelated product landing pages.
- Keep the first version focused on account demo workflows rather than every possible ledger admin operation.

## Do Not Do

- Do not use SSR node workspace sessions in the WASM app.
- Do not compile server secrets into the PWA.
- Do not add visual-only placeholder flows for required account setup actions.

## Acceptance Checklist

- Project restores/builds in the solution.
- PWA launches in a browser with no console errors from missing assets.
- Account setup flow works through real browser services.
- Account data loads through node API.
- Signed transaction broadcast smoke flow is reachable.
- PWA installability and offline/stale-config behavior are documented.

## Acceptance Result

- `Passed`: AccountApp scoped build passed; Playwright/Chrome proof launched the PWA from a clean browser profile; create, unlock, encrypted backup export/import, pasted-key import, API workspace load, transaction detail, signed broadcast, manifest, and service worker checks passed.

## Proof Required

- `proof/SB06/manifest.md` with command transcript links, changed-file hashes, screenshots, and route map.
- `dotnet build` output for the solution or scoped projects.
- Browser screenshots for first run, unlocked workspace, transaction list/detail, and PWA manifest/install check.
- Network trace proving API-only communication.
- Dependency audit proving forbidden browser dependencies are absent.

## Browser Validation Logging

- Target route: AccountApp local dev URL.
- Viewports: desktop 1440x1000, mobile 390x844.
- Actions: launch, create/import/unlock, load account data, open transaction detail, attempt signed broadcast smoke path, inspect manifest/service worker.
- Screenshots/evidence: `proof/SB06/screenshots/01-first-run-desktop.png`, `proof/SB06/screenshots/02-unlocked-workspace.png`, `proof/SB06/screenshots/03-transaction-detail.png`, `proof/SB06/screenshots/04-signed-broadcast-result.png`, `proof/SB06/screenshots/05-backup-export.png`, `proof/SB06/screenshots/06-imported-backup-unlocked.png`, `proof/SB06/screenshots/07-unlock-stored-vault-after-reload.png`, `proof/SB06/screenshots/08-first-run-mobile.png`, `proof/SB06/screenshots/09-pasted-key-import-mobile.png`.
- Review questions: Is the first screen the usable AccountApp experience, and does it avoid marketing/landing-page detours?

## Progression Gate

- `Passed`: SB07 proceeded after AccountApp build and connected browser proof passed.

## Suggested Agent Prompt

```text
Implement SB06 only. Add the Blazor WASM PWA AccountApp demo using prior subbundle services, prove build/launch/PWA/account data flow in a browser, update the workbook and execution report, and keep scope to the account demo.
```
