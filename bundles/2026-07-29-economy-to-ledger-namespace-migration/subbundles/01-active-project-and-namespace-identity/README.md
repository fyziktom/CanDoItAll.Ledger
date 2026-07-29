# Active Project And Namespace Identity

## Status

- `Completed`

## Objective

- Atomically migrate all active .NET project, assembly, folder/file, namespace,
  using, type, configuration, Compose/build-path, example, tool, and test
  identities from Economy to Ledger without changing behavior or project
  dependency topology.

## Success Criteria

- The project map in `inventories/01-scope-inventory.md` is implemented exactly.
- The former Ledger project is `CanDoItAll.Ledger.Engine`.
- Stale Simulator assembly/root/source identities use `CanDoItAll.Ledger`.
- Solution/project references resolve; no `Ledger.Ledger` collision exists.
- Full solution build and all Ledger tests pass.
- A negative residual scan finds no unapproved active old identity.

## Covered Inputs

- N001, N002, the implementation part of N003, R001-R005, and R009.

## Prerequisites

- Prepared bundle validation passes.
- Pre-change project graph and explicit rename map remain trusted.
- Existing worktree changes are limited to this bundle.

## Exact Source References

- `C:\repositories\CanDoItAll.Ledger\CanDoItAll.Ledger.slnx`
- `C:\repositories\CanDoItAll.Ledger\src`
- `C:\repositories\CanDoItAll.Ledger\examples`
- `C:\repositories\CanDoItAll.Ledger\tests`
- `C:\repositories\CanDoItAll.Ledger\tools\CanDoItAll.Ledger.DemoSeeder`
- `C:\repositories\CanDoItAll.Ledger\docker`
- `C:\repositories\CanDoItAll.Ledger\compose.yaml`

## UI Composition Contract

- N/A. Existing Razor/component behavior is renamed, not redesigned.

## Deliverables

- Renamed directories, files, `.csproj` files, assemblies, root/source
  namespaces, types, project references, solution entries, runtime config
  prefixes, service/image names, Docker paths, examples, tests, and DemoSeeder.
- Approved residual list for historical/sibling/persisted compatibility cases.

## Dependency Impact

- This is the critical foundation for current docs/tool validation and all
  Docker runtime proof. Weak identity or graph proof invalidates SB02 and SB03.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: yes; affected surface is the entire repository-owned .NET
  graph and Node/AccountApp composition.

## Implementation Steps

1. Capture normalized pre-change project reference edges and partial/file counts.
2. Apply the explicit longest-name-first project mapping, then rename active
   namespace/type/config identities and affected file names.
3. Update solution/project references and Docker build/DNS/config connections.
4. Inspect persisted identifiers and sibling/historical references individually;
   migrate or allowlist with rationale.
5. Run identity/path scans, graph comparison, Release build, full tests, and a
   focused Node composition smoke.
6. Run the architecture review checkpoint and update execution status/proof.

## Scope Exceptions

- Closed bundle evidence and quarantined legacy docs are not rewritten.
- The separate `CanDoItAll.Economy` repository keeps its real name in boundary
  docs/audits.
- Ignored local Docker data is not deleted.

## Do Not Do

- Do not add namespace shims, type-forwarding projects, new project references,
  partial files, factories/facades, or service location.
- Do not mechanically change persisted format identifiers without compatibility
  analysis.
- Do not edit sibling repositories.

## Acceptance Checklist

- [x] All 21 old project identities map to the target names.
- [x] All already-moved Simulator project metadata is Ledger-named.
- [x] `Ledger.Engine` owns the former validation/execution source.
- [x] Solution and all project references resolve.
- [x] Normalized project edges match; no cycle/forbidden edge exists.
- [x] No new partial declaration/file exists.
- [x] Release build and full tests pass.
- [x] Old active identity and `Ledger.Ledger` scans pass.
- [x] Downstream Node/composition behavior is covered by the passing Node tests.

## C# Architecture Impact

- Public assembly/namespace identity changes across the existing project graph;
  responsibility ownership does not move.

## Boundary Ownership

- Existing owners remain as recorded in
  `architecture/01-csharp-boundary-map.md`; the former Ledger project is named
  Engine to express its existing responsibility.

## Dependency Direction

- One-to-one name substitution only. Every pre-change edge must have exactly one
  target edge and no additional edge.

## Pattern Decision

- Direct identity migration; no design pattern layer.

## Testability Contract

- Identity validator positive/negative proof, normalized graph equality, full
  tests, and composition smoke.

## Partial Class Policy

- Rename existing partial groups in place; add no partial file or responsibility.

## Architecture Proof Required

- Before/after graph table, manual/CodeAnalytics cycle result, source residual
  assertions, partial-count assertion, build/test results, and downstream Node
  smoke.

## Proof Required

- Exact identity scans and approved residual report.
- Exact build/test commands and results.
- Dependency/partial comparison and C# architecture gate result.
- Positive intended case: renamed assemblies compose and tests pass.
- Shallow-pass trap: only project folders were renamed while namespaces,
  assembly names, config, or references stayed old.
- Adversarial negative: an injected old namespace fixture is detected by SB02's
  validator; until then, use an explicit source scan.

## Browser Validation Logging

- N/A for SB01. Browser proof is owned by SB03.

## Progression Gate

- SB02 may start only when every acceptance item passes and the architecture
  checkpoint records no cycle, edge drift, partial expansion, or old active
  identity.

## Reopen Triggers

- Reopen if SB02 or SB03 finds an old path/namespace/config identity, broken
  Docker build context/service DNS, missing assembly, unexpected graph edge, or
  runtime composition failure.

## Suggested Agent Prompt

```text
Implement only the atomic active Economy-to-Ledger identity migration defined by
the exact project map. Preserve dependency topology and persisted compatibility,
prove build/tests and negative residual behavior, update the execution report,
and stop on a collision, cycle, unplanned edge, or ambiguous persisted identity.
```
