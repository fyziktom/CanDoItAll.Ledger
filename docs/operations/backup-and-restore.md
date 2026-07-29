# PostgreSQL Backup And Restore

## Data Classification

The Compose `postgres-data` volume is authoritative durable development data.
Its owner is the Ledger repository operator. It contains PostgreSQL 16 data and
the EF Core Ledger schema/migration history. The `node-dpkeys` volume contains
development Data Protection keys and must be preserved with a matching node
identity when encrypted application state depends on it.

The local stack has no automatic backup schedule. Before a schema change,
Docker/host upgrade, or important local experiment, create a database-native
backup and keep it encrypted outside the repository. Production deployments
must define their own frequency, retention, encryption, RPO, and RTO in the
deployment platform; this development stack is not a production backup policy.

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
Store the backup outside the repository after creation; `backups/` is a
short-lived local staging location only.

## Restore Into A New Empty Volume

Do not overwrite the only known-good volume. Start a separately scoped restore
project so Compose creates a new `postgres-data` volume:

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

After validation, connect a disposable node to the restore project and verify
`/api/v1/health` plus representative ledger reads before any cutover. Preserve
the original volume until the restored system is accepted.

## Upgrade And Rollback

Before changing the PostgreSQL major version:

1. Create and verify a logical backup.
2. Restore it into a new project/volume using the target major version.
3. Run Ledger migrations, health checks, and representative read/write tests.
4. Cut over only after validation.
5. Roll back by returning to the untouched original project/volume; do not try
   to mount a data directory created by a newer major version into an older
   server.

Normal teardown uses `docker compose down` and preserves volumes. Any reset that
uses `down --volumes` is destructive and must name the exact disposable Compose
project first.
