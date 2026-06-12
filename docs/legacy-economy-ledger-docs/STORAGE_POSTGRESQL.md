# PostgreSQL storage design

## Why PostgreSQL

PostgreSQL is the primary storage engine for production because it provides transactional writes, row-level locks, unique indexes, JSONB support for flexible metadata, partitioning, and mature backup tooling.

Do not build a custom binary database for MVP. Use custom binary files later for snapshots, cold archives, and export/import bundles.

## Hot tables

Hot tables are needed for new transaction validation:

- `ledger_state`
- `registered_addresses`
- `transactions` recent headers
- `transaction_outputs`
- `unspent_outputs`
- `spent_outputs`
- `ledger_token_types`
- `address_balances`
- `business_objects`

## Warm tables

Warm tables are used for normal history queries:

- `transactions`
- `transaction_inputs`
- `transaction_outputs`
- `business_object_versions`
- `ledger_entries`

## Cold data

Cold data can be archived after snapshot boundaries:

- old transaction payload JSON;
- old BusinessObject payload JSON;
- archived transaction partitions;
- compressed binary archive files;
- archive manifest signed by the admin key.

## Recommended indexes

```sql
create unique index ux_transactions_ledger_txid on transactions(ledger_id, scenario_id, tx_id);
create unique index ux_transaction_outputs_outpoint on transaction_outputs(ledger_id, scenario_id, tx_id, output_index);
create unique index ux_unspent_outputs_outpoint_key on unspent_outputs(outpoint_key);
create unique index ux_spent_outputs_outpoint_key on spent_outputs(outpoint_key);
create unique index ux_ledger_token_types_key on ledger_token_types(token_type_key);
create index ix_unspent_outputs_address on unspent_outputs(ledger_id, scenario_id, address);
create index ix_spent_outputs_spent_in_tx on spent_outputs(ledger_id, scenario_id, spent_in_tx_id);
create index ix_business_objects_owner on business_objects(ledger_id, scenario_id, current_owner_address);
create index ix_ledger_entries_sequence on ledger_entries(ledger_id, scenario_id, ledger_sequence);
```

## Projection signatures

Do not sign large projection blobs directly. Sign state hashes.

```text
addressStateHash = SHA-256(
  address ||
  ledgerSequence ||
  sortedAvailableOutPointKeys ||
  mainBalance ||
  sortedTokenBalances
)
```

## Partitioning

For high-volume ledgers, partition history tables by `ledger_sequence` or `confirmed_at`. Keep `unspent_outputs` unpartitioned unless the UTXO set becomes too large.

Recommended initial plan:

- no partitioning in MVP;
- add monthly partitions later for `transactions`, `transaction_inputs`, `transaction_outputs`, `business_object_versions`, and `ledger_entries`;
- never archive without a signed snapshot.

## Data types

Use text ids for portability in MVP:

- `tx_id char(64)` lowercase hex;
- `tx_hash char(64)` lowercase hex;
- `outpoint_key char(64)` lowercase hex;
- `address text`;
- `amount numeric` for current decimal `Amount` values;
- `jsonb` for metadata and BO payloads;
- `bytea` for signatures and public keys.

## Transaction isolation

The executor should use a database transaction and lock the ledger state row:

```sql
select * from ledger_state
where ledger_id = @ledger_id and scenario_id is not distinct from @scenario_id
for update;
```

This serializes confirmation per ledger and protects the hash chain.

Gate A also locks selected input UTXO rows during EF confirmation and uses a unique token-type key so duplicate token mint races produce one accepted transaction and one rejected transaction.

The node now treats production schema management as explicit configuration. `EconomyNodeSchemaManagementMode.EnsureCreated` remains available for development and tests, but the production security profile rejects it. Use reviewed EF migrations or reviewed SQL scripts for durable deployments, and set the node schema mode to `Migrate` or `None` according to the deployment pipeline.

## Operations runbook

The current production path is EF Core over PostgreSQL. Before a database is treated as durable production storage:

- create a backup or disposable restored copy;
- configure the node with `SecurityProfile = Production`, `PersistenceProvider = PostgreSql`, a non-default connection string, protected read/write API credentials, disabled legacy routes, disabled reset, configured treasury/admin keys, and non-`EnsureCreated` schema management;
- for VPS hosting, configure `VpsHostingMode`, cookie UI auth, JWT API auth, persistent auth storage, and a persistent data-protection key ring as described in `docs/VPS_HOSTING.md`;
- run chain verification against confirmed transactions with `economy verify chain`;
- run projection verification with `economy verify projections`;
- run projection rebuild on a restored copy with `economy projections rebuild` and compare derived row counts and state hashes;
- create or verify a signed snapshot before archive or major migration work;
- apply migrations only through reviewed EF migrations or reviewed SQL scripts;
- rerun verification and node smoke tests after migration;
- keep rollback as restore-from-backup, not manual edits to canonical transaction rows.

See `docs/operations/postgresql-runbook.md` for the current runbook. The repository has a reference schema at `sql/migrations/0001_reference_schema.sql`; formal EF migration generation and containerized migration validation remain next operational hardening work.
