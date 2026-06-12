# ADR-0002: Single-node ledger, not distributed blockchain

## Status

Accepted

## Context

The system does not need trustless distributed consensus. It needs deterministic internal economic state and tamper evidence.

## Decision

Use a single-node append-only ledger with admin-signed entries and hash chaining. Do not implement mining, validators, peer-to-peer networking, mempool, chain reorg, or block consensus.

## Consequences

- Less infrastructure and less code.
- Admin key becomes operationally important.
- Verification focuses on tamper evidence and deterministic replay.
