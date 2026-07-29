# CanDoItAll.Ledger Documentation

CanDoItAll.Ledger provides auditable financial-state and transaction
infrastructure for agents, projects, and finance-oriented applications in the
CanDoItAll ecosystem.

## Start Here

- [Node and AccountApp first setup](getting-started/node-and-account-app.md)
  starts the PostgreSQL Node and browser account client, then creates, funds, or
  imports an account.
- [SDK and driver integration](development/sdk-and-drivers.md) connects .NET
  and raw HTTP clients to the versioned Node API.

## Product And Architecture

- [Product vision](architecture/product-vision.md) explains the problem the
  Ledger solves and the product direction.
- [Runtime architecture](architecture/ledger-runtime.md) describes the trust
  model, transaction flow, modules, and dependency direction.
- [Ecosystem role and boundaries](architecture/repository-boundaries.md)
  defines what the Ledger owns and how it integrates with other CanDoItAll
  packages and applications.
- [Persisted identity compatibility](architecture/namespace-compatibility.md)
  lists identifiers that must remain stable for existing data.
- [IPFS display URI resolution](architecture/ipfs-display-uri-resolution.md)
  documents replaceable content-address display strategies.

## Financial Simulation

- [Simulator vision](architecture/ledger-simulator-vision-and-refactoring.md)
  describes the work-in-progress project-finance modeling and
  planned-versus-actual direction.

Simulator output is exploratory. It is not authoritative accounting,
forecasting, tax, legal, compliance, or investment evidence.

## Operations And Security

- [Local build, test, and audit](operations/local-build-test-audit.md)
- [Local containers](operations/containers.md)
- [PostgreSQL backup and restore](operations/backup-and-restore.md)
- [Data Protection key handling](security/data-protection.md)

## Compatibility Assets

- `golden-vectors/transaction-hashes-v1.json` contains transaction hash
  compatibility vectors used by tests.
- Historical engineering records are retained outside the active documentation
  index and do not define current product behavior.
