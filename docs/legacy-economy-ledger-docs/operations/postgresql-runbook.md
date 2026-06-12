# PostgreSQL Operations Runbook

This runbook covers the current EF Core/PostgreSQL storage path. It is intentionally conservative: confirmed transaction rows are canonical, while UTXO, spent-output, token-type, address-projection, BusinessObject current/version, and ledger-state rows are derived and rebuildable.

## Current Status

- EF confirmation uses one transaction for validation reads, row locks where supported, append, sequence/hash state, and derived rows.
- PostgreSQL provider wiring exists through `CanDoItAll.Economy.Persistence.PostgreSql`.
- Gate A concurrency tests cover double-spend and duplicate-token mint races.
- `EfLedgerProjectionRebuilder` can rebuild derived EF rows for one ledger/scenario.
- `EfLedgerProjectionVerifier` can hash-check persisted derived rows, and `EfLedgerSnapshotService` can create/verify signed EF snapshots.
- `/api/v1/health` is cheap liveness; `/api/v1/health/readiness` reports latest ledger sequence/hash, chain/projection verification status, storage, security profile, auth modes, signer mode, schema management, reset posture, and trusted-admin verification requirement.
- The production security profile fails startup when unsafe development defaults remain.
- Formal EF migrations and automated migration validation are still next operational hardening work.

## Production Node Preflight

Before a node is reachable outside a local development machine:

1. Set `Economy:Security:Profile=Production`.
2. Set `Economy:Auth:Api:WriteMode=RequireJwtBearer` and `Economy:Auth:Api:ReadMode=RequireJwtBearer` for VPS-hosted API access, or use `RequireApiKey` only for a deliberately private API-key deployment.
3. Set `Economy:Api:MapLegacyRoutes=false` and `Economy:Api:AllowLedgerReset=false`.
4. Set `Economy:Persistence:Provider=PostgreSql`, a non-default PostgreSQL connection string, and `Economy:Persistence:SchemaManagementMode=Migrate` or `None`.
5. For VPS hosting, set `Economy:Hosting:VpsMode=true`, `Economy:Auth:Ui:Mode=Cookie`, persistent `Economy:Auth:Persistence:*`, persistent `Economy:Auth:DataProtection:KeyRingPath`, and a strong `CANDOITALL_ECONOMY_JWT_SIGNING_KEY`.
6. Configure non-default treasury key material and a bounded treasury bootstrap amount, or disable bootstrap for an already initialized ledger.
7. Configure `Economy:LedgerAdmin:*` key material and trusted public key publication for verification.
8. Put the node behind TLS/perimeter controls with forwarded headers enabled when TLS terminates at the proxy.
9. Verify `/api/v1/health/readiness` with the configured API credential and confirm `verificationStatus` is `valid_trusted` for non-empty production ledgers.

## Migration Preflight

1. Stop writers or route traffic away from the node.
2. Take a database backup or create a restored copy for rehearsal.
3. Record current latest ledger sequence and hash for each ledger/scenario.
4. Run chain verification on confirmed transaction history.
5. Run projection verification and record the projection hash.
6. Run application smoke tests against the restored copy.

## Applying Migrations

Use only reviewed EF migrations or reviewed SQL scripts. The current reference schema lives at `sql/migrations/0001_reference_schema.sql`; do not treat it as a complete operational migration history yet.

Recommended policy before production:

- add committed EF migrations for schema changes;
- run migrations against a disposable PostgreSQL database in CI;
- verify unique indexes for transaction ids, outpoint keys, spent keys, token-type keys, and ledger state keys;
- run the projection rebuild test suite after migration;
- document every manual SQL script with rollback and validation steps.

## Projection Rebuild

Use projection rebuild when derived rows are missing, corrupted, or intentionally dropped during a migration rehearsal.

CLI path:

```text
dotnet run --project src/CanDoItAll.Economy.Cli/CanDoItAll.Economy.Cli.csproj -- verify chain --provider postgres --connection "<connection>"
dotnet run --project src/CanDoItAll.Economy.Cli/CanDoItAll.Economy.Cli.csproj -- verify projections --provider postgres --connection "<connection>"
dotnet run --project src/CanDoItAll.Economy.Cli/CanDoItAll.Economy.Cli.csproj -- projections rebuild --provider postgres --connection "<connection>"
dotnet run --project src/CanDoItAll.Economy.Cli/CanDoItAll.Economy.Cli.csproj -- snapshot create --provider postgres --connection "<connection>"
```

Snapshot creation requires `CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY` and `CANDOITALL_ECONOMY_LEDGER_ADMIN_PRIVATE_KEY`.

Programmatic path:

```csharp
var rebuilder = new EfLedgerProjectionRebuilder(dbContextFactory, transactionHasher);
EfLedgerProjectionRebuildResult result =
    await rebuilder.RebuildAsync(new LedgerId("default"), scenarioId: null, cancellationToken);
```

After rebuild:

- compare `CurrentSequence` and `CurrentLedgerHash` with confirmed transaction history;
- run chain verification;
- run projection verification and compare the projection hash;
- validate address projection counts and state hashes;
- validate BusinessObject current/version counts against known append-time expectations;
- run node read smoke tests for balances, spendable outputs, BusinessObject holdings, token registry, and transaction history.

## Rollback

Rollback means restoring a known-good backup or promoted replica. Do not edit canonical transaction rows by hand. If a migration only affected derived rows, restore or rebuild derived rows from confirmed history, then rerun verification.

## Incident Checklist

- Leaked development/default key: rotate treasury and ledger-admin material, disable affected node, verify no production node used `Development` signer or default treasury material, and review audit logs for mint/transfer/grant/reset activity.
- Suspected ledger tamper: stop writers, take a forensic backup, run strict trusted-key chain verification and projection verification, compare latest sequence/hash with previous readiness records, and restore from backup if canonical history is invalid.
- Double-spend or conflict storm: inspect rejected `spent_input`, `unknown_input`, `duplicate_transaction`, `duplicate_token_type`, and `ledger_state_conflict` errors, then confirm row locks and unique indexes are present.
- Migration failure: stop writers, restore the pre-migration backup or promoted replica, rerun chain/projection verification, and do not patch canonical rows manually.
- Sensitive read exposure: set read security to `RequireJwtBearer`, rotate exposed credentials, inspect access logs, and verify transaction details omit inline data unless explicitly requested by an authorized caller.
- Exposed JWT: revoke the token from `/admin/jwts/`, inspect API access logs, issue a replacement with a shorter lifetime, and rotate `CANDOITALL_ECONOMY_JWT_SIGNING_KEY` if the signing key may also be exposed.
- Exposed admin cookie or stolen admin device: lock or reset the Identity user, require 2FA, rotate data-protection keys only as a last resort because it invalidates all current cookies and Identity tokens.

## Open Operational Work

- Generate and commit formal EF migrations.
- Add repeatable containerized PostgreSQL migration tests.
- Add alerting policy for node health/readiness issue codes and verification freshness.
- Integrate admin private keys with a managed secret store or HSM-compatible provider.
- Add provider-specific deployment examples once a target VPS/proxy stack is chosen.
