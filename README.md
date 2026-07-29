# CanDoItAll.Ledger

CanDoItAll.Ledger owns the transaction runtime, node host, ledger persistence,
ledger UI, SDK, CLI, demos, and ledger-specific tests separated from the
`CanDoItAll.Economy` repository.

`CanDoItAll.Economy` remains the separate simple-accounts and experimental
economics repository. Its scenario packs, Smith-style experiments,
visualization, and WebGL observer evidence do not belong here.

## Project Map

```text
src/CanDoItAll.Ledger.Core                    Transaction, chain, account, and storage contracts
src/CanDoItAll.Ledger.Cryptography            Hashing, keys, signatures, and crypto drivers
src/CanDoItAll.Ledger.Engine                  Transaction validation, execution, replay, and chain verification
src/CanDoItAll.Ledger.BusinessObjects         Ledger-bound object transitions and transaction builders
src/CanDoItAll.Ledger.Sdk                     Node and ledger client APIs
src/CanDoItAll.Ledger.Node                    Node host, APIs, auth, and administration UI
src/CanDoItAll.Ledger.Persistence.EFCore      EF Core stores and projections
src/CanDoItAll.Ledger.Persistence.PostgreSql  PostgreSQL registration, migrations, and schema checks
src/CanDoItAll.Ledger.Storage.InMemory        In-memory ledger storage
src/CanDoItAll.Ledger.Components              Ledger and BusinessObject UI components
src/CanDoItAll.Ledger.Repository              Repository-local ledger helpers
src/CanDoItAll.Ledger.Rules.NCalc             NCalc account-rule adapter
src/CanDoItAll.Ledger.Simulation.Abstractions Ledger-backed simulation contracts
src/CanDoItAll.Ledger.Simulation.Ledger       Ledger snapshot simulation adapter
src/CanDoItAll.Ledger.Cli                     Ledger command-line driver
src/CanDoItAll.Ledger.Simulator*              Experimental BO/project simulator
examples/                                     AccountApp, AppHost, component, console, and Simulator hosts
tests/CanDoItAll.Ledger.Tests                 Ledger, node, persistence, UI, SDK, and boundary tests
tools/CanDoItAll.Ledger.DemoSeeder            Demo-ledger seeding and verification tool
```

The former `CanDoItAll.Economy.Ledger` project is now
`CanDoItAll.Ledger.Engine`. `Engine` distinguishes its validation/execution
responsibility from the contract-focused `CanDoItAll.Ledger.Core` project.

## Simulator Status

The BO/project Simulator is work in progress and experimental. It is useful for
development, exploration, and regression testing, but it is not production-ready
and must not be treated as authoritative financial, accounting, forecasting, or
economic evidence. Its in-memory workflows and persistence adapters may change
without compatibility guarantees.

## Local Validation

Run from the repository root:

```powershell
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\audit-ledger-boundaries.ps1
```

The separate two-repository boundary check requires a sibling Economy checkout:

```powershell
.\scripts\audit-economy-ledger-split.ps1 `
  -EconomyRoot C:\repositories\CanDoItAll.Economy `
  -LedgerRoot C:\repositories\CanDoItAll.Ledger
```

## Containers

Copy `.env.example` to ignored `.env` and replace the password placeholders
before starting the development stack:

```powershell
Copy-Item .env.example .env
.\tools\validation\Test-Docker.ps1 -EnvFile .env
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build --wait --wait-timeout 300
```

The default stack publishes the node and three AccountApp instances only on
loopback. PostgreSQL stays on the internal backend network and uses a
Docker-managed named volume. Normal teardown preserves data:

```powershell
docker compose --env-file .env down --remove-orphans
```

See `docs/operations/containers.md` and
`docs/operations/backup-and-restore.md` before resetting or moving durable data.

## Documentation

- `docs/README.md` indexes active documentation.
- `docs/architecture/repository-boundaries.md` defines Economy/Ledger ownership.
- `docs/architecture/namespace-compatibility.md` records the completed namespace
  migration and the intentionally stable persistence identities.
- `docs/operations/local-build-test-audit.md` is the validation runbook.
- `docs/legacy-economy-ledger-docs/README.md` marks imported Economy-era
  material as non-product legacy quarantine.
