# SB05 Proof Manifest - Shared UI SSR WASM Flex

## Status

- Closure result: `Passed`
- Date: `2026-06-29`
- Scope closed: host-neutral account workspace adapter contract, SSR adapter wrappers, WASM adapter over API read/broadcast plus local signing, adapter-driven shared workspace host, demo SSR/sample routes, focused component/service tests, browser render proof, dependency audit, and workbook/status updates.
- Downstream gate: SB06 may build the AccountApp PWA using the SB05 adapter surface and SB04 signing boundary.

## Implementation Summary

- Added `IEconomyAccountWorkspaceAdapter` and host-specific implementations in `EconomyAccountWorkspaceAdapters.cs`.
- Added `WasmAccountWorkspaceAdapter` that loads balances, UTXOs, business objects, transactions, and transaction details from `ILedgerReadClient`, assembles unsigned drafts with `BusinessObjectTransactionBuilder`, signs locally with `IAccountTransactionSigner`, and broadcasts signed transactions through `ILedgerGateway`.
- Added `EconomyAccountWorkspaceAdapterHost.razor` to render `EconomyAccountWorkspace`, `UtxoExplorer`, `TransactionHistoryBrowser`, and `TransactionComposerShell` from any adapter.
- Added SSR adapter registration and demo routes:
  - `/adapter-workspace` uses `DevelopmentEconomyWorkspaceAdapter`.
  - `/adapter-workspace-samples` renders SSR and WASM sample adapters side by side for browser render comparison.
- Closed account-layer DI gaps by registering default `ITimeProvider`, crypto driver, in-memory vault store, and `AccountVaultService` in `AddBusinessObjectAccountLayer`; browser/host registrations can still override these with their own services.
- Added tests proving shared render surfaces, WASM sign/broadcast behavior without node session, and account-layer setup/vault DI resolution.

## Production Behavior Artifact Matrix

| Artifact | Behavior Proven | Evidence |
| --- | --- | --- |
| `IEconomyAccountWorkspaceAdapter` | Host-neutral contract for workspace load, transaction details, and transaction submission | `transcripts/02-account-workspace-adapter-tests-green.txt` |
| `WasmAccountWorkspaceAdapter` | Uses API/gateway clients plus local signing to broadcast a signed payment | `WasmWorkspaceAdapter_LoadsWorkspaceAndBroadcastsSignedPaymentWithoutNodeSession` |
| `EconomyAccountWorkspaceAdapterHost` | Same account, UTXO, history, and composer surfaces render for SSR and WASM modes | `AdapterHost_RendersSameWorkspaceSurfacesForSsrAndWasmAdapters`, browser screenshots |
| `DevelopmentEconomyWorkspaceAdapter` and `NodeEconomyWorkspaceAdapter` | Preserve SSR session-backed behavior behind the same adapter contract | `dotnet build` and `/adapter-workspace` browser proof |
| `AddEconomyWasmAccountWorkspaceAdapter` | Registers the WASM adapter without registering `EconomyNodeWorkspaceSession` | `WasmWorkspaceRegistration_DoesNotRegisterSsrNodeSession` |
| `AddBusinessObjectAccountLayer` defaults | Account setup and backup services resolve in SSR/demo hosts | `EconomyAccountHandlerTests.AddBusinessObjectAccountLayer_PreservesCustomAccountHandler` |

## Browser Proof

| Route | Viewport | Evidence | Result |
| --- | --- | --- | --- |
| `/adapter-workspace` | Desktop 1440x1200 | `adapter-workspace-ssr.png`, `browser-render-result.json` | Passed |
| `/adapter-workspace-samples` | Desktop 1440x1200 | `adapter-workspace-ssr-wasm-samples.png`, `browser-render-result.json` | Passed |
| `/adapter-workspace-samples` | Tablet 900x900 | `adapter-workspace-ssr-wasm-samples-tablet.png`, `browser-responsive-result.json` | Passed |
| `/adapter-workspace-samples` | Mobile 390x844 | `adapter-workspace-ssr-wasm-samples-mobile.png`, `browser-responsive-result.json` | Passed |

## Command Transcripts

| Transcript | Result |
| --- | --- |
| `transcripts/01-failing-account-workspace-adapter-tests.txt` | Expected red compile: adapter contract did not exist. |
| `transcripts/02-account-workspace-adapter-tests-green.txt` | SB05 focused adapter tests: 3 passed. |
| `transcripts/03-account-layer-and-adapter-regression-tests-green.txt` | SB05 adapter plus account-layer DI regression slice: 6 passed. |
| `transcripts/04-demo-build-and-browser-render.txt` | Demo app build/run and Playwright desktop/tablet/mobile browser proof passed. |
| `transcripts/05-sb05-dependency-audit.txt` | Session/private-key dependency audit passed. |
| `transcripts/06-git-diff-check-after-sb05.txt` | `git diff --check` passed, with existing LF-to-CRLF warnings only. |
| `transcripts/07-workbook-regeneration-after-sb05.txt` | Workbook status updated; SB05 closed and SB06 marked ready. |
| `transcripts/08-full-ledger-tests-after-sb05.txt` | Full ledger test project passed: 188 tests. |

## Source Hashes

- See `changed-file-hashes.txt`.

## Downstream Notes

- SB06 should use `WasmAccountWorkspaceAdapter.UseUnlockedAccountAsync` after the account setup flow unlocks a vault key.
- SB06 must still prove the real PWA route, manifest/service worker behavior, live account setup/backup placement, and final API read/broadcast network proof.
