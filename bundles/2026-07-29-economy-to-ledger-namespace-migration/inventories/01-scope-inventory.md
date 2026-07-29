# Scope Inventory

## Project Rename Map

| Current | Target |
| --- | --- |
| `CanDoItAll.Economy.Core` | `CanDoItAll.Ledger.Core` |
| `CanDoItAll.Economy.Cryptography` | `CanDoItAll.Ledger.Cryptography` |
| `CanDoItAll.Economy.Cryptography.Browser` | `CanDoItAll.Ledger.Cryptography.Browser` |
| `CanDoItAll.Economy.Ledger` | `CanDoItAll.Ledger.Engine` |
| `CanDoItAll.Economy.BusinessObjects` | `CanDoItAll.Ledger.BusinessObjects` |
| `CanDoItAll.Economy.Sdk` | `CanDoItAll.Ledger.Sdk` |
| `CanDoItAll.Economy.Node` | `CanDoItAll.Ledger.Node` |
| `CanDoItAll.Economy.Persistence.EFCore` | `CanDoItAll.Ledger.Persistence.EFCore` |
| `CanDoItAll.Economy.Persistence.PostgreSql` | `CanDoItAll.Ledger.Persistence.PostgreSql` |
| `CanDoItAll.Economy.Rules.NCalc` | `CanDoItAll.Ledger.Rules.NCalc` |
| `CanDoItAll.Economy.Components` | `CanDoItAll.Ledger.Components` |
| `CanDoItAll.Economy.Simulation.Abstractions` | `CanDoItAll.Ledger.Simulation.Abstractions` |
| `CanDoItAll.Economy.Simulation.Ledger` | `CanDoItAll.Ledger.Simulation.Ledger` |
| `CanDoItAll.Economy.Storage.InMemory` | `CanDoItAll.Ledger.Storage.InMemory` |
| `CanDoItAll.Economy.Cli` | `CanDoItAll.Ledger.Cli` |
| `CanDoItAll.Economy.AccountApp` | `CanDoItAll.Ledger.AccountApp` |
| `CanDoItAll.Economy.AppHost` | `CanDoItAll.Ledger.AppHost` |
| `CanDoItAll.Economy.AppHost.DcpProxy` | `CanDoItAll.Ledger.AppHost.DcpProxy` |
| `CanDoItAll.Economy.Components.Demo` | `CanDoItAll.Ledger.Components.Demo` |
| `CanDoItAll.Economy.ConsoleDemo` | `CanDoItAll.Ledger.ConsoleDemo` |
| `CanDoItAll.Economy.DemoSeeder` | `CanDoItAll.Ledger.DemoSeeder` |

## Stale Identities In Already-Renamed Paths

- `CanDoItAll.Ledger.Simulator`
- `CanDoItAll.Ledger.Simulator.Components`
- `CanDoItAll.Ledger.Simulator.Persistence.EFCore`
- `CanDoItAll.Ledger.Simulator.App`

Their paths remain, but assembly names, root namespaces, internal namespaces,
types, component imports, and docs change from `Economy.Simulator*` to
`Ledger.Simulator*`.

## Active Surfaces

- `CanDoItAll.Ledger.slnx` and every repository-owned `.csproj`.
- C#, Razor, generated EF migration metadata, JSON configuration, CSS selectors
  tied to component types, Dockerfiles, shell entry points, and PowerShell.
- `README.md`, active `docs/**`, project READMEs, example/tool READMEs.
- Compose service/image names, `Ledger__*` configuration section names,
  `CANDOITALL_LEDGER_*` DemoSeeder variables, paths and service DNS.
- Test source identifiers, reflection/assembly assertions, source-text contract
  tests, and test project references.

## Historical/Boundary Exclusions

- Closed `bundles/**` except this active bundle.
- `docs/legacy-economy-ledger-docs/**`, which remains explicitly quarantined.
- Literal `CanDoItAll.Economy` sibling-repository references in boundary docs and
  `scripts/audit-economy-ledger-split.ps1`.
- Persisted identifiers approved in the final residual allowlist only when a
  compatibility test and rationale exist.

## Standards Surfaces

- Canonical Compose filename/name, role-based services, loopback ports, named
  PostgreSQL volume, readiness dependencies, no committed production secret,
  and local/shared Docker validation.
- Root/current docs must explain ownership, commands, project entry points, WIP
  simulator status, and migration break.
- Tooling lives under `tools/validation` with portable path discovery and
  nonzero failure behavior.
