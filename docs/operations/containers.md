# Local Containers

The canonical development stack is `compose.yaml`. It builds the Ledger node,
three static AccountApp instances, and the optional DemoSeeder, and runs
PostgreSQL 16 on an internal backend network.

## Configuration

Copy the committed non-secret template and replace both password placeholders:

```powershell
Copy-Item .env.example .env
```

`.env` is ignored. It supplies Compose interpolation only and is not a
production secret store. Production credentials must come from the deployment
platform or approved secret manager.

Default loopback endpoints:

| Service | URL |
| --- | --- |
| Ledger node | `http://127.0.0.1:8080` |
| Alice AccountApp | `http://127.0.0.1:5081` |
| Bob AccountApp | `http://127.0.0.1:5082` |
| Lab AccountApp | `http://127.0.0.1:5083` |

PostgreSQL is not published to the host. Containers use the `postgres` service
name and port 5432. The AccountApps proxy `/api/` to the `node` service and
publish only loopback ports.

## Validate And Start

```powershell
.\tools\validation\Test-Docker.ps1 -EnvFile .env
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build --wait --wait-timeout 300
docker compose --env-file .env ps --all
```

The node healthcheck calls `/api/v1/health`; AccountApp healthchecks load their
Nginx root. Long-form readiness dependencies prevent the node from starting
before PostgreSQL is healthy and prevent AccountApps/Seeder from starting before
the node is ready.

`Test-Docker.ps1 -RunBuildChecks` uses Compose-aware Dockerfile checks because
the node and AccountApp images declare the sibling Components repository as a
named build context. The portable SharedInfo validator still owns all other
Docker policy checks.

## Seed A Disposable Development Ledger

The Seeder is optional and writes development keys/backups to the ignored
`docker/demo-keys` host-exchange directory:

```powershell
docker compose --env-file .env --profile seed run --rm demo-seeder
```

Those files contain sensitive local key material. Do not commit, share, or use
them outside an explicitly disposable development ledger.

## Stop Without Deleting Data

```powershell
docker compose --env-file .env down --remove-orphans
```

This preserves the `postgres-data` and `node-dpkeys` named volumes. Never add
`--volumes` to the normal stop path. Volume deletion is an explicit reset and
permanently removes local ledger/database state.

## Runtime Safety

- Application ports bind to `127.0.0.1` by default.
- Images run as non-root users; node capabilities are dropped and its root
  filesystem is read-only.
- Logs use Docker's bounded `local` driver.
- CPU, memory, and PID defaults are configurable in `.env`.
- The base development stack uses `restart: "no"` so failures remain visible.

The base Compose file is development-only. There is no production overlay in
this repository; do not deploy it as a production model.
