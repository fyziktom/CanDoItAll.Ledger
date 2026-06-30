# Target Solution

## Refresh And Cache

- Add a small refresh probe model to the account workspace adapter contract.
- Implement `GetRefreshSnapshotAsync` in adapters using newest 10 transaction summaries without transaction data.
- Add `IAccountWorkspaceCache` as a shared abstraction with:
  - transaction detail get/set by contextual cache key;
  - optional object/detail get/set path for future BO details;
  - no-op/memory implementation for SSR;
  - localStorage-backed implementation in the WASM AccountApp.
- Keep the UI timer in `EconomyAccountWorkspaceAdapterHost` so it starts only when the component is alive and is cancelled in `DisposeAsync`.
- Compare fingerprints before full reload. Full reload remains the only path that mutates the current workspace state.

## UI Polish

- Remove inline `LastResult` and `RefreshNotification` rendering from `VenftSsrEconomyApp`.
- Keep `NotificationService` calls in the host for send/result and refresh messages.
- Make the AccountApp shell full width while preserving setup flow readability.
