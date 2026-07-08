# Assumptions And Risks

## Assumptions

- The working WASM AccountApp behavior in `WasmAccountWorkspaceAdapter` is the parity reference for other demos.
- The repair should favor shared production APIs over new demo-local transaction assembly.
- The console demo can be tested with helper and lesson-level tests without requiring a live node for every assertion.
- The SSR demo can be validated through `EconomyDevelopmentWorkspaceSession` and component/sample adapter tests when a live node is unavailable.

## Critical Path Risks

- If console parity is implemented by only printing new text while still calling old BO split/merge paths, N002 and N004 remain unsolved.
- If SSR development draft keeps using a large token amount for BO create, the demo can teach an invalid carrier-token mental model even though the shared builder rejects invalid BO carriers.
- If UTXO split/merge tests seed BO or token outputs instead of plain main outputs, the proof can pass while missing the key exclusion rule.
- If CodeAnalytics remains unavailable, dependency/cycle proof must be covered by direct project reference inspection, source assertions, and builds.

## Validation Risks

- Live-node console runs are environment dependent because they need a reachable node and external test keys; use compile/source/unit proof as required baseline and mark live-node proof optional unless the environment is already running.
- Browser proof for the SSR demo may be unnecessary if changes are service/test only; if visible copy or sample pages change, capture a Playwright/browser row before closure.
- Existing uncommitted changes in `bundles/2026-07-08-bo-events-tickets-minting`, `BusinessObjectFamilyCatalog.cs`, `BusinessObjectCreationWizard.razor`, and related tests are user-owned and must not be reverted.

## Reopen Triggers

- Reopen SB01 if new console tests show helpers still call BO merge/split for plain UTXO behavior.
- Reopen SB02 if SSR development session cannot submit UTXO split/merge through `BusinessObjectAccount`.
- Reopen the bundle if a required proof transcript is missing, prose-only, or cannot be tied to an invariant id.
- Reopen architecture review if any project reference or partial-class expansion is introduced without a planned checkpoint.
