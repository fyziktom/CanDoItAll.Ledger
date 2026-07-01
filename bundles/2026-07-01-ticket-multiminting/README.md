# Ticket multiminting for AccountApp

This bundle covers adding VEFramework-style multiminting to the Ledger AccountApp ticket minting flow.

## Profile

- `initiative`

## Mission

Bring the practical VEFramework multimint flow into the Ledger BO AccountApp without copying its account-layer implementation: users can create multiple ticket BOs for an event, choose own account, subaccount, address-book, or pasted recipient addresses, submit the resulting signed transactions through the existing adapter path, and validate the result in the real WASM AccountApp.

## Outcome Contract

- Requested outcome: study `VEBlazor/Components/Minting`, propose the integration, implement it in the AccountApp UI, and test a small batch of 5-10 tickets.
- Hard constraints: do not mint 100 tickets during validation; keep BO identity unique per transaction; keep browser signing/API-only WASM behavior; keep SSR and WASM behavior aligned.
- Evidence required before closure: bundle validator pass, focused .NET tests, real AccountApp browser proof with screenshots, and a ledger/API proof showing the minted ticket count and target owner address.
- Known blockers or explicit scope exceptions: browser AccountApp still does not persist newly generated subaccount vaults, so this bundle supports existing `Workspace.SubAccounts` and recipient lists now; persistent child-account creation remains outside this slice.

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

1. `subbundles/01-sb01-veframework-multimint-architecture-mapping`
2. `subbundles/02-sb02-ledger-batch-draft-and-recipient-ui`
3. `subbundles/03-sb03-real-accountapp-validation`

## Dependency And Validation Map

- Source analysis gates UI and transaction design.
- Draft batching gates browser proof.
- Browser proof closes only after screenshots and ledger/API evidence are captured.

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`

## Closure Evidence

- Focused .NET proof: `bundle://proof/transcripts/dotnet-test-ledger-tests.txt` (`237/237` tests passed).
- Real AccountApp proof: `bundle://proof/results/ticket-multimint-result.json`.
- Browser transcript: `bundle://proof/transcripts/playwright-ticket-multimint.txt`.
- Screenshots:
  - `bundle://proof/screenshots/ticket-multimint-event-picker-loaded.png`
  - `bundle://proof/screenshots/ticket-multimint-setup.png`
  - `bundle://proof/screenshots/ticket-multimint-gallery-after-submit.png`
- Source assertions: `bundle://proof/transcripts/source-assertions.txt`.
- Anti-stub audit: `bundle://proof/transcripts/anti-stub-audit.txt`.
