# Secure Message BO Privacy Hardening

This bundle hardens secure-message business objects so transaction history can prove that a message transfer happened without leaking the message subject, plaintext attachment metadata, or IPFS attachment locator in raw transaction data. It also makes raw transaction display and BO envelope parsing tolerant enough to show nested BO state as structured JSON while keeping legacy ledger entries readable.

## Profile

- `initiative`

## Mission

- Remove privacy leaks from new secure-message transactions, preserve compatibility with existing message transactions, and prove the result through unit/component/integration tests plus Docker/browser validation.

## Outcome Contract

- Requested outcome: secure-message raw tx data must not contain subject-derived BO IDs or public attachment DataItem/IPFS metadata; raw tx view must be newest-first and show nested JSON structurally where possible.
- Hard constraints: do not break existing v1 message decryption, transaction projections, BO history, public-key discovery, or Docker WASM account apps.
- Evidence required before closure: failing-first privacy tests, passing implementation tests, AccountApp build, Docker compose rebuild/restart, Alice/Bob send/read attachment validation, and raw tx screenshot.
- Known blockers or explicit scope exceptions: public sender/receiver routing remains visible because ledger inputs/outputs require it; broader non-message BO ID naming is inventoried but only secure-message IDs are changed in this bundle.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `inventories/secure-message-privacy-impact-map.xlsx` detailed affected-surface workbook
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-privacy-red-team-baseline`
2. `subbundles/02-opaque-secure-message-identity`
3. `subbundles/03-encrypted-attachment-manifest`
4. `subbundles/04-object-state-envelope-compatibility`
5. `subbundles/05-transaction-raw-view-and-tests`
6. `subbundles/06-docker-browser-closure`

## Dependency And Validation Map

- `SB01` defines privacy assertions and proves the existing failure mode.
- `SB02` removes subject/name leakage from secure-message BO IDs and external refs.
- `SB03` moves attachment locator metadata into encrypted message content while retaining v1 read support.
- `SB04` makes BO envelope readers compatible with both legacy string `stateJson` and object-valued `state`/display expansion.
- `SB05` updates raw transaction JSON, test coverage, and component rendering proof.
- `SB06` rebuilds Docker WASM apps and captures browser proof.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`
