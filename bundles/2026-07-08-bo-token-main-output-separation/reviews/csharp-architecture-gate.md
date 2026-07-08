# C# Architecture Gate

Status: Passed with one tooling gap

## Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| High | Node accepted mixed token/main outputs and token-less normal BO outputs. | `TransactionShapeValidator` had no such rule before this bundle. | Fixed by adding token/main separation validation and normal BO carrier validation. |
| High | UI/adapters could create `MainAmount = 1` BOs. | `EconomyAccountWorkspaceAdapters`, `EconomyNodeWorkspaceSession`, `EconomyDevelopmentWorkspaceSession`, creation wizard. | Fixed by requiring/selecting reusable carrier tokens and creating normal BO drafts with `MainAmount = 0`. |

## Dependency Direction

No new project references added.

## Partial-Class Policy

No partial classes added.

## Testability Proof

Completed:

- Full unit/component test suite: `292/292` passed.
- Focused invariant suite: `33/33` passed.
- Docker build for node, seeder, and account apps passed.
- Docker browser proof passed with real BO create/payment/receipt/purchase/delivered/recycle transactions.

Tooling gap:

- CodeAnalytics MCP snapshot failed before analysis because the environment resolved `C:\repositories\CanDoItAll\global.json` and required SDK `10.0.200`. Manual architecture gate review was completed against the changed boundaries and verified by tests/runtime proof.
