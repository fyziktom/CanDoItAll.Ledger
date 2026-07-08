# C# Current State Inventory

| Area | Current Responsibility | Change Pressure |
| --- | --- | --- |
| `CanDoItAll.Economy.BusinessObjects` | Transaction builder and domain requests. | Needs clone request carrier-token support while preserving create/update/transfer contracts. |
| `CanDoItAll.Economy.Components.Models` | UI-facing draft and token option models. | Has enough fields for carrier token selection; needs reusable selection policy. |
| `CanDoItAll.Economy.Components.Services` | Workspace adapters, node session, commerce dispatcher. | Must consistently attach carrier tokens to generated create/clone drafts. |
| `CanDoItAll.Economy.Components.Components.BusinessObjects` | Shared BO wizard and commerce dashboard UI. | Must default creation assets and expose split commerce lists. |
| `examples/CanDoItAll.Economy.AccountApp` | Real account flows for buying and validating BOs. | Payment draft must combine sale price and one-unit carrier token. |
| `tests/CanDoItAll.Ledger.Tests` | Behavioral safety net. | Needs failing-first coverage for carrier-token defaulting and clone/payment/receipt/purchase invariants. |

