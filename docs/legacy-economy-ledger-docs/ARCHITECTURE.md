# Architecture

## Purpose

CanDoItAll.Economy is not a distributed blockchain. It is a single-node accounting and simulation ledger inspired by UTXO systems, token transfers, NTP1-like metadata, and NFT-like object ownership. The user-facing name for NFT-like objects is `BusinessObject`.

## Layering

```text
CanDoItAll.Economy.Core
  Domain values, contracts, immutable transaction model.

CanDoItAll.Economy.Cryptography
  Canonical serialization, transaction hashing, signature payloads, signature providers.

CanDoItAll.Economy.Ledger
  Validation, confirmation, ledger execution, projection update orchestration.

CanDoItAll.Economy.Persistence.PostgreSql
  PostgreSQL persistence, migrations, transaction locking, chain verification reads.

CanDoItAll.Economy.Storage.InMemory
  Deterministic in-memory store for tests and dry-run simulations.

CanDoItAll.Economy.Node
  Headless node service, address registry, ledger admin key management, background verification.

CanDoItAll.Economy.Cli
  Minimal operator tool. No UI dependency.

CanDoItAll.Economy.Sdk
  Public facade for CanDoItAll modules, future UI, and integrations.
```

## Runtime modes

### CLI-only node

A small local process or command invocation can initialize a ledger, submit transactions, update projections, and verify chain integrity. This is ideal for development, simulations, and small deployments.

### Headless node service

A hosted service can expose local API endpoints later. It owns the ledger admin key and writes admin signatures over confirmed ledger state.

### UI node

A future UI should use the SDK and must not talk directly to storage tables for writes. UI reads may query projections, but all writes must go through the ledger executor.

## Source-of-truth model

```text
Confirmed ledger transactions + ledger entry hash chain
    -> current UTXO set
    -> spent output index
    -> address/account projections
    -> BusinessObject current projections
    -> reports, cash-flow views, simulations
```

Only confirmed transactions and ledger entries are canonical. Everything else can be rebuilt.

## Why hybrid UTXO + account projections

UTXO is excellent for ownership, splitting, joining, transfer authorization, and double-spend prevention. Account projections are necessary for practical wallet UX, fast balance checks, dashboards, and cash-flow planning.

The node behaves like a wallet indexing service for known addresses, but this index is not a separate source of truth. Gate A does not require output addresses to be pre-registered; it enforces derived-address validity and signature ownership.

## Admin signatures

The accounting admin signs:

- confirmed ledger entries;
- spent output index rows;
- address projection state hashes;
- snapshot manifests;
- archive manifests.

This is not a consensus mechanism. It is tamper evidence against direct database writes or accidental data corruption.

## Recommended deployment for MVP

```text
PostgreSQL database
CanDoItAll.Economy.Node process or CLI
Admin private key stored outside DB
Known wallet public keys or an explicit admission policy in DB when required
Periodic snapshot and verification jobs
```

## Repository boundaries

Do not put CanDoItAll process-engine logic into the ledger. The ledger should know about BusinessObject metadata, not about specific project-management semantics. Higher modules can define BusinessObject schemas and use the ledger as economic infrastructure.
