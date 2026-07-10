# Testability plan

## Failing-first tests

1. Mint with one existing normal-token input and outputs greater than input plus a new authorized token must reject.
2. Mint that duplicates aggregate permission-token outputs must reject.
3. Empty main/token output and zero token output must reject.
4. Decimal aggregation overflow must return `amount_overflow`.
5. BO Update to a new owner and Transfer to the same owner must reject.
6. BO update that swaps carrier token ID while conserving both token totals must reject.
7. Oversized transaction must be rejected before the counting hasher is invoked.
8. Concurrent in-memory double spend confirms exactly one transaction and returns a rejected result for the other.
9. SQLite/relational insert violating token/main separation fails even when bypassing executor.
10. Raw Mint that increases main currency rejects unless it consumes a configured treasury authority output; supported treasury issuance remains accepted.

## Characterization/regression tests

- Existing golden vectors remain unchanged.
- Existing permission/rule/token mint happy paths remain accepted.
- Existing BO create/update/transfer/close/merge builder tests remain accepted.
- Existing PostgreSQL concurrency tests remain accepted.
- Existing production security-profile tests remain accepted.

## Composition proof

- `LedgerExecutor` direct-construction tests still work.
- Node DI resolves executor plus validation pipeline.
- Full solution test project passes.
- Docker node and AccountApp images rebuild from changed sources.

## Browser proof

Large-screen only (target viewport at least 1440x900):

- Alice/Bob AccountApp loads against the rebuilt Docker node.
- existing seeded account can refresh and display UTXOs;
- a normal payment/token/BO flow remains usable;
- a raw invalid mixed output is rejected by the node through the AccountApp origin;
- a raw invalid spent/double-spend or issuance exploit is rejected;
- screenshots and response assertions are saved under `proof/browser/` and `proof/docker/`.

## Second-wave proof contract

1. Migration: invalid legacy asset rows fail adoption and do not write the baseline; valid legacy and empty databases reach the same migration set.
2. Replay: hash-valid but asset-imbalanced history fails semantic verification; unsupported version is not mapped to v1/v2; valid v1/v2 histories pass.
3. Token v2: canonical ID golden vector, tampered ID, mismatched supply/issuer/permission, fungible-with-BO, BO-carrier source-with-BO, and unit-without-BO negatives.
4. JWT: missing scope, unknown scope, read-on-write, and write-on-admin deny; hierarchy and legacy alias positives; API-key/development behavior characterization.
5. NCalc: arithmetic/comparison/coercion/error vectors run directly against the driver; fake evaluator proves BusinessObjects orchestration has no NCalc dependency; DI resolves the driver through the contract.
6. Composition: full solution build/test, migration list/script, package audit, PostgreSQL proof, and Docker readiness with semantic status.
