# Requirement Traceability

| Requirement | Raw notes | Owning subbundle | Source references | Planned proof |
| --- | --- | --- | --- | --- |
| `R001` | `N001` | `SB01`, `SB02` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` | Existing WASM adapter tests plus source assertions |
| `R002` | `N002`, `N004` | `SB01`, `SB02` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` | Console and SSR UTXO merge tests |
| `R003` | `N002`, `N004` | `SB01`, `SB02` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` | Console and SSR UTXO split tests |
| `R004` | `N002`, `N004` | `SB01`, `SB02` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` | Negative rejection/source assertion proof |
| `R005` | `N003` | `SB01`, `SB02` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`; `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` | BO output and draft token amount tests |
| `R006` | `N003` | `SB01`, `SB02` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` | Token mint rejection/source proof |
| `R007` | `N004`, `N005` | `SB01` | `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs` | Console source assertions and tests |
| `R008` | `N005` | `SB02` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` | SSR development workspace test |
| `R009` | `N005` | `SB02` | `repo://examples/CanDoItAll.Economy.Components.Demo/Components/Pages/AdapterWorkspaceSamples.razor` | Sample adapter/component test |
| `R010` | `N006` | `SB01`, `SB02` | `repo://examples/CanDoItAll.Economy.ConsoleDemo/CanDoItAll.Economy.ConsoleDemo.csproj`; `repo://examples/CanDoItAll.Economy.Components.Demo/CanDoItAll.Economy.Components.Demo.csproj` | Build transcripts |
| `R011` | `N006` | `SB01`, `SB02` | `bundle://proof/SB01/manifest.md`; `bundle://proof/SB02/manifest.md` | Artifact-backed proof |
| `R012` | `N006` | `SB02` | `repo://bundles/2026-07-08-ledger-demo-parity-repairs/reviews/01-execution-report.md` | Final raw-note closure table |
