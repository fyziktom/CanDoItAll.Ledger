# Current State

## Current AccountApp Screen

- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` owns both locked account entry and unlocked workspace rendering.
- When `Session.IsUnlocked` is true, `Home.razor` renders `<header class="account-app__workspace-frame">` with account identity, node/ledger/signing facts, and icon actions before `EconomyAccountWorkspaceAdapterHost`.
- The screenshot's top red strike-through corresponds to `account-app__workspace-frame`; it duplicates information/actions already available inside the shared workspace shell.
- When locked, `Home.razor` renders a separate `account-app__topbar`, status strip, and a four-card grid: create account, unlock account, import encrypted backup, and import pasted key pair.

## Current Session Behavior

- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` detects a stored vault through `IAccountVaultStore.GetAsync(Options.VaultId)`.
- `IAccountVaultStore` has `DeleteAsync`, implemented by `BrowserIndexedDbAccountVaultStore` and browser JS `deleteVaultRecord`.
- No user-facing method currently clears the stored vault, unlocked key, workspace state, form fields, or status message as a reset operation.
- Import supports two AccountApp-native modes: encrypted backup JSON and pasted public/private key material. It does not support VEF/Neblio backup files.

## VEF Reference Observations

- `ImportAccount.razor` uses a three-step flow: import material, password, finish. The AccountApp should copy the interaction shape, not the chain-specific data handling.
- `CreateAccount.razor` makes create explicit and warns that password loss cannot be recovered by a third party.
- `UnlockAccount.razor` focuses the password field and supports enter-to-unlock.
- `MyAccount.razor` offers create/import when no account is stored and gives a stored-account message otherwise.

## Shared Component Inventory

- Components MCP returned `CanDoItAll.Components.BaseLib.Steps` and `StepsItem` for wizard-like flows where order matters.
- `Steps` supports `@bind-Value`, `NavigationPosition`, `NextText`, `PreviousText`, `CanSelect`, `CanChange`, and `StepsItem` children.
- Components MCP also confirmed `Grid`, `Stack`, and `FormSection` for layout and form sections. These should be used before page-local structural wrappers where practical.
