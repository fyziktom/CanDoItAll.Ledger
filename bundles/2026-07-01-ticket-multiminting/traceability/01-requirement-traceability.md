# Requirement Traceability

| Requirement | Source | Implementation Target | Proof Target |
| --- | --- | --- | --- |
| R1 | User asked to study VEFramework minting | `analysis/01-current-state.md` | Bundle validator |
| R2 | Integrate multimint into account UI | `BusinessObjectCreationWizard.razor` | Component test + screenshot |
| R3 | Own/subaccount/different/list recipients | Wizard recipient resolver | Component test + real proof target |
| R4 | Unique tickets | Batch draft builder | Unit/component assertions |
| R5 | Existing signing/API path | `VenftSsrEconomyApp.razor`, `EconomyAccountWorkspaceAdapterHost.razor` | Adapter host test + browser proof |
| R6 | SSR/WASM alignment | `EconomyNodeWorkspaceSession.cs`, development session as needed | Source/test proof |
| R7 | Small validation batch | Playwright proof script | Proof manifest |
| R8 | Keep event prefill | Existing ticket event picker code | Existing tests remain green |
