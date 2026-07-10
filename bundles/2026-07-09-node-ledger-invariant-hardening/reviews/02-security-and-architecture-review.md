# Security and architecture review

Status: Passed for the implemented v1 hardening scope, with explicit rollout conditions and protocol-v2 proposals.

## Review conclusion

No known Critical or High finding from the audited v1 transaction-confirmation path remains open. The refactor is genuine responsibility separation: validation logic moved into cohesive classes, the facade shrank, stores retain their atomicity responsibilities, and no partial class, service locator, cyclic project reference, or persistence dependency was introduced into Ledger/Core.

This conclusion does not claim that the development Docker profile is production secure or that historical payload verification is full semantic replay.

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

## Open Medium roadmap/operational risks

### R-001: Existing database migration

The repository has no EF migration baseline and production disallows `EnsureCreated`. New constraints are model-complete but are not retroactively installed into an existing database. Before production rollout:

1. create and review a provider-specific migration/baseline;
2. audit existing output/UTXO rows for violations before adding checks;
3. apply it in a transactional deployment rehearsal;
4. verify constraint presence and readiness afterward.

### R-002: Semantic history replay

Readiness reports `valid_payload_only`: hashes and signatures are verified, but persisted transactions are not replayed through version-appropriate semantic validators. Add a versioned verifier registry and projection rebuild comparison; never replay v1 history with future rules indiscriminately.

### R-003: Token protocol definition

The current unique token ID is a human string. The requested hash identity and global BO/fungible semantics need a canonical immutable definition containing at least symbol/namespace, issuer authority, initial supply, divisibility, classification, and protocol version. Derive the v2 ID from canonical definition bytes and retain display metadata separately. Migration must keep v1 IDs addressable.

### R-004: JWT authorization scopes

JWT `scope` currently has no endpoint authorization effect. Introduce named read/write/admin policies, translate existing development behavior explicitly, and add deny-by-default endpoint tests before presenting scope as a security boundary.

### R-005: Rule expression dependency

`ncalc` 1.3.8 restores compatibility assets rather than net10.0 assets. Isolate it behind the existing rule-evaluation contract or replace it, then add expression compatibility vectors before removal.

## Accepted development risk

The Docker demo intentionally exposes services, uses development credentials, public reads, development writes, in-memory auth state, reset, and `EnsureCreated`. It is useful proof that the actual apps and node interoperate; it is not a hardened deployment profile.

## Recommended next sequence

1. Production schema migration/baseline and data audit.
2. JWT scope authorization policies.
3. Version-aware semantic replay.
4. Token-definition protocol-v2 ADR and compatibility prototype.
5. `ncalc` isolation/replacement.

Items 3 and 4 must remain versioned to preserve canonical v1 history and AccountApp compatibility.
