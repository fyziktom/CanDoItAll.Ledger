# Ledger Runtime Architecture

CanDoItAll.Ledger is the transaction and node runtime repository.

## Runtime Layers

1. Core contracts define ledger transaction, account, chain, storage, and validation shapes.
2. Cryptography provides hashing, key, and signature drivers used by validation and node flows.
3. Ledger execution validates transactions, applies state transitions, verifies chain continuity, and protects asset conservation.
4. Storage adapters persist ledger state through in-memory, EF Core, and PostgreSQL-backed implementations.
5. Business-object and SDK layers provide higher-level transaction assembly and client access.
6. Node hosts APIs, authentication, admin services, and ledger UI endpoints.
7. CLI, demos, AppHost, and seeding tools exercise ledger flows without importing Economy simulator ownership.

## Dependency Direction

Ledger runtime projects may depend inward on core contracts and storage abstractions. Host, UI, CLI, and demo projects sit at the edge. Repository-local adapters must not reach into the separated Economy simulator lane.

## Evidence Boundary

Ledger proof is build, test, audit, node smoke, transaction validation, persistence, SDK, CLI, and ledger UI proof. Browser or visual proof from the separated Economy simulator is not Ledger correctness evidence.
