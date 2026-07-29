# Local Build, Test, And Audit

Run commands from the repository root.

## Standard Validation

```powershell
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
.\scripts\build.ps1
.\scripts\test.ps1
.\tools\validation\Test-Docker.ps1 -EnvFile .env.example
```

These entry points validate:

- active product and configuration identities;
- a Release solution build;
- the Ledger test suite;
- project-reference and source ownership boundaries covered by the test suite;
- Docker, Compose, secret, healthcheck, volume, and runtime conventions.

The identity negative fixture injects invalid names into an isolated fixture and
proves the validator rejects them. It does not modify product source.

## Direct .NET Lane

```powershell
dotnet restore CanDoItAll.Ledger.slnx --configfile NuGet.config
dotnet build CanDoItAll.Ledger.slnx --configuration Release --no-restore
dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj `
  --configuration Release --no-build --no-restore
```

## Dockerfile Build Checks

```powershell
.\tools\validation\Test-Docker.ps1 `
  -EnvFile .env.example `
  -RunBuildChecks
```

The build checks resolve every Compose profile and validate the same Dockerfile
model used by developers.

## NuGet Packaging

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1
```

This restores, builds, tests, and packs the explicit public package allowlist.
It writes packages under ignored `artifacts/` and rejects Simulator projects.

## Runtime Evidence

For changes that affect hosting, persistence, authentication, migrations, or
containers, compile-time validation is not enough. Start the affected host and
record:

- health and readiness results;
- one representative read and write;
- restart behavior;
- expected persistence after restart;
- expected failure behavior for invalid configuration.
