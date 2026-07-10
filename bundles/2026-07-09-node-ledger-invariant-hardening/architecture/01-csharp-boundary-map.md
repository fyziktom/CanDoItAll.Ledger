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
