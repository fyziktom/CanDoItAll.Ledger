# Ledger Demo Parity Repairs

This bundle coordinates repairs for `2026-07-08-ledger-demo-parity-repairs`.

## Profile

- `initiative`

## Mission

- Bring the console demo and Blazor SSR components demo into parity with the working WASM AccountApp transaction behavior for explicit UTXO split/merge, BusinessObject carrier token rules, token-only BO outputs, and token minting without unintended main-currency amounts.

## Outcome Contract

- Requested outcome: the console app demo and Blazor SSR demo teach and exercise the same transaction semantics that now work in the WASM AccountApp.
- Hard constraints: preserve the shared `BusinessObjectTransactionBuilder` as the source of ledger transaction truth; do not reimplement transaction assembly inside demos; BO create/update/merge/split outputs must carry exactly one carrier token and no main amount in the BO output; plain UTXO merge/split must only select plain main-currency outputs; token minting must not mint main currency.
- Evidence required before closure: failing-first and passing targeted tests, source assertions, anti-stub audit, build proof, console-demo compile proof, SSR demo compile/proof, changed-file hashes, and final completed-stage validator proof.
- Known blockers or explicit scope exceptions: CodeAnalytics MCP snapshot `snap-20260708181406-97cb2b5b-failed` failed before loading projects because the MCP host resolved `C:\repositories\CanDoItAll\global.json` and requested SDK `10.0.200`; this bundle records the MCP-unavailable validation gap and uses direct source/build/test evidence.

## Bundle Layout

- `inputs/` raw request and structured input
- `analysis/` current-state evidence, assumptions, risks, and reopen triggers
- `requirements/` normalized requirements
- `architecture/` C# current-state inventory, boundary map, dependency direction, pattern decisions, and testability plan
- `plan/` execution order, dependency map, phase gates, and architecture checkpoints
- `traceability/` raw-note and requirement mapping
- `shared-prompts/` implementation and QA prompts
- `subbundles/` numbered execution workstreams
- `reviews/` self-review, execution report, and C# architecture gate
- `inventories/` scoped file and behavior inventory
- `templates/` reusable subbundle template

## Recommended Execution Order

1. `subbundles/01-sb01-console-demo-transaction-parity`
2. `subbundles/02-sb02-ssr-demo-transaction-parity-and-closure`

## Dependency And Validation Map

- SB01 proves the console demo exposes and teaches the shared ledger semantics without rebuilding transaction rules locally.
- SB02 depends on SB01 source evidence and closes the SSR demo path plus final raw-note audit.
- If any demo cannot create valid UTXO split/merge transactions through `BusinessObjectAccount`, reopen the affected subbundle and repair the shared adapter/session path before closure.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed prepared-stage validator`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed completed-stage validator`
- Browser validation analytics: `N/A for code-only service changes; source and component tests captured`
