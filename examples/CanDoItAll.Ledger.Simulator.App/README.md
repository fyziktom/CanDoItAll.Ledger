# CanDoItAll.Ledger.Simulator.App

Local browser host for the moved BO/project simulator.

The app is retained for Ledger smoke testing of the migrated simulator runtime.
It must not become the Economy simple/Smith launch surface.

## Run locally

From the repository root:

```powershell
dotnet run --disable-build-servers --project examples\CanDoItAll.Ledger.Simulator.App\CanDoItAll.Ledger.Simulator.App.csproj -- --urls http://127.0.0.1:5287
```

Open <http://127.0.0.1:5287>. The process-liveness endpoint is
<http://127.0.0.1:5287/health>; it does not probe PostgreSQL or other dependencies.

The interactive-server host writes ASP.NET Core Data Protection keys to the
writable `DataProtectionKeys` directory beside the app by default. Override
`DataProtection:KeyRingPath` with an absolute path, or with a path relative to
the app content root, when another durable writable location is required.

The projection provider is configured under `EconomySimulator`. Supported
`StorageMode` values are `InMemory` and `PostgreSql`; PostgreSQL requires
`EconomySimulator:PostgreSql:ConnectionString`. The provider adapters are
registered, but the current run lifecycle does not write through them. The
project workspace, runs, and generated graphs remain in memory in this host.
