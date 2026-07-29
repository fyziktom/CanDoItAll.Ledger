# Contributing

This repository accepts code contributions only from partners who have been
explicitly approved by the maintainer. Unsolicited pull requests are not
accepted.

To discuss becoming an approved partner, contact the maintainer on LinkedIn
using the handle `fyziktom`. Wait for approval before preparing or opening a
pull request.

## Development Setup

1. Install the .NET SDK selected by `global.json`.
2. Install Docker Desktop or another Docker Compose implementation when working
   on container behavior.
3. Run commands from the repository root.
4. Copy `.env.example` to ignored `.env` only when the container lane is needed,
   and replace every password placeholder.

## Validation

For normal code changes:

```powershell
dotnet restore CanDoItAll.Ledger.slnx --configfile NuGet.config
dotnet build CanDoItAll.Ledger.slnx --configuration Release --no-restore
dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj `
  --configuration Release --no-build --no-restore
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
```

For Docker changes, also run:

```powershell
.\tools\validation\Test-Docker.ps1 -EnvFile .env.example -RunBuildChecks
```

For packaging changes, run:

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1
```

## Architecture Rules

- Keep deterministic ledger contracts and execution independent from hosts,
  user interfaces, and infrastructure providers.
- Treat signed transactions and persisted identifiers as compatibility
  contracts.
- Keep Simulator projections distinct from confirmed Ledger facts.
- Consume CanDoItAll Components and IPFS through their published packages.
- Keep credentials, account keys, Data Protection keys, generated packages,
  logs, and local state out of Git.
- Update tests and durable documentation when public behavior or package
  contracts change.

## Pull Requests

- Open a pull request only after partner approval.
- Keep changes focused.
- Add or update tests for behavior changes.
- Describe public API, persistence, security, and migration effects.
- Include the exact validation commands and results.
