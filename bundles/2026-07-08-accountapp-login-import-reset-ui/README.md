# AccountApp Login Import Reset UI Repair

This bundle is a coordination and execution package for `2026-07-08-accountapp-login-import-reset-ui`.

## Profile

- `feedback`

## Mission

- Repair the AccountApp browser-account entry experience by removing the useless unlocked top bar, redesigning the locked/create/unlock/import surfaces for large desktop screens, and adding a deliberate reset path for the stored browser vault so users are not trapped when they need to start over.

## Outcome Contract

- Requested outcome: the unlocked AccountApp no longer renders the redundant top workspace frame from the screenshot; the locked first-run screen presents create, unlock, import, and guarded reset flows clearly; import is staged with the shared CanDoItAll `Steps` component; reset/delete-account is hard to trigger accidentally but possible from the locked state.
- Hard constraints: prefer CanDoItAll BaseLib components over ad hoc structure; use the `Steps` component for import; focus on large desktop only; preserve browser vault cryptography/domain behavior; do not copy Neblio-specific VEF logic into this ledger app; use real Playwright browser proof and rebuild dockerized apps before closure.
- Evidence required before closure: prepared and completed bundle validators; targeted component/session tests; AccountApp build; source assertions for top-bar removal, shared-component usage, import steps, and guarded reset; anti-stub audit; large-screen Playwright proof with screenshots for locked setup, import steps, reset guard, and unlocked workspace; docker compose rebuild transcript.
- Known blockers or explicit scope exceptions: small and medium breakpoints are intentionally out of scope per user instruction. The VEF files are source references only; this app imports AccountApp JSON backups and pasted browser key material, not VEF/Neblio backup files.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-account-entry-layout-and-topbar-removal`
2. `subbundles/02-import-steps-and-guarded-reset-flow`
3. `subbundles/03-validation-docker-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
