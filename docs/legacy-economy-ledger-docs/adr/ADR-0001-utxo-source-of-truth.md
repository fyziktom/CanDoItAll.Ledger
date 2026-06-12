# ADR-0001: UTXO is the source of truth

## Status

Accepted

## Context

The system needs auditability, ownership, split/join budget operations, double-spend prevention, and simulation.

## Decision

Confirmed UTXOs and the append-only ledger chain are the canonical state. Address balances and account views are projections.

## Consequences

- Wallet UX requires projection tables.
- Projections must be rebuildable.
- Validation always checks input outpoints.
- Account balance alone cannot authorize spending.
