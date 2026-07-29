# CanDoItAll.Ledger.Simulator.App

Local interactive host for the experimental project-finance Simulator.

> [!WARNING]
> The Simulator is work in progress. Its projections are not authoritative
> accounting, forecasting, tax, legal, compliance, economic, or investment
> evidence.

The app demonstrates project workspaces, planned events, deterministic
scenarios, interventions, runs, projections, graphs, and reconciliation
surfaces.

## Run Locally

From the repository root:

```powershell
dotnet run --disable-build-servers `
  --project examples\CanDoItAll.Ledger.Simulator.App\CanDoItAll.Ledger.Simulator.App.csproj `
  -- --urls http://127.0.0.1:5287
```

Open <http://127.0.0.1:5287>. The process-liveness endpoint is
<http://127.0.0.1:5287/health>; it does not probe PostgreSQL or other external
dependencies.

## Local Data Protection Keys

The host generates ASP.NET Core Data Protection keys under the ignored:

```text
examples/CanDoItAll.Ledger.Simulator.App/artifacts/dataprotection/simulator-app
```

On Windows, entries are protected with machine-scoped DPAPI. They protect
application payloads such as cookies and antiforgery state; they are not Ledger
transaction-signing keys.

Override `DataProtection:KeyRingPath` with an absolute path or a path relative
to the app content root. See
[Data Protection key handling](../../docs/security/data-protection.md) before
changing or regenerating the key ring.

## Projection Storage

Configuration is under `LedgerSimulator`. Supported `StorageMode` values are
`InMemory` and `PostgreSql`; PostgreSQL requires
`LedgerSimulator:PostgreSql:ConnectionString`.

The provider adapters are registered, but the current run lifecycle does not
write through them. Project workspaces, runs, and generated graphs remain in
memory in this host.
