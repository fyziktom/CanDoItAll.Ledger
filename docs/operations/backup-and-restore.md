# PostgreSQL Backup And Restore

## Data Classification

The Compose `postgres-data` volume is authoritative durable development data.
It contains PostgreSQL 16 data and the EF Core Ledger schema and migration
history.

The `node-dpkeys` volume contains ASP.NET Core Data Protection keys used for
authentication and protected application state. It is operational continuity
data, not transaction-signing material and not a substitute for a database
backup. Preserve it when users must retain existing protected sessions or
tokens. Regenerating it invalidates data protected by the previous key ring but
does not alter signed Ledger transactions.

The local stack has no automatic backup schedule. Before a schema change,
Docker or host upgrade, or important experiment, create a database-native
backup and keep it encrypted outside the repository. Production deployments
must define frequency, retention, encryption, RPO, RTO, access control, and
restore testing in their deployment platform.

## Create A Logical Backup

With the stack healthy:

```powershell
New-Item -ItemType Directory -Force .\backups | Out-Null
docker compose --env-file .env exec -T postgres `
  pg_dump -U candoitall -d candoitall_ledger `
  --format=custom --file=/tmp/candoitall-ledger.backup
docker compose --env-file .env cp `
  postgres:/tmp/candoitall-ledger.backup `
  .\backups\candoitall-ledger.backup
```

Use the `POSTGRES_USER` and `POSTGRES_DB` values from `.env` when they differ.
Move the backup to approved encrypted storage after creation. Ignored
`backups/` is a short-lived staging location only.

## Restore Into A New Empty Volume

Do not overwrite the only known-good volume. Start a separately scoped Compose
project so the restore uses a new `postgres-data` volume:

```powershell
$restoreProject = "candoitall-ledger-restore"
docker compose --env-file .env -p $restoreProject up -d postgres --wait
docker compose --env-file .env -p $restoreProject cp `
  .\backups\candoitall-ledger.backup `
  postgres:/tmp/candoitall-ledger.backup
docker compose --env-file .env -p $restoreProject exec -T postgres `
  pg_restore -U candoitall -d candoitall_ledger `
  --clean --if-exists /tmp/candoitall-ledger.backup
docker compose --env-file .env -p $restoreProject exec -T postgres `
  psql -U candoitall -d candoitall_ledger -c "select 1"
```

Connect a disposable node to the restored database and verify
`/api/v1/health`, representative reads, and a controlled write before cutover.
Keep the original volume until the restored system is accepted.

## Upgrade And Rollback

Before changing the PostgreSQL major version:

1. Create and verify a logical backup.
2. Restore it into a new project and volume using the target major version.
3. Run Ledger migrations, health checks, and representative reads and writes.
4. Cut over only after validation.
5. Roll back by returning to the untouched original project and volume.

Do not mount a data directory created by a newer PostgreSQL major version into
an older server.

Normal teardown uses `docker compose down` and preserves volumes. Any reset
using `down --volumes` is destructive and must name the exact disposable
Compose project first.
