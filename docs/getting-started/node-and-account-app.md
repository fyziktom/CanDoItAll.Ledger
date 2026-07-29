# Node And AccountApp First Setup

This guide starts a PostgreSQL-backed Ledger node and the AccountApp WebAssembly
client, creates or imports a Ledger account, and proves that an external client
can reach the versioned Node API.

The Compose defaults are intended for loopback development. They deliberately
enable development conveniences that are rejected by the Node production
security profile.

## 1. Prerequisites

- Docker Desktop or another Docker Engine with Compose v2
- Git
- PowerShell 7 for the commands shown here

The .NET SDK is not required when using only the container path. It is required
for package development or a .NET driver built from source.

## 2. Create Local Configuration

From the repository root:

```powershell
Copy-Item .env.example .env
```

Open the ignored `.env` and replace:

- `POSTGRES_PASSWORD`;
- `LEDGER_ACCOUNT_BACKUP_PASSWORD`.

Use development-only values that are not reused elsewhere. `.env` is a Compose
input, not a production secret store.

Keep `CDA_PUBLIC_HOST=127.0.0.1` when using the endpoints in this guide. If the
app is deliberately published under another hostname, set `CDA_PUBLIC_HOST` to
that browser-visible hostname and use it consistently; the AccountApp origin
must match its generated Node base URI exactly.

Validate the resolved model without printing all interpolated values:

```powershell
docker compose --env-file .env config --quiet
```

## 3. Start The Core Stack

```powershell
docker compose --env-file .env up -d --build --wait --wait-timeout 300
docker compose --env-file .env ps --all
```

The default graph contains:

| Service | Host endpoint | Role |
| --- | --- | --- |
| `postgres` | not published | Authoritative development Ledger database |
| `node` | `http://127.0.0.1:8080` | API and embedded operator UI |
| `accountapp` | `http://127.0.0.1:5081` | Static WebAssembly account client |

PostgreSQL is isolated on the internal backend network. AccountApp serves
static files and proxies browser requests from `/api/` to the `node` service.

Verify both applications:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/api/v1/health
Invoke-RestMethod http://127.0.0.1:8080/api/v1/health/readiness
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5081/
```

The readiness response identifies an Npgsql/PostgreSQL database provider.

## 4. Understand The Two Account Types

A Node operator identity and a Ledger account solve different problems:

- The operator identity is an ASP.NET Identity user for a protected Node UI and
  JWT administration.
- The Ledger account is a cryptographic address and signing-key pair used for
  Ledger transactions.

The development Compose model leaves the operator UI authentication disabled.
AccountApp creates Ledger accounts only. It never registers an operator user,
and public operator registration is disabled by the Node.

## 5A. Create A Fresh Browser Account

1. Open `http://127.0.0.1:5081`.
2. Under **Create account**, enter an account name and a new vault password.
3. Select **Create encrypted vault**.
4. Copy the address displayed in the account workspace.
5. Open `http://127.0.0.1:8080/transactions`.
6. In the development **Mint** form, paste the account address and enter a
   small main amount.
7. Submit the mint transaction.
8. Return to AccountApp and refresh the workspace.
9. Export an encrypted backup and store it somewhere protected.

The account is generated and encrypted in the browser. The Node does not
receive the private key. A new address is not visible in Ledger state until a
confirmed transaction creates an output for it.

## 5B. Import A Seeded Development Account

The optional Seeder prepares funded test accounts and encrypted AccountApp
imports:

```powershell
.\scripts\seed-demo.ps1 -EnvFile .env
```

Imports are written below the ignored directory:

```text
docker/demo-keys/accountapp-demo/imports/
```

To import Alice:

1. Open `alice-accountapp-backup.json`.
2. Open AccountApp at `http://127.0.0.1:5081`.
3. Choose **Import account** and **Encrypted backup**.
4. Paste the JSON.
5. Use `LEDGER_ACCOUNT_BACKUP_PASSWORD` from `.env` as the backup password.
6. Choose and confirm a new browser-vault password.
7. Complete the import.

The Seeder output contains development private-key material. Never commit,
share, publish, or reuse `docker/demo-keys`.

## 6. Run Additional AccountApp Origins

Each origin owns separate IndexedDB storage. Start the optional Bob and Lab
instances with:

```powershell
docker compose --env-file .env --profile multi-account up -d --wait
```

They are available at:

- `http://127.0.0.1:5082`
- `http://127.0.0.1:5083`

An account created on one port does not automatically exist on another. Use an
encrypted export/import when the same account must move between origins.

## 7. Connect A Driver

The versioned API base is:

```text
http://127.0.0.1:8080/api/v1
```

Install the typed .NET client:

```powershell
dotnet add package CanDoItAll.Ledger.Sdk
```

Then follow [SDK And Driver Integration](../development/sdk-and-drivers.md) for
health, bootstrap, balance, authentication, and transaction examples.

## Persistence And Safe Teardown

The default stack creates:

- `postgres-data` for authoritative development Ledger state;
- `node-dpkeys` for authentication and protected application-state
  continuity.

Stop containers without deleting those volumes:

```powershell
docker compose --env-file .env down --remove-orphans
```

Do not add `--volumes` to normal teardown. Removing volumes deletes the local
database and Data Protection key ring. See
[Backup And Restore](../operations/backup-and-restore.md) before moving or
resetting valuable state.

## Troubleshooting

### AccountApp Loads But Cannot Reach The Node

Check the service and proxy:

```powershell
docker compose --env-file .env ps --all
docker compose --env-file .env logs node accountapp
Invoke-RestMethod http://127.0.0.1:8080/api/v1/health
```

The Compose AccountApp must use its own browser-visible origin as
`ACCOUNT_APP_NODE_BASE_URI`; Nginx handles the `/api/` hop to the `node`
service.

### PostgreSQL Password Changed After First Start

PostgreSQL initializes its user password only when the data volume is created.
Changing `.env` does not rewrite an existing database role. Restore the
original local password, change the role deliberately, or reset only an
explicitly disposable project after confirming that its volume can be lost.

### AccountApp Shows An Unexpected Old Configuration

The application is a PWA. Reload it, close other tabs for that origin, and
clear only the intended development origin's site data if a service worker or
IndexedDB state is intentionally being reset. Clearing site data removes its
local vault.

## Moving Beyond The Development Profile

Before exposing a Node beyond loopback:

- select `Ledger__Security__Profile=Production`;
- configure authenticated read and write APIs;
- provide non-default treasury and Ledger-admin signing material;
- disable legacy routes and ledger reset;
- use durable identity and Data Protection storage where required;
- supply secrets through the deployment platform;
- define PostgreSQL backup, restore, monitoring, and migration procedures;
- put the service behind reviewed TLS and proxy configuration.

The Node validates these production invariants at startup.
