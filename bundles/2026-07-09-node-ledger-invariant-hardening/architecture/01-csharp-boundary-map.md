# Target boundary map

| Responsibility | Current owner | Target owner | Test seam |
| --- | --- | --- | --- |
| Cheap untrusted-envelope limits | `TransactionShapeValidator` after hashing | `TransactionPreflightValidator` in Ledger | Direct unit tests; source assertion that executor calls it before hasher. |
| Normalized transaction shape | `TransactionShapeValidator` | Retained focused `TransactionShapeValidator` | Direct unit tests. |
| Input-reference validation | private `LedgerExecutor` method | `InputReferenceValidator` | Direct dictionary-based tests. |
| Input-signature validation | private `LedgerExecutor` methods | `InputSignatureValidator` | Direct fake-provider tests. |
| Ordered validation orchestration | `LedgerExecutor` | `LedgerTransactionValidationPipeline` | Direct pipeline tests; executor composition smoke. |
| Issuance-aware conservation | blanket mint bypass | `AssetConservationValidator` mint branch plus token policy | Exploit regression tests. |
| Main-currency issuance authorization | treasury command only; absent for raw signed Mint | `MainCurrencyIssuancePolicy` in Ledger, configured by Node | Unauthorized/default and authorized-treasury tests; deployed treasury command. |
| BO owner/carrier semantics | incomplete BO validator | `BusinessObjectTransitionValidator` | Direct transition matrix. |
| In-memory atomic confirmation | executor legacy read/append path | `InMemoryLedgerStore : ILedgerConfirmationStore` | Parallel execution tests. |
| Relational output constraints | absent | EF model configuration | SQLite schema/constraint tests. |
| Request stream limit | Content-Length endpoint filter | Kestrel/server options plus filter | Host configuration test. |

`LedgerExecutor` remains a thin facade/orchestrator: preflight, hash normalization, acquire confirmation context, delegate validation pipeline, construct confirmed envelope, return mapped store conflict.

No new project is required. The validators belong in the existing Ledger domain project; storage-specific atomicity remains in each store; host request limits remain in Node composition.

All target boundaries in this map were implemented. Policy omission is fail closed; the development workspace must select disabled token/main issuance policies explicitly.

## Second-wave target boundaries

| Responsibility | Target owner | Contract/test seam | Old responsibility removed |
| --- | --- | --- | --- |
| Schema migration abstraction | `Persistence.EFCore.IEconomySchemaMigrator` | fake/default provider migrator | duplicated direct `MigrateAsync` selection in Node callers |
| PostgreSQL audit/adoption/migrations | `Persistence.PostgreSql` | disposable database tests and audit report | no SQL or migration-history manipulation in Node |
| Versioned semantic replay | Ledger rule-set registry/verifier | fake rule set and replay store | no “latest rules for all versions” logic in readiness |
| Replay state | custom InMemory store composed by Node | non-durable store instance per readiness run | no durable writes during verification |
| Token definition contract | Core | immutable records | no persistence/host types |
| Token definition canonicalization | Cryptography | golden canonical bytes/ID | no display metadata in identity hash |
| Token definition/classification policy | Ledger | direct policy matrix | no builder-only enforcement |
| Token definition representation | EF/InMemory stores | store round-trip | no domain rules in persistence |
| JWT scope semantics | Node authorization handler/policies | direct handler and host requests | endpoint filter no longer treats authentication as authorization |
| Rule evaluator contract | BusinessObjects | fake evaluator | static NCalc construction removed |
| NCalc implementation | `Rules.NCalc` driver project | compatibility vectors | NCalc package/type references removed from BusinessObjects |

Temporary compatibility bridges: v1 human IDs and canonical bytes remain indefinitely addressable; legacy `node.api` is an explicit JWT full-access alias; direct `BusinessObjectAccount` construction without an evaluator remains usable for non-rule flows but fails clearly if rule execution is requested. These bridges are behavior contracts, not partial-class scaffolds.
