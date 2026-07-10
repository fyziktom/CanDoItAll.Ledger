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

## PSR-006: Provider schema migrator adapter

- Force: PostgreSQL must audit/adopt legacy schemas before applying migrations, while Node should not own provider SQL.
- Selected: narrow `IEconomySchemaMigrator` adapter with default EF and PostgreSQL implementations.
- Rejected: direct SQL in Node; blind migration-history insert; a service locator.
- Testability: disposable database audit/adoption tests and DI implementation assertion.

## PSR-007: Versioned semantic rule-set strategy

- Force: historical protocol versions must retain their own semantic rules and future rules must not reinterpret v1.
- Selected: `ILedgerSemanticRuleSet` strategies in an exact-version registry, orchestrated by a replay verifier.
- Rejected: latest-validator replay; switch logic in readiness; reflection discovery.
- Testability: fake rule-set selection, v1/v2 positive history, unsupported version, and hash-valid semantic negative.

## PSR-008: JWT scope authorization policy

- Force: authenticated JWTs need reusable hierarchical read/write/admin permission semantics.
- Selected: ASP.NET named policies backed by a custom scope requirement/handler.
- Rejected: substring checks in filters; roles for API scopes; endpoint-local claim parsing.
- Testability: principal-level policy tests and real HTTP endpoint denial.

## PSR-009: NCalc adapter

- Force: an external, compatibility-targeted package must remain replaceable without leaking into BusinessObjects.
- Selected: driver project implementing the repository-owned evaluator contract; instance-based rule executor.
- Rejected: immediate removal, global mutable factory, service locator, or a wrapper that remains inside BusinessObjects.
- Testability: driver vectors plus a fake contract implementation driving orchestration.
