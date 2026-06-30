# AccountApp BO IPFS Gallery

This bundle coordinates IPFS-backed generic BusinessObject creation, parsing, gallery rendering, and Alice/Bob WASM validation for `2026-06-30-accountapp-bo-ipfs-gallery`.

## Profile

- `initiative`

## Mission

Make the AccountApp able to create and view the practical generic BO families that replace VEFramework NFTs: posts, image/media records, tickets, events, payments, purchases/orders, receipts, invoices, messages, products, and profile-like records. Attachments must be stored through the rebuilt IPFS docker stack, transaction assembly/signing must remain in WASM, and the gallery/detail UI must show typed data instead of falling back to raw generic state.

## Outcome Contract

- Requested outcome: IPFS-backed BO creation and gallery validation across Alice and Bob AccountApp WASM instances.
- Hard constraints: AccountApp communicates with the ledger only through the node API; BO transactions are assembled and signed in the browser; IPFS upload must be browser-safe; do not implement domain-specific VEFramework NFTs such as IoT, Xray, or Coruzant records in this bundle.
- Evidence required before closure: prepared and completed bundle validators, IPFS docker health, .NET tests, AccountApp build, docker app rebuild, Playwright browser proof for Alice/Bob create/import/gallery/detail flows, screenshots, anti-stub audit, semantic proof manifests for critical subbundles.
- Known blockers or explicit scope exceptions: the CanDoItAll components MCP transport closed during lookup, so local component source inspection is the fallback. New domain-specific BO families beyond the generic inventory are out of scope.

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

1. `subbundles/01-sb01-ipfs-stack-and-wasm-upload-service`
2. `subbundles/02-sb02-generic-bo-schema-and-typed-casting`
3. `subbundles/03-sb03-bo-wizard-and-gallery-ux-coverage`
4. `subbundles/04-sb04-alice-bob-wasm-validation`

## Dependency And Validation Map

- `plan/01-phase-plan.md` contains the dependency map, critical subbundle list, phase gates, and reopen triggers.
- If the bundle is resumed after compaction or by a different agent, use this README, the active subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Complete`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
