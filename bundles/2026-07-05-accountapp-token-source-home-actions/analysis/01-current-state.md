# Current State

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` renders a three-column work grid: Balances, Token Sources, and Recent Transactions.
- The Balances card currently lists main balance, token types, and balance-only tokens. It does not list token-source outpoints.
- `TokenSourceOutputs` in `EconomyAccountWorkspace.razor` currently filters only `output.Token is not null`, so unique one-amount token UTXOs and BO-backed token outputs can appear in the Token Sources card.
- The domain rule already exists in `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs`: `GetTokenSourceOutputsAsync` requires `BusinessObject is null`, matching token id, and `output.Token.Amount.Value > 1m`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` uses the same source concept for BO creation: token source outputs must be non-BO outputs with more token amount than the requested token amount.
- `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css` hides `.rz-button-text` inside `.cda-vef-account-actions`, so the in-home action group has accessible labels but no visible short labels.
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` top-frame actions are in `.account-app__frame-actions`; this is a separate toolbar that must remain icon-only.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs` already renders `EconomyAccountWorkspace` and can host focused assertions for home surface behavior.

## Component Inventory Fallback

- The CanDoItAll components MCP search failed during preparation with transport closed.
- Existing markup confirms this page uses shared `Grid`, `PageHeaderActionButton`, `Button`, `CopyableMonoValue`, `QrCodeButton`, and panel classes from the component stack.
- Execution should prefer existing shared components and targeted page CSS over new structural wrappers.
