# AccountApp UI Tuning And Ledger Shared Components

This bundle implements the large-screen AccountApp WASM PWA UI tuning and isolates reusable ledger UI/services for related CanDoItAll ledger apps.

## Profile

- `initiative`

## Mission

Deliver a dense, component-led AccountApp desktop workspace: remove the post-login card stack, move routine confirmations into notifications, consolidate account actions into a sticky top workspace bar, introduce shared QR generation/scanning, standardize tags/address management/settings backup, and prove each UI change with browser screenshots.

## Outcome Contract

- Requested outcome: architecture design, whole execution plan, detailed checklist workbook, QR feasibility proof, production implementation, and validation proof.
- Hard constraints: prefer `CanDoItAll.Components`; avoid custom CSS where prepared components fit; use dialogs instead of accordions for management surfaces; focus large-screen layouts only; preserve existing AccountApp signing and ledger behavior.
- Evidence required before closure: build/test commands, source-backed semantic proof, large-screen Playwright screenshots, screenshot review notes, and workbook-backed checklist closure.
- Known blockers or explicit scope exceptions: mobile and medium-screen layout tuning is out of scope for this bundle.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input.
- `analysis/` current-state analysis, assumptions, and risks.
- `requirements/` normalized, testable requirements.
- `architecture/` target solution and boundaries.
- `plan/` execution order, dependency map, and workbook.
- `traceability/` requirement-to-subbundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` preparation self-review and execution report.
- `proof/` implementation transcripts, screenshots, workbook previews, and final validation proof.

## Recommended Execution Order

1. `subbundles/01-sb01-shared-component-boundary-and-qr-foundation`
2. `subbundles/02-sb02-desktop-account-shell-and-top-toolbar-polish`
3. `subbundles/03-sb03-tag-occurrence-service-and-shared-tag-rendering`
4. `subbundles/04-sb04-generic-address-manager-dialog-bookmarks-and-subaccounts`
5. `subbundles/05-sb05-browser-settings-backup-import-export`
6. `subbundles/06-sb06-verification-driver-qr-scan-and-ticket-ownership-flows`
7. `subbundles/07-sb07-large-screen-browser-visual-validation-and-closure`

## Dependency And Validation Map

- The dependency map and phase gates are in `plan/01-phase-plan.md`.
- The detailed implementation checklist is in `plan/accountapp-ui-shared-components-plan.xlsx`.
- QR feasibility and implementation proof is in `proof/SB01/` and `proof/SB06/`.
- If resumed by another agent, start with this README, `reviews/01-execution-report.md`, and the current subbundle README.

## Validation Summary

- Bundle preparation status: `Completed`
- Bundle readiness gate: `Validated after implementation`
- Execution status: `Completed across SB01-SB07`
- Subbundle gate review: `All seven subbundles marked Completed with proof paths`
- Final closure gate: `Passed with build/test transcripts, source assertions, workbook closure, and large-screen screenshots`
- Browser validation analytics: `Captured for AccountApp shell, tooltips, tags, address manager, settings backup, QR generation/scanning, ownership verification, signed ticket ownership, and used-ticket state`
- Docker validation: `Passed after image rebuild, compose recreation, node reset, seed/repeat-seed, HTTP smoke, and Docker-hosted AccountApp browser import screenshot`

## Final Proof Pointers

- Execution report: `reviews/01-execution-report.md`
- Detailed checklist workbook: `plan/accountapp-ui-shared-components-plan.xlsx`
- Evidence manifest: `proof/00-evidence-manifest.md`
- Final home screenshot: `proof/SB07/screenshots/accountapp-final-home-large.png`
- Docker AccountApp screenshot: `proof/SB07/screenshots/docker-accountapp-alice-imported-large.png`
