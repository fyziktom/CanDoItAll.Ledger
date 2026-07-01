# AccountApp BO Commerce Flow

This bundle is a coordination and execution package for `bo-commerce-flow`.

## Profile

- `initiative`

## Mission

Add reliable BO long-description handling and implement the AccountApp/shared-component BO commerce flow: sale setup for originals or copies, shareable Buy BO route, browser-signed payment BO, seller refresh dispatch/clone, linked payment/purchase/receipt records, dashboard lists, and Alice/Bob browser validation.

## Outcome Contract

- Requested outcome: Preserve VEFramework UX semantics for BO/NFT sale and purchase while implementing a cleaner typed C#/Blazor architecture in CanDoItAll.Ledger.
- Hard constraints: AccountApp signs in browser, communicates with node only through API, does not copy VEFramework implementation, keeps manual payment/purchase/receipt creation, and proves real Alice/Bob behavior.
- Evidence required before closure: targeted tests, build, Playwright screenshots, raw node state assertions, proof manifests and semantic invariants for critical subbundles, final raw-note closure.
- Known blockers or explicit scope exceptions: VEFramework-specific domain NFTs and Dogecoin/BTC payment paths are out of scope; main-currency purchase flow is required.

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

1. `subbundles/01-sb01-long-description-persistence-and-display`
2. `subbundles/02-sb02-commerce-contracts-and-parsers`
3. `subbundles/03-sb03-sell-share-buy-ui`
4. `subbundles/04-sb04-dispatch-automation`
5. `subbundles/05-sb05-alice-bob-browser-validation`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Complete`
- Subbundle gate review: `SB01-SB05 completed`
- Final closure gate: `Passed`
- Browser validation analytics: `SB01, SB03, SB04, and SB05 passed`
