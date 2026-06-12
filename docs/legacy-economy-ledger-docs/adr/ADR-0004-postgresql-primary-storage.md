# ADR-0004: PostgreSQL is the primary storage for MVP

## Status

Accepted

## Context

The ledger requires transactional writes, locking, indexes, JSON metadata, migrations, and operational reliability.

## Decision

Use PostgreSQL as the production storage backend. Use custom binary formats only for snapshots, archives, and export/import after MVP.

## Consequences

- Easier implementation and debugging.
- Strong transaction semantics.
- Uses existing CanDoItAll infrastructure.
- Archive compaction remains a future optimization.
