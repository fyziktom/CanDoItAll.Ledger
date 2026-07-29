# Local Containers

The canonical development stack is `compose.yaml`. Its default graph builds
the PostgreSQL-backed Ledger node and one static AccountApp. Additional
AccountApps and the DemoSeeder are opt-in profiles. PostgreSQL 16 runs only on
the internal backend network.

## Configuration

Copy the committed non-secret template and replace its password placeholders:

```powershell
Copy-Item .env.example .env
```

`.env` is ignored and supplies Compose interpolation only. It is not a
production secret store. Production credentials belong in the deployment
platform or an approved secret manager.

Default loopback endpoints:

| Service | URL |
| --- | --- |
| Ledger node | `http://127.0.0.1:8080` |
| AccountApp | `http://127.0.0.1:5081` |

PostgreSQL is not published to the host. Containers use the `postgres` service
name and port 5432. AccountApps proxy `/api/` to the `node` service.
`CDA_PUBLIC_HOST` must match the hostname opened in the browser; its default
`127.0.0.1` matches the endpoints above.

The Node has no SQLite ledger-state provider. Compose uses PostgreSQL because
it is the supported durable provider. The EF Core InMemory and direct
in-memory stores remain test/disposable options only.

## Validate And Start

```powershell
.\tools\validation\Test-Docker.ps1 -EnvFile .env
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build --wait --wait-timeout 300
docker compose --env-file .env ps --all
```

The node healthcheck calls `/api/v1/health`; AccountApp healthchecks load their
Nginx root. Health-based startup ordering prevents the node from starting
before PostgreSQL is healthy and prevents AccountApps or the Seeder from
starting before Node liveness succeeds. Use `/api/v1/health/readiness` when a
deployment gate must also prove dependencies and Ledger verification.

Start two additional browser origins when multi-account testing is needed:

```powershell
docker compose --env-file .env --profile multi-account up -d --wait
```

They publish ports `5082` and `5083`. Each port has a separate browser
IndexedDB boundary.

`Test-Docker.ps1 -RunBuildChecks` validates every service and profile through
the resolved Compose build model. Application Dockerfiles restore published
CanDoItAll dependencies from NuGet.org and do not require sibling repository
build contexts.

For account creation, funding, backup, and import, follow
[Node And AccountApp First Setup](../getting-started/node-and-account-app.md).

## Seed A Disposable Development Ledger

The optional Seeder writes development account keys and backups to the ignored
`docker/demo-keys` host-exchange directory:

```powershell
.\scripts\seed-demo.ps1 -EnvFile .env
```

Those files contain sensitive local key material. Do not commit, share, or use
them outside an explicitly disposable development ledger. On a native Linux or
macOS Docker engine, the wrapper runs the Seeder with the host UID and GID so
the non-root container can write the bind-mounted exchange directory.

## Data Classes

| State | Location | Classification |
| --- | --- | --- |
| Ledger database | `postgres-data` named volume | Authoritative development ledger state |
| Node Data Protection key ring | `node-dpkeys` named volume | Operational authentication continuity |
| Seeder account keys | ignored `docker/demo-keys` directory | Sensitive disposable development credentials |
| Container logs | bounded Docker `local` driver | Transient operational evidence |

Data Protection keys are not Ledger signing keys and do not replace database
backups. Losing the key ring invalidates protected authentication state, but it
does not rewrite signed Ledger transactions.

## Stop Without Deleting Data

```powershell
docker compose --env-file .env down --remove-orphans
```

This preserves `postgres-data` and `node-dpkeys`. Never add `--volumes` to the
normal stop path. Volume deletion is an explicit reset that permanently removes
the selected Compose project's local database and key-ring state.

## Runtime Safety

- Application ports bind to `127.0.0.1` by default.
- Images run as non-root users; node capabilities are dropped and its root
  filesystem is read-only.
- Logs use Docker's bounded `local` driver.
- CPU, memory, and PID defaults are configurable in `.env`.
- The development stack uses `restart: "no"` so failures remain visible.

This Compose file is a development model. Production deployments must supply
their own secret, certificate, backup, monitoring, availability, and recovery
controls.
