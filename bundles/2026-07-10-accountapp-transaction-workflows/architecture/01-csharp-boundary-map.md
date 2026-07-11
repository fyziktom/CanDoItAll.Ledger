# C# Boundary Map

| Responsibility | Current owner | Target owner | Contract/implementation |
|---|---|---|---|
| Mode metadata/disposition | static list in composer | `TransactionFlowCatalog` in Components Models/Services | Pure implementation over existing enum |
| Shell/navigation/actions | composer monolith | thin `TransactionComposerShell` + `TransactionFlowShell` | Razor UI |
| Validation state | implicit/absent | `TransactionFlowValidation` record + per-form pure rules | Model + UI presentation |
| BO source selection | raw text fields | `BusinessObjectSourcePicker` | Reusable Razor component |
| Compact BO presentation | full gallery card elsewhere | `BusinessObjectSelectionCard` reusing display helpers | Reusable Razor component |
| Mode behavior | inline switch fragments | one cohesive form per transaction responsibility | Razor components |
| Domain transaction build | adapter switch | unchanged adapter/domain accounts | Existing implementation |

## Composition Root

`EconomyAccountWorkspaceAdapterHost` supplies `BusinessObjects`, balance, tokens, UTXOs, bookmarks, and callbacks. It does not learn flow rules.

## Temporary Bridges

- Existing draft fields remain the compatibility bridge to adapter submission.
- Inline helper render fragments may remain only until their owning mode form is extracted in SB03/SB04; final closure requires their removal from the shell.

