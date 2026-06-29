# SB05 Dependency Audit

## Result

- Status: `Passed`
- Date: `2026-06-29`

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Shared adapter host has no SSR session injection | Passed | `rg "@inject .*Economy(Node|Development)WorkspaceSession|EconomyNodeWorkspaceSession|EconomyDevelopmentWorkspaceSession" src/CanDoItAll.Economy.Components/Components .../AdapterWorkspace*.razor` returned no matches. |
| Direct `EconomyNodeWorkspaceSession` use is isolated outside the WASM adapter path | Passed | `rg "EconomyNodeWorkspaceSession" .../EconomyAccountWorkspaceAdapters.cs` found only `NodeEconomyWorkspaceAdapter`, the explicit SSR adapter. |
| WASM registration does not expose SSR node session | Passed | `WasmWorkspaceRegistration_DoesNotRegisterSsrNodeSession` asserts `GetService<EconomyNodeWorkspaceSession>()` is null and `IEconomyAccountWorkspaceAdapter` resolves to `WasmAccountWorkspaceAdapter`. |
| Shared account UI does not access private key material | Passed with expected SB02 labels | `rg "PrivateKey|privateKey|GetPrivateKeyMaterial"` over account components and adapters found only visible backup/import labels/callback names in SB02 account backup components. |
| Demo SSR route compiles through adapter registration | Passed | `dotnet build examples/CanDoItAll.Economy.Components.Demo/CanDoItAll.Economy.Components.Demo.csproj --no-restore` passed before browser proof. |

## Notes

- `NodeEconomyWorkspaceAdapter` intentionally remains in the shared services project to preserve SSR node/demo behavior. The WASM registration path does not register or consume it.
- SB06 must wire the real PWA route to `WasmAccountWorkspaceAdapter` after account unlock; SB05 proves the component and adapter seams needed for that route.
