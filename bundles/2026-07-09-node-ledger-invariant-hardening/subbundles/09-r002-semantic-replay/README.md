# SB09 — Version-aware semantic replay

Status: Complete — 2026-07-10

## Owned requirement

RQ-026 / R-002: readiness must replay persisted history through the semantic rule set matching each transaction version.

## Acceptance checklist

- rule-set registry selects by exact protocol version;
- v1 semantics are frozen in a named v1 rule set;
- replay uses isolated state and cannot mutate durable storage;
- hash-valid semantic-invalid history is reported invalid;
- unsupported versions are explicit, not silently treated as current;
- readiness requires chain, semantic replay, and projections to pass.

## C# Architecture Impact

Ledger owns versioned semantic rule sets/verifier; Node composes a replay-only in-memory state driver.

## Boundary Ownership

No persistence query or readiness rule moves into Core. The history reader remains EF infrastructure.

## Dependency Direction

Node may reference Storage.InMemory as an outer composition dependency; Ledger must not reference storage or EF.

## Pattern Decision

Strategy plus registry because protocol versions are independently stable algorithms. Rejected: applying the latest validators to every historical row or a version switch in Node.

## Testability Contract

Direct verifier tests cover valid history, semantic invalidity with valid hashes, unsupported version, and rule-set selection.

## Partial Class Policy

No new partial class.

## Architecture Proof Required

Direct rule-set tests, Node readiness composition, dependency proof, source assertion, anti-stub audit.

## Progression gate

SB10 must register v2 independently and demonstrate v1 behavior is unchanged.

## Closure evidence

- Exact-version v1/v2 rule strategies are registered once and duplicate registration fails.
- Replay writes only to a fresh `InMemoryLedgerStore`, never durable state.
- Hash-valid asset imbalance and unsupported version tests fail explicitly.
- Docker readiness at sequence 13 reports `valid_semantic_payload`, zero semantic issues, and a valid projection.
