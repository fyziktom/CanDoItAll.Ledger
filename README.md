# CanDoItAll.Ledger

CanDoItAll.Ledger owns the transaction runtime, node host, ledger persistence, ledger UI, SDK, CLI, demos, and ledger-specific tests that were separated from CanDoItAll.Economy.

CanDoItAll.Economy remains the basic simulator repository. Basic account simulation, scenario packs, Smith-like experimental economics, visualization, WebGL observer evidence, and simulator host behavior stay there.

## Project Map

```text
src/CanDoItAll.Economy.Core                    Ledger transaction, chain, account, and storage contracts.
src/CanDoItAll.Economy.Cryptography            Hashing, key, and signature drivers.
src/CanDoItAll.Economy.Ledger                  Transaction validation, execution, and chain verification.
src/CanDoItAll.Economy.BusinessObjects         Ledger-bound object transitions and transaction builders.
src/CanDoItAll.Economy.Sdk                     Client facade for node and ledger operations.
src/CanDoItAll.Economy.Node                    Ledger node host, APIs, auth, and admin UI shell.
src/CanDoItAll.Economy.Persistence.EFCore      EF Core ledger store and projections.
src/CanDoItAll.Economy.Persistence.PostgreSql  PostgreSQL registration and ledger persistence wiring.
src/CanDoItAll.Economy.Components              Ledger UI components.
src/CanDoItAll.Economy.Simulation.Abstractions Ledger-backed simulation contracts only.
src/CanDoItAll.Economy.Simulation.Ledger       Ledger-backed simulation adapter.
src/CanDoItAll.Economy.Storage.InMemory        In-memory ledger storage.
src/CanDoItAll.Economy.Cli                     Ledger command-line driver.
src/CanDoItAll.Ledger.Repository               Repository-local ledger helper project.
examples/                                     Ledger demos and AppHost projects.
tests/CanDoItAll.Ledger.Tests                 Ledger, node, persistence, SDK, and boundary tests.
tools/CanDoItAll.Economy.DemoSeeder           Demo ledger seeding tool.
```

The `CanDoItAll.Economy.*` names are compatibility namespaces from the physical split. They do not mean the basic simulator moved into this repository.

## Local Commands

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\audit-ledger-repo-does-not-contain-basic-simulator.ps1 -RunNegativeFixture
.\scripts\audit-ledger-boundaries.ps1
.\scripts\audit-economy-ledger-split.ps1 -EconomyRoot C:\repositories\CanDoItAll.Economy -LedgerRoot C:\repositories\CanDoItAll.Ledger
```

Direct commands:

```powershell
dotnet build CanDoItAll.Ledger.slnx -c Release -m:1
dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj -c Release
```

## Documentation Map

- `docs/README.md` is the active Ledger docs index.
- `docs/architecture/repository-boundaries.md` defines Economy/Ledger ownership boundaries.
- `docs/architecture/ledger-runtime.md` summarizes the Ledger runtime architecture.
- `docs/architecture/namespace-compatibility.md` tracks compatibility namespaces, owners, costs, and sunset gates.
- `docs/operations/local-build-test-audit.md` is the local build, test, and audit runbook.
- `docs/migration/import-inventory.md` is the migration inventory and compatibility namespace deletion plan.
- `docs/legacy-economy-ledger-docs/README.md` marks imported Economy-era docs as non-product legacy quarantine.

## No-Basic-Simulator Rule

Ledger must not reference or copy Economy basic simulator drivers, scenario-pack content, Smith-like experiment drivers, memory-economy simulator extensions, simulator hosts, or WebGL observer evidence.

Ledger-backed simulation may remain here only when it depends on ledger contracts and does not import the Economy basic simulator lane.

## Namespace Migration

The first repository split intentionally kept imported `CanDoItAll.Economy.*` namespaces to avoid mixing physical movement with a broad public API rename. New Ledger code should use `CanDoItAll.Ledger.*` names where the surrounding public API allows it. Compatibility namespaces are tracked in `docs/architecture/namespace-compatibility.md` and must be retired through a documented package migration, type-forwarding, or source rename plan.
