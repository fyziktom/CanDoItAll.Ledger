# AccountApp UTXO Merge Split Transactions

This bundle is a coordination and execution package for `2026-07-04-accountapp-utxo-merge-split`.

## Profile

- `feedback`

## Mission

- Repair the AccountApp home send dialog so its Merge and Split tabs perform plain main-currency UTXO consolidation and preparation, not BusinessObject merge or split-payment operations. The implementation must expose reusable transaction form components, use selected eligible UTXOs as explicit inputs, submit real ledger transactions, and prove large-screen transaction detail dialogs remain readable with many inputs or outputs.

## Outcome Contract

- Requested outcome: Send, Merge, and Split transaction forms in the home send dialog are isolated reusable components, and Merge/Split implement explicit selected-UTXO behavior against real account spendable outputs.
- Hard constraints: Merge and Split selectors must exclude BO, token, permission, and rule outputs; Merge vout is one output to self or an entered external address; Split can consume multiple selected inputs and create either equal-amount outputs or specific per-output amounts.
- Evidence required before closure: targeted domain and adapter tests, component tests for reusable forms, build, rebuilt/restarted docker stack, large-screen Playwright proof for Send/Merge/Split, submitted transaction details proving vin/vout shapes, and screenshots showing many-input/many-output details do not overlay, clip, or overflow.
- Known blockers or explicit scope exceptions: token UTXO consolidation and BusinessObject unique UTXO merge/split are excluded from this bundle because the raw request says to avoid BO unique outputs and the existing token rules distinguish source and unique token UTXOs.

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

1. `subbundles/01-explicit-utxo-merge-split-domain`
2. `subbundles/02-reusable-send-dialog-forms`
3. `subbundles/03-docker-browser-transaction-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared-stage validator passed`
- Execution status: `SB01, SB02, and SB03 complete`
- Subbundle gate review: `All subbundles passed`
- Final closure gate: `Completed-stage validator passed`
- Browser validation analytics: `Pass at 1920x1080 with Send, Split, and Merge transaction proof`
