# Security and architecture review

Status: Passed for the v1 hardening and approved R-001–R-005 implementation wave; residual risks are explicit.

## Review conclusion

No known Critical or High finding from the audited v1 transaction-confirmation path remains open. The refactor is genuine responsibility separation: validation logic moved into cohesive classes, the facade shrank, stores retain their atomicity responsibilities, and no partial class, service locator, cyclic project reference, or persistence dependency was introduced into Ledger/Core.

This conclusion does not claim that the development Docker profile is production secure, that NCalc's NU1701 is removed, or that an unrelated AppHost dependency advisory is repaired.

## Architecture gate

| Gate | Result | Evidence |
| --- | --- | --- |
| Cohesive responsibilities | Pass | Preflight, references, signatures, BO transitions, conservation, and pipeline are separate owners. |
| Thin facade | Pass | `LedgerExecutor` coordinates validation/confirmation and no longer contains reference/signature rule clusters. |
| Dependency direction | Pass | Core remains inward; Ledger depends on Core/Cryptography; stores depend on Core; Node is composition root. |
| No service locator | Pass | Dependencies are constructor supplied; no `IServiceProvider` lookup in domain validation. |
| No fake partial split | Pass | New responsibilities are standalone types, not partial fragments. |
| Atomicity | Pass | PostgreSQL behavior remains transaction/lock based; custom in-memory store now owns one serialized confirmation boundary. |
| Test seams | Pass | Extracted validators are directly callable; fake/counting dependencies and parallel store tests are present. |
| Compatibility | Pass | Canonical v1 structures/hashes unchanged; full golden-vector suite passes. |
| Analytics | Manual substitute | CodeAnalytics SDK-host failure recorded; `.csproj` inspection and green build/test provide evidence. |

## Closed security findings

- Critical mint-conservation bypass.
- Missing ledger-layer treasury authorization for positive main-currency Mint deltas.
- Permission/rule-token aggregate inflation during mint.
- Unsafe policy omission in direct executor composition.
- Unsafe main-currency issuance-policy omission in direct executor composition.
- In-memory time-of-check/time-of-use confirmation race.
- Pre-hash resource-exhaustion ordering.
- Decimal overflow escaping protocol validation.
- Empty and zero-token UTXOs.
- BO carrier swap, dishonest owner operation, and weak external-data binding.
- Chunked request-body size bypass at the endpoint-filter layer.
- Vulnerable SQLite native dependency version in the audited graph.

Defense in depth now includes relational checks, but those checks protect a database only after its schema is created or migrated from the updated model.

## Closed Medium roadmap/operational findings

### R-001: Existing database migration — closed

The PostgreSQL provider now owns two EF migrations plus a fail-closed legacy adoption workflow. It verifies the required schema, audits invalid rows before writing history, holds one advisory session lock across audit/adoption/migration/verification, and refuses incomplete or invalid databases. New, legacy, invalid, and concurrent startup paths are integration-tested.

### R-002: Semantic history replay — closed

Readiness now selects an exact semantic rule strategy by transaction version and replays ordered confirmed history into a fresh in-memory state. Unsupported versions and semantic failures are explicit. A valid non-empty Docker ledger reports `valid_semantic_payload` only when chain, semantic replay, and projection verification all pass.

### R-003: Token protocol definition — closed for protocol v2

Protocol v2 defines protocol version, namespace, symbol, issuer, initial supply, divisibility, and classification. The token ID is SHA-256-derived from canonical immutable bytes; display metadata is separate. Node policy enforces exact initial supply, issuer-backed authorization input, fungible/BO classification, and divisibility. V1 bytes and identifiers remain unchanged/addressable.

### R-004: JWT authorization scopes — closed

JWT scope now drives named read/write/admin policies. Standard `scope`/`scp`, hierarchy, legacy compatibility, issuance validation, insufficient-scope 403, and allowed live endpoints are tested. API-key and development bypass modes remain explicit configuration choices.

### R-005: Rule expression dependency — closed by isolation

`ncalc` 1.3.8 remains intentionally retained but now appears only in the dedicated `CanDoItAll.Economy.Rules.NCalc` adapter project. BusinessObjects owns the evaluator/result contracts and uses constructor injection. Compatibility vectors, DI, assembly isolation, and a fake evaluator prove replacement can occur without rewriting rule orchestration.

## Second-wave architecture gate

| Gate | Result | Evidence |
| --- | --- | --- |
| Migration ownership | Pass | EFCore seam; PostgreSQL adapter/migrations; Node only composes schema mode. |
| Migration serialization | Pass | Session advisory lock spans baseline and EF migration; two-start integration proof. |
| Version isolation | Pass | Exact v1/v2 semantic strategies; unknown versions fail. |
| Protocol compatibility | Pass | Dedicated v1 canonical path and golden tests; v2 definition envelope is opt-in. |
| External dependency isolation | Pass | BusinessObjects has no NCalc reference; only driver project owns package/types. |
| Least privilege | Pass | Read/write/admin named policies and live deny tests. |
| Testability | Pass | Disposable stores/databases, direct contracts, fake evaluator, live host seams. |
| Dependency direction | Pass | No cycle or inward dependency on Node/EF/NCalc. |

## Residual risks

- NCalc's NU1701 remains until a replacement is selected; isolation reduces replacement scope but does not change package compatibility.
- The development compose profile remains accepted demo-only risk.
- A solution-wide audit reports vulnerable MessagePack 2.5.192 under the separate AppHost project; the four changed runtime/test graphs are clean.
- Every future transaction protocol version must provide a frozen semantic rule strategy before readiness will accept it.

## Accepted development risk

The Docker demo intentionally exposes services, uses development credentials, public reads, development writes, in-memory auth state, and reset. Ledger persistence now uses `Migrate`, but the profile remains interoperability proof rather than a hardened deployment.

## Recommended next sequence

1. Select and qualify a modern expression engine, then replace only the NCalc adapter.
2. Repair the separate AppHost MessagePack advisory in its own dependency-focused change.
3. Produce production deployment/runbook review around backup, migration rehearsal, and rollback policy.
4. Add a new exact semantic strategy whenever protocol v3 is designed; never reinterpret v1/v2 history.
