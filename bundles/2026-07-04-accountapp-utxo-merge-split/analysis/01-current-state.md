# Current State

## Repo Observations

- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor` owns the current transaction tabs. Its `Merge` tab maps to `BusinessObjectTransactionMode.MergeObjects`, and `Split pay` maps to `BusinessObjectTransactionMode.SplitBusinessObjectPayment`.
- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTransactionDraft.cs` has only BO-centric merge/split fields plus main payment and token transfer fields. It has no selected UTXO list or split output list.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` supports plain `BuildUnsignedPaymentAsync`, but that method auto-selects main UTXOs by amount. It cannot merge an explicit UTXO set or split explicit UTXOs into chosen outputs.
- `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerContracts.cs` exposes `LedgerSpendableOutput` with `OutPoint`, `MainAmount`, `Token`, `BusinessObject`, and `CreatedAt`, which is sufficient for an eligible-UTXO selector.
- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectWorkspaceModel.cs` exposes `SpendableOutputsOrEmpty`, and both SSR and WASM adapters load spendable outputs.
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor` already uses scroll-list panels for inputs and outputs. It needs proof with many rows, and possibly tightening if large input/output sets break the dialog.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectAdvancedTransactionTests.cs` already covers advanced BO, plain payment, and token transaction builder behavior. New explicit UTXO merge/split tests should live near these.
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs` already verifies SSR/WASM adapter and component host behavior. New adapter tests should verify the new mode uses explicit UTXO shape.

## Current Bug Shape

- The user-facing labels "Merge" and "Split pay" are semantically misleading in the send dialog because they refer to BO workflows, not UTXO consolidation/preparation.
- A fake fix that only renames tabs or changes copy would not satisfy the raw request because submitted transaction vin/vout shapes would still be wrong.

## Component Guidance Result

- The `candoitall_components` MCP tools were queried for `Grid`, `Stack`, `FormRow`, and `SectionCard`, but every query failed with `Transport closed`.
- Execution should therefore follow existing repo component usage: `Stack`, `Grid`, `FormField`, `SecondaryTabs`, `Button`, `StatusBadge`, and existing dialog primitives.
