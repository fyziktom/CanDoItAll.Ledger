# Current State

## Repository Baseline

- Branch: `renaming`.
- Worktree before preparation: clean.
- SDK request: `10.0.200` with `rollForward: latestFeature`; local CLI resolves
  installed SDK `10.0.300`.
- Canonical solution: `CanDoItAll.Ledger.slnx`.
- Repository-owned projects: 27; 21 project paths currently carry
  `CanDoItAll.Economy`.
- Active tracked paths containing `Economy`, excluding closed bundles and the
  legacy-doc quarantine: 602.
- Active tracked content files containing an old namespace/config/service
  identity: 637 (`src` 490, `tests` 85, `examples` 42, `docs` 7, `tools` 4,
  `docker` 4, `scripts` 2, plus root solution/README/Compose).

## Current Identity Shape

- Fifteen production project folders still begin with
  `src/CanDoItAll.Economy.*`.
- Five example project folders and one DemoSeeder tool folder still begin with
  `CanDoItAll.Economy.*`.
- The already-moved `CanDoItAll.Ledger.Simulator*` paths still declare
  `CanDoItAll.Economy.Simulator*` assembly and root namespaces.
- Current source uses old namespaces, `EconomyNode*`, `EconomyClient`,
  `EconomyDbContext`, `EconomyAccount*`, and `EconomySimulator*` active
  identifiers.
- Compose still uses `docker-compose.yml`, service `economy-node`,
  `Economy__*` configuration, `CANDOITALL_ECONOMY_*` variables, a Windows bind
  mount for PostgreSQL data, and non-loopback published ports.

## Dependency Evidence

- The manual `.csproj` inventory found a directed acyclic product graph rooted
  in Core/Simulation.Abstractions and consumed by Engine, BusinessObjects, SDK,
  persistence, components, Node, examples, tests, and DemoSeeder.
- No new project or project reference is required. The target graph must be
  isomorphic under the rename map.
- CodeAnalytics could not provide reliable project/cycle evidence. Both the
  solution-wide and Engine-project snapshots loaded zero projects because the
  service resolved an unrelated parent `global.json`. Snapshot IDs and
  diagnostics are preserved in `inputs/01-source-artifacts.md`.

## Partial-Class And Generated-Code Evidence

- Existing production partial groups:
  `EconomyNodeHost` (four hosting/API files),
  `SimulationDeterministicHash` (three cohesive hashing files), and
  `EconomySimulatorSchema`/`EconomySimulatorIdentifiers` (one constants file).
- EF migration partials and the DbContext model snapshot are generated/migration
  surfaces.
- This migration must rename these existing identities but must not add partial
  files, responsibilities, service-location, factories, or abstraction layers.

## Existing Validation Surfaces

- `scripts/build.ps1`
- `scripts/test.ps1`
- `scripts/audit-ledger-boundaries.ps1`
- `scripts/audit-ledger-repo-does-not-contain-basic-simulator.ps1`
- `scripts/audit-economy-ledger-split.ps1` (intentionally names both repositories)
- `tests/CanDoItAll.Ledger.Tests`
- Docker Node, DemoSeeder, and three AccountApp services
