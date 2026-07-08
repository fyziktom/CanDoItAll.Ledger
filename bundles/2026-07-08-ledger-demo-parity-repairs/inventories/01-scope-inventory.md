# Scope Inventory

| Surface | Files | Current gap | Planned subbundle |
| --- | --- | --- | --- |
| WASM AccountApp reference | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`; `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs` | Working reference; keep passing | `SB01`, `SB02` |
| Console demo | `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs`; `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs` | No UTXO split/merge helpers or lesson proof | `SB01` |
| SSR demo real-node session | `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` | Routes UTXO modes correctly; needs source proof | `SB02` |
| SSR demo development fallback | `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` | Draft uses token amount `250`, not one-token BO carrier default | `SB02` |
| SSR sample page | `repo://examples/CanDoItAll.Economy.Components.Demo/Components/Pages/AdapterWorkspaceSamples.razor` | Accepts modes but does not prove UTXO parity | `SB02` |
| Tests | `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs` | Needs console/SSR parity additions | `SB01`, `SB02` |
