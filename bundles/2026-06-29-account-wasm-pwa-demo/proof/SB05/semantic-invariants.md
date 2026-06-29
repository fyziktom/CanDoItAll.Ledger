# SB05 Semantic Invariants

## Invariants

- Reusable account workspace UI is driven through parameters and `IEconomyAccountWorkspaceAdapter`, not direct `EconomyNodeWorkspaceSession` injection.
- SSR behavior remains available through `DevelopmentEconomyWorkspaceAdapter` and `NodeEconomyWorkspaceAdapter`.
- WASM behavior is represented by `WasmAccountWorkspaceAdapter`, which consumes API read/broadcast clients, the unlocked account key source, and SB04's local transaction signer.
- WASM registration does not register or resolve `EconomyNodeWorkspaceSession`.
- The adapter host renders the same core workspace surfaces for SSR and WASM modes: account workspace, UTXO explorer, transaction history, and transaction composer.
- Account setup and backup UI remain owned by SB02 host-neutral callbacks; SB05 does not add a second account lifecycle implementation.
- Shared UI paths do not require raw private key material. The only private-key transition remains inside the unlocked key source used by the local signer.

## Evidence

- `AccountWorkspaceAdapterTests.AdapterHost_RendersSameWorkspaceSurfacesForSsrAndWasmAdapters`
- `AccountWorkspaceAdapterTests.WasmWorkspaceAdapter_LoadsWorkspaceAndBroadcastsSignedPaymentWithoutNodeSession`
- `AccountWorkspaceAdapterTests.WasmWorkspaceRegistration_DoesNotRegisterSsrNodeSession`
- `EconomyAccountHandlerTests.AddBusinessObjectAccountLayer_PreservesCustomAccountHandler`
- `browser-render-result.json`
- `browser-responsive-result.json`
- `dependency-audit.md`
