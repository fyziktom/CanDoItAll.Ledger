# C# Current State Inventory

## Source Files Inspected

- `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs`
- `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs`
- `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs`
- `repo://examples/CanDoItAll.Economy.Components.Demo/Program.cs`
- `repo://examples/CanDoItAll.Economy.Components.Demo/Components/Pages/Home.razor`
- `repo://examples/CanDoItAll.Economy.Components.Demo/Components/Pages/AdapterWorkspaceSamples.razor`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`

## Large Classes And Partial Classes

- `BusinessObjectTransactionBuilder` is responsibility-heavy, but this bundle will not expand it beyond existing behavior.
- `EconomyAccountWorkspaceAdapters.cs` contains multiple adapter types in one file; this bundle will not add a new adapter type or partial class.
- No new partial classes are planned.

## Constructor Dependency Counts

- `WasmAccountWorkspaceAdapter` has many dependencies but already owns browser-signing orchestration.
- `EconomyNodeWorkspaceSession` has options and cast-registry dependencies and loads short-lived account drivers.
- `EconomyDevelopmentWorkspaceSession` has in-memory store/client dependencies for SSR fallback.

## Direct Instantiation Points

- Console demo constructs `BusinessObjectTransactionBuilder` and `EconomyClient` in `DemoAccountDriver.CreateAsync`.
- SSR real-node session constructs a short-lived `NodeAccountDriver` and `BusinessObjectAccount` in `LoadDriverAsync`.
- SSR development session loads a seeded `BusinessObjectAccount` in `EnsureSeededAsync`.

## Provider Tool Driver Responsibilities

- No provider/tool/plugin boundary changes are required.
- Transaction assembly remains owned by shared business-object and ledger layers.

## Current Tests

- WASM adapter UTXO split/merge proof exists in `AccountWorkspaceAdapterTests`.
- BO transaction shape proof exists in `BusinessObjectAdvancedTransactionTests`.
- Console demo-specific parity proof is missing.
- SSR development draft parity proof is missing.

## Missing Tests

- Console helper tests for `MergeUtxosAsync` and `SplitUtxosAsync`.
- Console lesson/source assertion proving UTXO split/merge is separate from BO split payment/merge.
- SSR development workspace test proving one-token BO carrier draft.
- SSR development/session test proving UTXO split/merge creates the same vin/vout shapes as WASM.

## Risk Notes

- CodeAnalytics MCP evidence is unavailable due SDK resolution in the MCP host; snapshot id and diagnostic are recorded in `inputs/01-source-artifacts.md`.
- Direct source assertions and CLI build/test proof must carry the architecture evidence.
