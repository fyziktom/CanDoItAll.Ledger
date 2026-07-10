# SB10 — Protocol-v2 token definitions and classification

Status: Complete — 2026-07-10

## Owned requirement

RQ-027 / R-003: add canonical immutable token definitions with hash-derived IDs while retaining v1 IDs and enforcing declared token semantics.

## Acceptance checklist

- immutable definition covers protocol version, namespace, symbol, issuer, initial supply, divisibility, and classification;
- token ID derives from canonical definition bytes; display metadata is stored separately;
- v1 canonical golden vectors are unchanged and v1 registry rows remain usable;
- v2 issuance requires matching permission namespace/issuer and exact initial supply;
- fungible outputs cannot carry BOs;
- BO-carrier amount-one outputs require BO identity, and source outputs greater than one cannot carry BOs;
- both stores persist/read registry metadata and replay can reconstruct v2 transactions.

## C# Architecture Impact

Core owns protocol contracts; Cryptography owns canonicalization/hash; Ledger owns validation; stores own representation; builders expose an opt-in v2 path.

## Boundary Ownership

No EF or host type enters Core/Ledger. V1 and v2 share transfer types but not definition identity rules.

## Dependency Direction

Existing inward direction is retained. No new project is required for the token protocol.

## Pattern Decision

Strategy is used only at the versioned replay boundary; token identity uses cohesive canonicalizer/hasher services. Rejected: deriving IDs from display metadata or changing v1 canonical bytes.

## Testability Contract

Golden vector, tampered-ID, supply mismatch, authorization, classification, store round-trip, builder, and v1 compatibility tests.

## Partial Class Policy

No new partial class.

## Architecture Proof Required

Canonical vectors, migration metadata proof, negative policy matrix, source assertions, dependency proof, architecture review.

## Progression gate

SB13 requires both a realistic v2 create/split/BO/recycle-compatible flow and unchanged v1 full-suite proof.

## Closure evidence

- Golden token ID: `token:v2:7823e24911fd358347232b27e1f384066f3debcb035f60c9031fd9de6cef93e4`.
- Display metadata does not affect identity; immutable definition fields do.
- Tampered ID, supply mismatch, divisibility, fungible/BO misuse, and unauthorized creation are rejected.
- InMemory and EF metadata round trips pass; legacy v1 token IDs remain addressable.
- The v1 canonical golden suite remains green inside the 361-test regression.
