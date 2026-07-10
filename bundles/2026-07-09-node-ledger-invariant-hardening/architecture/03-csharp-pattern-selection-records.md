# Pattern selection records

## PSR-001: Ordered validation pipeline

- Force: multiple validation stages have strict ordering, async policy work, and independent negative-test needs.
- Selected: small facade/pipeline coordinating cohesive validator classes.
- Rejected: more private methods in `LedgerExecutor`; partial class; generic middleware framework; service locator.
- Testability: each validator and the ordered pipeline can be instantiated directly.
- Anti-fake proof: signature/reference logic leaves `LedgerExecutor`; direct tests do not construct executor/store.

## PSR-002: Preflight validator

- Force: resource and null/shape checks must happen before hashing untrusted data.
- Selected: simple extracted class, not a pattern framework.
- Rejected: catching out-of-memory/serialization exceptions; relying only on HTTP body limits.
- Testability: counting hasher proves invalid input never reaches canonicalization.

## PSR-003: Confirmation callback/transaction boundary

- Force: store-specific atomic state acquisition and commit with domain validation in the Ledger layer.
- Selected: preserve `ILedgerConfirmationStore` callback (transaction-script/strategy boundary) and implement it in custom in-memory storage.
- Rejected: make Ledger reference EF; hold a monitor lock across `await`; duplicate validation in stores.
- Testability: parallel submissions against one in-memory store.

## PSR-004: Compatibility facade

- Force: many tests and consumers construct `LedgerExecutor` directly.
- Selected: keep the public executor API and use a default pipeline composition path while DI can supply the real pipeline.
- Rejected: immediate breaking constructor rewrite across all consumers.
- Removal path: document explicit required policy composition for a future major version.

## PSR-005: Separate main-currency issuance policy

- Force: ordinary Mint must conserve existing main currency, raw account Mint must not inflate it, and the supported treasury command must still issue it.
- Selected: a Ledger-layer policy that evaluates the positive main delta against the configured treasury and a consumed plain treasury authority output.
- Rejected: blanket Mint bypass; blanket main conservation that breaks treasury; recognizing only the Node endpoint; treating a plain account signature as issuance authority.
- Testability: direct unauthorized/authorized policy tests, fail-closed executor composition, and deployed treasury endpoint proof.
