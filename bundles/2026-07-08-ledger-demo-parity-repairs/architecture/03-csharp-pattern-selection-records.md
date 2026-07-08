# C# Pattern Selection Records

## PSR-001 Console Helper Facade

- Problem force: the console demo needs to expose existing account transaction behavior without duplicating assembly logic.
- Selected pattern: thin facade/helper methods on `DemoAccountDriver`.
- Rejected alternatives: direct raw `LedgerTransaction` assembly in `LearningScenarios`; new demo-only transaction builder.
- New types/projects: no new production project; optional test helper types only.
- How it improves testability: tests can call driver/account helpers or source assertions and verify shared APIs are used.
- Proof required: source assertion that helpers call `BusinessObjectAccount.MergeUtxosAsync` and `SplitUtxosAsync`, plus tests for vin/vout shape.

## PSR-002 SSR Session Parity Through Existing Adapter

- Problem force: SSR demo needs the same behavior while supporting real-node and seeded in-memory fallback modes.
- Selected pattern: preserve existing session adapter facade and correct defaults/tests.
- Rejected alternatives: new SSR-specific transaction assembler; duplicating WASM adapter internals.
- New types/projects: no new project boundary.
- How it improves testability: `EconomyDevelopmentWorkspaceSession` and adapter tests can exercise the behavior without a live browser or node.
- Proof required: tests for development session draft and UTXO split/merge details through existing session APIs.
