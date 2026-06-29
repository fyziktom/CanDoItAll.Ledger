# SB06 Semantic Invariants

## Result

- Status: `Passed`
- Date: `2026-06-29`

## Invariants

| Invariant | Result | Evidence |
| --- | --- | --- |
| AccountApp is a Blazor WebAssembly PWA, not an SSR route | Passed | `examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj` uses `Microsoft.NET.Sdk.BlazorWebAssembly` and service-worker assets. |
| Node communication is API read/list/detail/broadcast only | Passed | Browser proof captured only account `GET` endpoints and one signed transaction `POST /api/v1/ledger/transactions`. |
| Signature and transaction assembly happen client-side | Passed | `WasmAccountWorkspaceAdapter` assembles unsigned drafts and uses the unlocked browser vault signer before broadcast; proof POST body contains signature/public key material. |
| Private key storage uses browser encrypted vault | Passed | AccountApp registers `AddCanDoItAllBrowserCrypto`; browser proof storage audit found only encrypted private key records in IndexedDB. |
| Backup import re-encrypts into the active AccountApp vault | Passed | Browser proof imported an exported backup, unlocked it, re-encrypted into the configured vault id, and loaded the workspace. |
| Shared account UI is reused through host-neutral adapter | Passed | AccountApp renders `EconomyAccountWorkspaceAdapterHost` with `Session.WorkspaceAdapter`; SB05 SSR/WASM adapter proof remains valid. |
