# EF Core storage design

EF Core is the primary persistence implementation.

## Why EF Core

CanDoItAll already uses EF Core. Matching that stack keeps migrations, diagnostics, DbContext factory usage, provider versions, and tests compatible with the current repository.

## Source of truth

The canonical truth is:

```text
confirmed transactions + ledger entry hash chain
```

The current UTXO set, spent-output index, token-type registry, address/account state, and BusinessObject current/version rows are persisted indexes/projections maintained atomically during confirmation. They exist so validation and reads do not require replaying history for every request.

## Tables

- `Economy_LedgerStates`
- `Economy_LedgerTransactions`
- `Economy_LedgerTransactionInputs`
- `Economy_LedgerTransactionOutputs`
- `Economy_UnspentOutputs`
- `Economy_SpentOutputs`
- `Economy_LedgerTokenTypes`
- `Economy_AddressStateProjections`
- `Economy_RegisteredAddresses`
- `Economy_BusinessObjects`
- `Economy_BusinessObjectVersions`
- `Economy_LedgerSnapshots`

## Amount storage

Canonical transaction hashing serializes amounts as invariant decimal strings. EF storage currently persists amount columns as `decimal` values for queryability. Do not use floating-point columns for money or token amounts.

## Transaction data typing

`Economy_LedgerTransactions` stores `DataItemType` beside content type, hash, inline JSON, and external reference. Querying settings BusinessObjects or token metadata must use this discriminator instead of parsing JSON payloads.

## Nullable scenarios

The real ledger uses `ScenarioId = null`. Simulations use a concrete scenario id. EF primary keys therefore use deterministic string keys for projection rows and state rows; nullable scenario ids are used in indexes, not as nullable primary-key components.

## Confirmation transaction

All confirmation mutations must run inside one EF database transaction:

```text
begin
  lock ledger state
  lock input UTXOs
  validate existence and ownership
  validate spent index
  validate signatures
  validate conservation
  validate token creation policy
  insert confirmed transaction rows
  remove consumed UTXOs
  insert spent output rows
  insert new UTXOs
  insert token type rows for new token ids
  update BusinessObject current/version projections
  rebuild touched address projections
  sign projection hashes
  update ledger state hash
commit
```

The EF confirmation store implements `ILedgerConfirmationStore`, opens a relational transaction, uses Npgsql `FOR UPDATE` locking for ledger state and selected input UTXO rows, evaluates the ledger confirmation callback against that context, and then persists the accepted transaction. `Economy_LedgerTokenTypes` has a deterministic key per ledger/scenario/token id so duplicate token mint races collapse to one accepted transaction.

## Projection rebuild

`EfLedgerProjectionRebuilder` rebuilds derived EF rows from confirmed transaction rows for one ledger/scenario. It deletes and regenerates:

- `Economy_LedgerStates`;
- `Economy_UnspentOutputs`;
- `Economy_SpentOutputs`;
- `Economy_LedgerTokenTypes`;
- `Economy_AddressStateProjections`;
- `Economy_BusinessObjects`;
- `Economy_BusinessObjectVersions`.

It does not rewrite canonical transaction/input/output rows. BusinessObject canon for this stage treats confirmed history plus BO-bearing UTXO transitions as truth; `Economy_BusinessObjects` and `Economy_BusinessObjectVersions` are projections that must match append-time UTXO-derived state.

Use the rebuilder after projection corruption, after a verified migration that changes derived-row shape, or as a maintenance validation step on a restored copy. Do not run it as a substitute for transaction confirmation.

```csharp
var rebuilder = new EfLedgerProjectionRebuilder(dbContextFactory, transactionHasher);
EfLedgerProjectionRebuildResult result =
    await rebuilder.RebuildAsync(new LedgerId("default"), scenarioId: null, cancellationToken);
```

The current test proof deletes EF derived rows and asserts that rebuild output matches append-time UTXO, spent-output, token-type, address-projection, BusinessObject current/version, and ledger-state rows.
