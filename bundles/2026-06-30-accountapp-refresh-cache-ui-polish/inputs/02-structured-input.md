# Structured Input

## Raw Notes

| Note ID | Exact note | Normalized requirement | Owning subbundle |
|---|---|---|---|
| RN-01 | "account must have some refreshing loop... if account pull from node api list of newest txs (for example just 10 newest for account) and it see they are same, it does not loading anything. if there are some new, it will reload account utxos and new txs." | AccountApp must run a moderate background refresh loop after workspace load. Each tick must perform a cheap newest-transaction fingerprint probe and skip full workspace reload when unchanged. When changed, reload account workspace including UTXOs, transactions, BOs, balances, and token state. | SB01 |
| RN-02 | "It is good to have some caching mechanism for BO and tx details... localstorage... interface and cache provider based on if it is wasm or ssr" | Introduce cache abstractions with WASM localStorage provider and SSR/in-memory provider registration. Cache immutable transaction details and BO/detail-style reads behind adapter-level interfaces. | SB01 |
| RN-03 | "notification in UI after send of tx showed also as component... it should show up as notificationservice popup only." | Full shell must not render transaction result or auto-refresh status as persistent inline page content. Accepted/rejected tx and refresh status should use `NotificationService` only. | SB02 |
| RN-04 | "in large screen app is not using whole width. it must use always full width. we need it now for large screen desktop only." | The unlocked AccountApp full shell must consume the full desktop viewport width instead of being capped by the AccountApp wrapper. Narrow setup screens may remain readable, but the active app surface must be full width. | SB02 |

## Assumptions

- "Not so fast" means a polling interval in the tens of seconds, not sub-second or every-few-seconds polling.
- Transaction summaries are immutable after confirmation in this ledger model, so transaction details are safe to cache by tx id.
- BO detail caching may start with transaction-backed BO/detail reads that the current UI uses; if a dedicated BO detail endpoint is added later, it should use the same cache provider.
