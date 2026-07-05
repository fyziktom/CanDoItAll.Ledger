# AccountApp Token Sources And Home Actions

This bundle is a coordination and execution package for `2026-07-05-accountapp-token-source-home-actions`.

## Profile

- `feedback`

## Mission

- Make the AccountApp home workspace show only real token sources, fold those token sources into the existing Balances card to save space, and expose short visible labels on the in-home action button group while leaving the top window frame actions icon-only.

## Outcome Contract

- Requested outcome: AccountApp home balances show the main currency and token-source UTXOs in one compact card; unique one-amount tokens and BO-backed outputs are not labeled or shown as token sources; home action buttons have short visible labels.
- Hard constraints: use the repo's existing shared Blazor component patterns; preserve top-frame icon-only command buttons in `examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`; do not change token-selection domain rules beyond display filtering.
- Evidence required before closure: targeted bUnit/component tests, AccountApp build, source assertions for the token-source predicate and action label visibility, anti-stub audit, and large-screen browser proof of the unlocked home page.
- Known blockers or explicit scope exceptions: CanDoItAll components MCP was unavailable during preparation due a closed transport, so execution uses existing repo component usages and source inspection as the fallback component inventory.

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

1. `subbundles/01-token-source-and-balance-card-behavior`
2. `subbundles/02-home-action-labels-and-browser-validation`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `SB01 passed; SB02 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
