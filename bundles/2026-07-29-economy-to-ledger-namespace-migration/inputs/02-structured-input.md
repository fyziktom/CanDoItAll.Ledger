# Structured Input

## Core Objective

- Make Ledger the sole active product identity for this repository's .NET
  project graph and runtime surfaces.

## Success Criteria

- The 21 projects currently carrying `CanDoItAll.Economy` identities use
  `CanDoItAll.Ledger` identities and paths.
- `CanDoItAll.Economy.Ledger` becomes `CanDoItAll.Ledger.Engine`.
- Existing Ledger Simulator projects also replace their stale
  `CanDoItAll.Economy.Simulator*` assembly/root namespaces.
- Project references and `CanDoItAll.Ledger.slnx` resolve only renamed paths.
- Active C#/Razor namespaces, usings, generated EF metadata, tests, tools,
  examples, config prefixes, and Docker build paths agree with the new names.
- Current documentation describes the new project map and marks the simulator
  WIP/experimental.
- Clean build, full tests, Docker rebuild/restart, node health, and AccountApp
  browser/HTTP smoke pass.

## Hard Constraints

- No sibling repository writes.
- No hidden compatibility namespace left in production code.
- No change to domain behavior or project-reference direction.
- No destructive deletion of the existing `docker/data` state.
- Historical bundle proof and quarantined legacy documents remain historical.
- Stable serialized/protocol identifiers are changed only with explicit proof
  that compatibility is not required; otherwise they are documented exceptions.

## Allowed Side Effects

- Rename repository-owned folders/files/projects, active identifiers, config
  keys, Docker services/images, local development storage, tests, scripts, and
  documentation.
- Adopt the canonical `compose.yaml`, a named PostgreSQL volume, loopback-only
  published ports, required environment input, and repository-owned Docker
  validation while the Compose surface is already changing.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- N001: namespace/project/folder/solution rename.
- N002: resolve `CanDoItAll.Economy.Ledger` naming collision.
- N003: documentation, tools, and tests migrate with production code.
- N004: shared repository standards govern .NET/Docker/documentation/tooling.
- N005: simulator is explicitly WIP/experimental.
- N006: full behavioral and Dockerized AccountApp validation.

## Dependency And Sequencing Signals

- The active project/namespace rename is an atomic critical foundation.
- Documentation and residual tooling must inspect the final identity rather than
  guessing it in advance.
- Docker runtime proof depends on the renamed projects, build paths,
  configuration prefixes, and validation tool.

## Validation Expectations

- Behavioral proof for the identity migration and end-to-end runtime.
- Positive proof: renamed solution builds/tests and Docker AccountApps load.
- Negative proof: active identity audit rejects old namespace/project/config
  names and dependency/cycle audit finds no new edge or cycle.

## Evidence Contract

- SB01 `Behavioral`: before/after project map, residual scan, build, full tests,
  and manual/CodeAnalytics dependency-cycle proof.
- SB02 `Behavioral`: standards validation, documentation link/identity audit,
  residual guard positive case and deliberate negative fixture.
- SB03 `Behavioral`: clean solution build/test, `docker compose config`,
  SharedInfo Docker conventions, image rebuild, stack restart with `--wait`,
  service health/API smoke, large-screen AccountApp browser smoke, and clean
  teardown that preserves durable volumes.

## UI Validation Strategy

- No composition change. At `1600x1000`, verify Alice and Bob AccountApp roots
  render, assets load, node-proxy traffic connects, no rename-related console
  error appears, and the existing page/scroll ownership is unchanged.

## Browser Validation Analytics

- SB03 logs both AccountApp routes, `1600x1000`, navigation/assertions, console
  and failed-request checks, screenshots if browser proof exposes a regression,
  and first-viewport/scroll-owner observations.

## Working Assumptions

- This is an intentional breaking namespace/assembly migration; no shim package
  is requested.
- `CanDoItAll.Ledger.Engine` is the selected name for transaction validation and
  execution.
- Literal references to the separate `CanDoItAll.Economy` repository remain in
  boundary docs/audits.

## Primary Risks

- Mechanical replacement could produce `Ledger.Ledger`, stale project paths, or
  accidentally rename persisted identifiers.
- EF migration designer/snapshot metadata can compile while pointing to an
  inconsistent context name.
- Docker may build but fail at runtime if service DNS/config prefixes diverge.
- Closed proof can be invalidated if generated/ignored stale outputs are scanned
  as source.
