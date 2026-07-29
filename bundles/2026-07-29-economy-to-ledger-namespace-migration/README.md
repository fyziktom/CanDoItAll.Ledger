# Economy to Ledger Namespace Migration

## Profile

- `initiative`

## Mission

- Complete the repository split by replacing the active `CanDoItAll.Economy.*`
  project, assembly, and namespace identity with `CanDoItAll.Ledger.*`, while
  preserving the existing dependency topology and runtime behavior.
- Rename the former `CanDoItAll.Economy.Ledger` validation/execution assembly to
  `CanDoItAll.Ledger.Engine` so it remains distinct from
  `CanDoItAll.Ledger.Core`.

## Outcome Contract

- Requested outcome: all active Ledger-owned projects, namespaces, consumers,
  Docker assets, tools, tests, and current documentation use the Ledger identity.
- Hard constraints: preserve behavior and project-reference direction; do not
  mutate sibling repositories; do not rewrite historical bundle evidence or the
  quarantined legacy documentation as though it were current; keep the Economy
  repository boundary audit meaningful.
- Evidence required before closure: identity residual checks, unchanged
  dependency topology, clean solution build, complete Ledger test run, Docker
  standards/config validation, rebuilt/restarted Compose services, healthy node,
  and browser/HTTP smoke of the Dockerized AccountApp instances.
- Known blockers or explicit scope exceptions: the CodeAnalytics MCP is unhealthy
  because its host resolves `C:\repositories\CanDoItAll\global.json` against an
  unavailable SDK. Manual project-graph evidence and .NET build proof are required
  unless the service recovers. Historical inputs and intentionally stable
  persisted format identifiers may retain `Economy` only when an explicit
  compatibility row documents the reason.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries when architecture decisions are material
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts when repeated handoff needs them
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-active-project-and-namespace-identity`
2. `subbundles/02-documentation-tooling-and-residual-guard`
3. `subbundles/03-full-build-docker-accountapp-validation`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## UI Target Policy

- No UI composition changes are planned. Runtime proof uses a large-screen
  `1600x1000` AccountApp browser viewport and checks only for rename regressions,
  load failures, console errors, and broken asset/API connections.

## Validation Summary

- Bundle preparation status: `Completed`
- Execution status: `SB01-SB03 complete`
- Subbundle gate review: `SB01, SB02, and SB03 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed for Alice and Bob at 1600x1000`
