# C# Current-State Inventory

## Source Inspected

- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTransactionDraft.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/TransactionUiComponentTests.cs`

## Responsibility Inventory

| Owner | Size/shape | Responsibilities | Risk |
|---|---|---|---|
| `TransactionComposerShell` | 441-line Razor component | Mode catalog, navigation, nine form renderers, parsing, address dialogs, draft mutation, preview, actions | High responsibility concentration and weak independent test seams |
| `BusinessObjectTransactionDraft` | record + 12-mode enum | Durable request semantics shared with adapters | Low, but transient wizard state must not inflate it |
| `EconomyAccountWorkspaceAdapterHost` | 692-line Razor component | Workspace lifecycle and all dialog wiring | Existing broad host; changes must be parameter-only |
| `EconomyAccountWorkspaceAdapters` | 1621-line service file | WASM/SSR adapter paths and transaction builders | Do not widen unless a real domain gap is found |

## Instantiation And Dependencies

- Razor components are created by Blazor; no explicit constructor dependencies.
- `TransactionComposerShell` injects `DialogService` and `NotificationService`; extracted forms should avoid new service dependencies where parameters/callbacks suffice.
- Current tests instantiate `TransactionComposerShell`, `TransactionSendForm`, and UTXO forms directly with BUnit.

## Existing Tests

- Merge filters to plain UTXOs.
- Split equal-output plan updates the draft.
- Payment amount and memo update the draft.
- Adapter tests cover request assembly for multiple modes.

## Missing Tests

- Flow catalog/disposition coverage for all enum members.
- Independent validation and BO selector tests.
- BO wizard valid/invalid paths.
- Composer submit-readiness gating.
- Full AccountApp browser coverage.

## CodeAnalytics Gap

Snapshot `snap-20260710162123-996d4827-failed` loaded 0 projects because the MCP host resolved an unavailable SDK from another repo's `global.json`. No dependency/finding conclusion is drawn from it.

