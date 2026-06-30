# Normalized Requirements

## Functional Requirements

- FR-01: Add background refresh behavior to `EconomyAccountWorkspaceAdapterHost` or a similarly scoped AccountApp UI lifetime owner.
- FR-02: Each refresh tick must call a cheap account transaction probe, limited to the newest 10 transaction summaries with data excluded.
- FR-03: If the newest transaction fingerprint is unchanged, the loop must skip full workspace reload and avoid churn.
- FR-04: If the fingerprint changes, the loop must reload the workspace through the adapter so balances, UTXOs, BOs, token options, and transaction lists update together.
- FR-05: Provide an adapter/cache abstraction that allows WASM localStorage-backed caching and SSR in-memory caching without hard-coding browser storage into shared non-browser code.
- FR-06: Cache transaction details by tx id and use the cache before hitting node API or read client detail endpoints.
- FR-07: Full-shell transaction success/rejection and refresh messages must use `NotificationService` popups only, not persistent inline alert components.
- FR-08: The large-screen unlocked AccountApp surface must use the full viewport width.

## Non-Functional Requirements

- NFR-01: Do not poll too aggressively; the default interval must be conservative and configurable in component parameters or options.
- NFR-02: The refresh loop must be cancellation-safe and stop when the component is disposed.
- NFR-03: Cache keys must include ledger/scenario/address or tx id context so Alice/Bob/lab app sessions do not collide.
- NFR-04: Tests must cover unchanged fingerprint skip and changed fingerprint reload behavior.
- NFR-05: Browser proof must show full-width large-screen layout and absence of inline result/refresh alerts after sending a transaction.
