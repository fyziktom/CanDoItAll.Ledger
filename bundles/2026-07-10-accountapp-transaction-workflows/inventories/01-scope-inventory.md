# Scope Inventory

## Production Files

| Surface | Current owner | Planned role |
|---|---|---|
| Composer orchestration | `TransactionComposerShell.razor` | Thin operation selection, active flow, shared validation/review, submit |
| Dialog data | `TransactionComposerDialogContent.razor` | Pass workspace BOs/balance/token/UTXO data |
| Host wiring | `EconomyAccountWorkspaceAdapterHost.razor` | Supply workspace data and keep submission callbacks |
| Draft contract | `BusinessObjectTransactionDraft.cs` | Preserve transaction semantics; add only durable request fields if needed |
| Payment | `TransactionSendForm.razor` | Balance-aware reusable simple flow |
| UTXO forms | `TransactionUtxoMergeForm.razor`, `TransactionUtxoSplitForm.razor` | Adopt shared validation/review contracts |
| BO raw cases | inline in composer | Extract guided create/select/edit flows |
| BO selector | absent | New reusable compact selector over `IBusinessObjectView` |
| Validation | absent | New immutable validation result/rule helper usable by every flow |
| Flow metadata | static tab list | New catalog mapping enum to family, label, icon, description, and disposition |
| CSS | `economy-components.css` | Add transaction-workbench styles without touching unrelated dirty BO detail styles |

## Existing Dirty Worktree Exclusions

- `BusinessObjectLedgerDetailsDialogContent.razor`
- `economy-components.css` (already modified; transaction styles must be appended carefully without overwriting existing changes)
- `BusinessObjectDetailRendererLayoutTests.cs`

The CSS file overlaps scope. Before editing, preserve and review its current diff; transaction changes must not alter existing BO-detail edits.

