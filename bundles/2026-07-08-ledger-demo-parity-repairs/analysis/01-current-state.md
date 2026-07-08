# Current State

## Source Evidence

- WASM AccountApp registers `AddEconomyWasmAccountWorkspaceAdapter` and uses the shared `WasmAccountWorkspaceAdapter` transaction path in `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` already implements `BuildUnsignedUtxoMergeAsync`, `BuildUnsignedUtxoSplitAsync`, `BuildUnsignedTokenMintAsync`, `BuildUnsignedCreateAsync`, and BO update/merge/split paths.
- `BuildUnsignedUtxoMergeAsync` selects explicit plain main UTXOs and emits exactly one plain output to self or requested receiver.
- `BuildUnsignedUtxoSplitAsync` selects explicit plain main UTXOs and emits requested plain outputs plus positive self-change only.
- `BuildUnsignedTokenMintAsync` rejects permission-holder main minting and emits token outputs with `Amount.Zero`.
- BO create/update/merge/split outputs use `Amount.Zero` on the BO output and exactly one carrier token through `EnsureBusinessObjectCarrierToken`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs` exposes shared `MergeUtxosAsync` and `SplitUtxosAsync` wrappers.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` routes WASM `UtxoMerge` and `UtxoSplit` drafts through unsigned transaction build/sign/submit.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` routes SSR real-node `UtxoMerge` and `UtxoSplit` drafts through `BusinessObjectAccount`.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` routes SSR development fallback `UtxoMerge` and `UtxoSplit` drafts through `BusinessObjectAccount`, but its seeded BO create draft uses `TokenAmount: Amount.FromInt64(250)` instead of the AccountApp one-token carrier behavior.
- `repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs` has helpers for main payment, token mint/transfer, BO merge, and BO split payment, but no helpers for plain UTXO merge/split.
- `repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs` lesson 7 still teaches set price, BO split payment, clone, BO merge, and close, without explicit plain UTXO split/merge proof.
- `repo://examples/CanDoItAll.Economy.Components.Demo/Components/Pages/AdapterWorkspaceSamples.razor` sample adapters accept any draft mode without asserting transaction shape, so they do not demonstrate UTXO split/merge semantics.

## Existing Tests

- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs` includes `WasmWorkspaceAdapter_SubmitsExplicitUtxoSplitAndMergeWithoutNodeSession`, proving the WASM path.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectAdvancedTransactionTests.cs` includes domain proof for BO merge/split payment behavior, but does not cover console demo parity.
- Existing tests do not prove the console demo helpers or SSR development draft token amount match AccountApp parity.

## CodeAnalytics MCP Evidence

- Attempted scoped snapshot with projects `CanDoItAll.Economy.AccountApp`, `CanDoItAll.Economy.ConsoleDemo`, `CanDoItAll.Economy.Components.Demo`, `CanDoItAll.Economy.BusinessObjects`, `CanDoItAll.Economy.Components`, `CanDoItAll.Economy.Ledger`, `CanDoItAll.Economy.Core`, and `CanDoItAll.Ledger.Tests`.
- Result: snapshot `snap-20260708181406-97cb2b5b-failed`, `projectCount=0`, blocking diagnostic `A compatible .NET SDK was not found`, because the MCP host resolved `C:\repositories\CanDoItAll\global.json`.
- Direct `dotnet --info` from this workspace shows SDK `10.0.300` and workspace `global.json` requests `10.0.200` with `rollForward: latestFeature`, so normal CLI validation from `repo://` workspace remains available.
