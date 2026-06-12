# Snapshots and archives

## Why snapshots

Transaction and spent-index tables will grow. Snapshots let the node verify and rebuild from a recent state without replaying the whole ledger every time.

## Snapshot boundary

Create snapshots by sequence or time:

```text
every 100000 transactions
or every month
or manually before archive compaction
```

## Current snapshot contents

Gate B implements signed EF snapshots for the current ledger/scenario state. Snapshot creation first verifies persisted projections and refuses to sign invalid state. A snapshot currently stores:

- ledger sequence and ledger entry hash;
- current UTXO set hash;
- address projection state hash;
- BusinessObject current state hash;
- creation timestamp;
- admin signature over the deterministic snapshot payload.

`EfLedgerProjectionVerifier` also calculates spent-output, token-type, BusinessObject-version, and full projection hashes. Those hashes are verified as part of projection verification; the compact `LedgerSnapshot` contract currently persists the subset above.

## Snapshot manifest

```text
snapshotId
ledgerId
scenarioId
ledgerSequence
ledgerHash
utxoSetHash
addressStateHash
businessObjectStateHash
createdAt
adminSignature
```

Future archive manifests can add token-type, spent-set high-water mark, address-registry, and full projection hashes before cold archive compaction is enabled.

## Archive rule

Only archive old confirmed transactions after a signed snapshot exists after those transactions.

## Archive format

Start with PostgreSQL partitions for warm archives. Add binary archive files later:

```text
ledger-archive-{ledgerId}-{fromSequence}-{toSequence}.bin.zstd
ledger-archive-{ledgerId}-{fromSequence}-{toSequence}.manifest.json
ledger-archive-{ledgerId}-{fromSequence}-{toSequence}.index
```

## Verification

Snapshot verification checks:

- current ledger sequence and ledger hash match the snapshot;
- current UTXO, address, and BusinessObject hashes match the snapshot;
- snapshot signature payload hash matches the deterministic snapshot payload;
- snapshot signature verifies cryptographically when public key material is present.

Archive verification is future work and must check:

- archive manifest admin signature;
- transaction hashes;
- sequence range continuity;
- boundary hash equals the snapshot chain input;
- no missing or duplicate ledger sequences.
