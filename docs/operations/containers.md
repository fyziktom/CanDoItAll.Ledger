# Local Containers

The canonical development stack is `compose.yaml`. It builds the Ledger node,
three static AccountApp instances, and an optional DemoSeeder. PostgreSQL 16
runs only on the internal backend network.

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
| Alice AccountApp | `http://127.0.0.1:5081` |
| Bob AccountApp | `http://127.0.0.1:5082` |
| Lab AccountApp | `http://127.0.0.1:5083` |

PostgreSQL is not published to the host. Containers use the `postgres` service
name and port 5432. AccountApps proxy `/api/` to the `node` service.

## Validate And Start

```powershell
.\tools\validation\Test-Docker.ps1 -EnvFile .env
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build --wait --wait-timeout 300
docker compose --env-file .env ps --all
```

The node healthcheck calls `/api/v1/health`; AccountApp healthchecks load their
Nginx root. Readiness-aware dependencies prevent the node from starting before
PostgreSQL is healthy and prevent AccountApps or the Seeder from starting
before the node is ready.

`Test-Docker.ps1 -RunBuildChecks` validates every service and profile through
the resolved Compose build model. Application Dockerfiles restore published
CanDoItAll dependencies from NuGet.org and do not require sibling repository
build contexts.

## Seed A Disposable Development Ledger

The optional Seeder writes development account keys and backups to the ignored
`docker/demo-keys` host-exchange directory:

```powershell
docker compose --env-file .env --profile seed run --rm demo-seeder
```

Those files contain sensitive local key material. Do not commit, share, or use
them outside an explicitly disposable development ledger.

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
