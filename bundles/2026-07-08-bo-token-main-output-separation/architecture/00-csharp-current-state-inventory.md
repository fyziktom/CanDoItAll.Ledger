# C# Current State Inventory

## Source Files Inspected

- `repo://src/CanDoItAll.Economy.Ledger/TransactionShapeValidator.cs`
- `repo://src/CanDoItAll.Economy.Ledger/BusinessObjectTransitionValidator.cs`
- `repo://src/CanDoItAll.Economy.Ledger/TokenCreationPolicy.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/BusinessObjectCarrierTokenPolicy.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/BusinessObjectCommerceDispatcher.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/BuyBo.razor`

## Current Behavior

- Normal BO drafts can still fall back to `MainAmount = 1` and no carrier token in profile, secure-message, and creation UI paths.
- Token transfers can mix `MainAmount` and `Token` on the same output.
- The ledger shape validator currently rejects negative amounts and bad identifiers, but does not reject mixed token/main outputs or token-less normal BO outputs.
- Permission/rule authority outputs also use `BusinessObjectRef`, but they are governed by `TransactionDataItemType.Permission` and `TransactionDataItemType.Rule` plus `TokenCreationPolicy`; they are not the normal BO carrier-token model.

## CodeAnalytics Evidence

- Snapshot attempt: `snap-20260708133758-d5bc8855-failed`
- Result: blocked by SDK resolution.
- Diagnostic: CodeAnalytics resolved `C:\repositories\CanDoItAll\global.json` and required SDK `10.0.200`; the tool process could not load the MSBuild workspace.
- Fallback: direct project/source inspection plus full build/test proof.

## Risks

- A broad rule that every `BusinessObjectRef` must have token amount `1` would break permission/rule authority outputs.
- A narrow UI-only fix would leave the node accepting invalid token/main mixes.
- Existing old local ledger rows can remain historical; the new node rule protects newly submitted transactions.

