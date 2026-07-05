# BO Ownership Home And Edit Actions

This bundle is a coordination and execution package for `2026-07-04-bo-ownership-home-edit-actions`.

## Profile

- `feedback`

## Mission

- Move BO ownership verification scanning out of BO details actions and keep it on the Home message-security tool surface, then add a BO details edit action that opens the same BO form used for minting and stages an update transaction for the selected BO UTXO.

## Outcome Contract

- Requested outcome: ownership verification is a Home-only wallet-owner tool, and each visible BO can be edited from BO details by staging a `BusinessObjectTransactionMode.UpdateObject` draft with the same data-entry surface as minting.
- Hard constraints: do not leave `Verify ownership` in BO Actions; keep owner-facing ownership QR presentation available; do not create a second ad hoc edit form; preserve the existing adapter update path that spends the current BO UTXO.
- Evidence required before closure: targeted bUnit tests, `dotnet test`, app build, Docker image rebuilds, and Playwright browser proof for Home ownership verification and BO details edit dialog staging.
- Known blockers or explicit scope exceptions: none at preparation time.

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

1. `subbundles/01-sb01-ownership-verifier-home-boundary`
2. `subbundles/02-sb02-bo-details-edit-update-staging`
3. `subbundles/03-sb03-docker-browser-validation-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed after prepared-stage validator and manual workflow audit`
- Execution status: `Completed`
- Subbundle gate review: `Completed; see bundle://reviews/01-execution-report.md`
- Final closure gate: `Completed with bundle://proof/SB03/transcripts/completed-stage-validator.txt`
- Browser validation analytics: `Completed with bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt and screenshots under bundle://proof/SB03/browser/`
