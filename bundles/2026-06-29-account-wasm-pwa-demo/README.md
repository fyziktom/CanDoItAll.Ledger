# Account WASM PWA Demo

Prepared and executed bundle for adding a Blazor WebAssembly PWA demo account application to the ledger solution. SB01 through SB07 are implemented and proven: browser crypto vault, account backup/import flow, API-only node client contracts, transaction assembly/signing split, shared UI SSR/WASM flex, AccountApp PWA demo, and final browser/security hardening.

## Profile

- `initiative`

## Mission

- Add a browser-first AccountApp demo that stores and signs keys in the browser, assembles transactions in WebAssembly, communicates with the node only through API reads and signed transaction broadcast, and reuses existing ledger account UI where it can be made SSR/WASM neutral.

## Outcome Contract

- Requested outcome: Prepare and execute the implementation-ready bundle that maps the current repository and VEFramework precedent, identifies ready components and missing implementation, and provides detailed XLSX checklists for execution.
- Hard constraints: The original preparation-only constraint was superseded by the follow-up implementation request; AccountApp is Blazor WASM and PWA; private keys and signatures stay browser-side; node API is read/broadcast only for this app; encryption/key vault work closed first.
- Evidence required before closure: Prepared bundle validator pass, detailed source inventory, subbundle dependency map, `plan/account-wasm-pwa-planning.xlsx`, per-subbundle proof manifests, browser proof, no-key-leak audit, scoped builds, and ledger tests.
- Known blockers or explicit scope exceptions: Full solution build remains blocked by an unrelated cross-repository `CanDoItAll.IPFS.NodeControl` dependency issue; bundle proof uses scoped AccountApp/demo builds plus the ledger test project.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and source reference map
- `inventories/` ready/missing component and driver inventory
- `plan/` execution order, dependency gates, and XLSX planning workbook
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report scaffold
- `evidence/` durable analysis artifacts captured during preparation

## Recommended Execution Order

1. `subbundles/01-sb01-browser-crypto-vault`
2. `subbundles/02-sb02-account-backup-import-flow`
3. `subbundles/03-sb03-api-only-node-client-contracts`
4. `subbundles/04-sb04-transaction-assembly-signing-split`
5. `subbundles/05-sb05-shared-ui-ssr-wasm-flex`
6. `subbundles/06-sb06-wasm-pwa-demo-app`
7. `subbundles/07-sb07-integration-browser-proof-hardening`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, `plan/account-wasm-pwa-planning.xlsx`, and `reviews/01-execution-report.md` as durable state.

## Validation Summary

- Bundle preparation status: `Prepared validation passed`
- Execution status: `SB01, SB02, SB03, SB04, SB05, SB06, and SB07 closed`
- Subbundle gate review: `SB01 passed; SB02 passed; SB03 passed; SB04 passed; SB05 passed; SB06 passed; SB07 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `SB01 browser harness passed; SB02 account setup browser harness passed; SB03 request-shape API proof passed; SB04 browser signing and POST proof passed; SB05 SSR/WASM adapter render proof passed; SB06 AccountApp PWA proof passed; SB07 no-key-leak/storage/network proof passed`
