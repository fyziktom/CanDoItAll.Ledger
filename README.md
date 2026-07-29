# CanDoItAll.Ledger

[![CI](https://github.com/fyziktom/CanDoItAll.Ledger/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/fyziktom/CanDoItAll.Ledger/actions/workflows/ci.yml)
[![SDK version](https://img.shields.io/nuget/v/CanDoItAll.Ledger.Sdk.svg?logo=nuget&label=SDK)](https://www.nuget.org/packages/CanDoItAll.Ledger.Sdk)
[![SDK downloads](https://img.shields.io/nuget/dt/CanDoItAll.Ledger.Sdk.svg?logo=nuget&label=SDK%20downloads)](https://www.nuget.org/packages/CanDoItAll.Ledger.Sdk)
[![Components version](https://img.shields.io/nuget/v/CanDoItAll.Ledger.Components.svg?logo=nuget&label=Components)](https://www.nuget.org/packages/CanDoItAll.Ledger.Components)
[![Components downloads](https://img.shields.io/nuget/dt/CanDoItAll.Ledger.Components.svg?logo=nuget&label=Components%20downloads)](https://www.nuget.org/packages/CanDoItAll.Ledger.Components)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/license-MIT--derived%20with%20website%20link-blue.svg)](LICENSE)

CanDoItAll.Ledger is the secure, auditable transaction and financial-state
foundation of the CanDoItAll ecosystem.

Its primary purpose is to give autonomous agents and project teams accountable
control over expenses. Financial actions can be represented as signed
transactions, checked against deterministic rules, persisted as an inspectable
history, and exposed through a node API, SDK, command-line tools, and reusable
user interfaces.

The same foundation can support other finance-oriented software that needs
verifiable ownership, approvals, balances, transfers, business-object state, or
an audit trail. A work-in-progress Simulator explores how planned project
financials can later be compared with confirmed ledger activity for analysis and
forecasting.

## Why A Ledger

Agents can act quickly and across many systems. Expense control needs stronger
evidence than an editable balance or an application log. The Ledger is designed
around:

- signed transaction intent and cryptographic verification;
- deterministic validation and state transitions;
- explicit accounts, assets, UTXOs, authorities, and rules;
- replayable history and chain-consistency checks;
- durable PostgreSQL persistence with in-memory adapters for tests;
- client, component, and node surfaces that share the same contracts.

The Ledger supplies technical evidence and enforcement primitives. It does not
replace accounting, tax, legal, compliance, or human approval obligations.

## Main Capabilities

| Capability | Main entry point |
| --- | --- |
| Ledger contracts and transaction model | `CanDoItAll.Ledger.Core` |
| Transaction validation, execution, replay, and chain verification | `CanDoItAll.Ledger.Engine` |
| Hashing, keys, signatures, and browser cryptography | `CanDoItAll.Ledger.Cryptography*` |
| Business-object transactions and lifecycle rules | `CanDoItAll.Ledger.BusinessObjects` |
| Node APIs, authentication, administration, and operator UI | `CanDoItAll.Ledger.Node` |
| Client integration | `CanDoItAll.Ledger.Sdk` |
| EF Core and PostgreSQL persistence | `CanDoItAll.Ledger.Persistence.*` |
| Reusable Ledger user interfaces | `CanDoItAll.Ledger.Components` |
| Local learning and integration hosts | `examples/` |
| Experimental project-finance simulation | `CanDoItAll.Ledger.Simulator*` |

Published CanDoItAll Components and IPFS packages are consumed from NuGet.org.
The repository does not require sibling source checkouts to build its normal
solution or Docker images.

## Requirements

- .NET SDK selected by `global.json`
- Docker Desktop or another Docker Compose implementation for the container lane
- PowerShell 7 recommended for repository tooling

## Build And Test

Run from the repository root:

```powershell
dotnet restore CanDoItAll.Ledger.slnx --configfile NuGet.config
dotnet build CanDoItAll.Ledger.slnx --configuration Release --no-restore
dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj `
  --configuration Release --no-build --no-restore
```

The repository-owned validation lane also checks product identity, dependency
boundaries through the test suite, plus the resolved Compose model and
Dockerfile build checks:

```powershell
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
.\scripts\build.ps1
.\scripts\test.ps1
.\tools\validation\Test-Docker.ps1 -EnvFile .env.example -RunBuildChecks
```

When `CanDoItAll.SharedInfo` is available, the Docker validator also applies
the ecosystem-wide convention gate.

## Run A Local Node

The smallest interactive host is the Ledger node:

```powershell
dotnet run --project src\CanDoItAll.Ledger.Node\CanDoItAll.Ledger.Node.csproj
```

For the complete PostgreSQL Node, AccountApp, account creation/import, and
first-driver flow, use
[Node And AccountApp First Setup](docs/getting-started/node-and-account-app.md).

For guided transaction scenarios without external services:

```powershell
dotnet run --project examples\CanDoItAll.Ledger.ConsoleDemo\CanDoItAll.Ledger.ConsoleDemo.csproj
```

## Containers

Copy the non-secret template, replace its password placeholders, and start the
development stack:

```powershell
Copy-Item .env.example .env
.\tools\validation\Test-Docker.ps1 -EnvFile .env
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build --wait --wait-timeout 300
```

The default stack exposes the Ledger node at `http://127.0.0.1:8080` and one
AccountApp at `http://127.0.0.1:5081`. PostgreSQL remains on the internal
network. Additional AccountApps are available through the `multi-account`
profile.

The Node has no SQLite ledger-state provider. The Docker path uses PostgreSQL;
in-memory providers remain for tests and disposable local runs. Normal teardown
preserves the database and node Data Protection volumes:

```powershell
docker compose --env-file .env down --remove-orphans
```

See [Local Containers](docs/operations/containers.md) and
[Backup And Restore](docs/operations/backup-and-restore.md) before resetting or
moving durable development data.

## Experimental Financial Simulator

The Simulator models projects, business units, accounts, assumptions, planned
events, interventions, and financial projections. Its intended direction is
planned-versus-actual analysis backed by confirmed Ledger evidence.

It remains work in progress. Current projections are not authoritative
financial statements, accounting records, forecasts, or economic evidence.
Simulator libraries are buildable and testable but intentionally excluded from
public NuGet packaging.

See [Simulator Vision](docs/architecture/ledger-simulator-vision-and-refactoring.md).

## Packaging

Build and test the public Ledger package allowlist without publishing:

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1
```

Packages are written under ignored `artifacts/`. The allowlist contains the
Ledger runtime, SDK, persistence, cryptography, rules, storage, business-object,
and component libraries. `CanDoItAll.Ledger.Simulation.*` and
`CanDoItAll.Ledger.Simulator*` remain non-packable while their contracts evolve.

Each NuGet embeds the README owned by its project. The package build also runs
the solution build and Ledger test suite before packing.

## Documentation

- [Documentation index](docs/README.md)
- [Node and AccountApp first setup](docs/getting-started/node-and-account-app.md)
- [SDK and driver integration](docs/development/sdk-and-drivers.md)
- [Product vision](docs/architecture/product-vision.md)
- [Runtime architecture](docs/architecture/ledger-runtime.md)
- [Ecosystem role and boundaries](docs/architecture/repository-boundaries.md)
- [Persisted identity compatibility](docs/architecture/namespace-compatibility.md)
- [Data Protection key handling](docs/security/data-protection.md)
- [Local validation runbook](docs/operations/local-build-test-audit.md)
- [Contribution policy](CONTRIBUTING.md)

## License And Contributions

This repository uses the
[MIT-Derived License with CanDoItAll Website Link Requirement](LICENSE).
Redistributions of the software or a substantial portion of it in source or
binary form must include at least one link to
[aicandoitall.com](https://aicandoitall.com).

Code contributions are limited to partners approved by the maintainer. See
[CONTRIBUTING.md](CONTRIBUTING.md) and contact the `fyziktom` account on
LinkedIn before preparing or opening a pull request.
