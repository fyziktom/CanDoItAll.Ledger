# C# current-state inventory

## Primary sources inspected

- Core contracts: `Core/Transactions.cs`, `StorageModels.cs`, `Abstractions.cs`, `Validation.cs`, `LedgerValueValidation.cs`.
- Cryptography: canonicalizer, transaction hasher, signature payload builder, signature provider registry/providers.
- Ledger: `LedgerExecutor`, shape/conservation/BO/token validators, chain verifier.
- Persistence: custom in-memory store, EF store, records/configurations, projection verifier/rebuilder/history reader.
- Node: host options, registration, API endpoints, application services, node service, bootstrapper, Docker composition.
- BO layer: 1,448-line `BusinessObjectTransactionBuilder` and its create/transfer/update/close/merge helpers.
- Tests: critical invariants, token policy, BO transitions, persistence, PostgreSQL concurrency, golden vectors, AccountApp contracts.

## Responsibility concentration

| Type | Approx. lines | Current responsibilities | Risk |
| --- | ---: | --- | --- |
| `BusinessObjectTransactionBuilder` | 1,448 | all transaction selection/assembly flows, token/permission/BO helpers, signing handoff | High growth/test coupling; not fully refactored by this bundle. |
| `EfLedgerStore` | 1,269 | reads, transaction boundary, locks, append, conflicts, token registry, BO/address projections, mapping | High but persistence-cohesive; extraction proposal after invariants stabilize. |
| `EconomyNodeService` | 798 | reset/bootstrap, treasury commands, signing, reads, UTXO selection, DTO helpers | High; endpoint-facing facades already reduce some coupling. |
| `LedgerExecutor` | 352 | normalization, state reads, all validation stages, signature validation, confirmation construction | Critical refactor target. |
| `TransactionShapeValidator` | 350 | identifiers/resources/data/signatures/indexes/output/BO shape | Critical split target: preflight vs normalized semantics. |
| `TokenCreationPolicy` | 582 | main issuance, token type issuance, permission lineage/delegation, rule issuance | Cohesive policy family but needs issuance result/aggregate tests. |

## Current test seams

- Validators can be instantiated directly, except signature/reference validation is private inside `LedgerExecutor`.
- Store concurrency can be tested through executor/store integration.
- Node API security can be tested through host/TestServer patterns already present.
- PostgreSQL tests are opt-in based on local database availability.

## Missing seams

- No top-level validation pipeline with an explicit stage order.
- No direct isolated test target for input-reference and signature validation.
- No pure issuance-conservation result describing permitted deltas.
- No semantic history replay service.

## Partial-class policy

No new partial class will be introduced. Existing `EconomyNodeHost` partial files are composition/endpoint organization and are not extended for ledger-domain behavior.

