# Scope Inventory

## Ready Components And Drivers

| Surface | Current file or component | Readiness | Notes |
| --- | --- | --- | --- |
| Transaction domain | `repo://src/CanDoItAll.Economy.Core/Transactions.cs` | Ready | Immutable records can be reused by WASM and node. |
| Signature DTOs | `repo://src/CanDoItAll.Economy.Core/Signatures.cs` | Partial | Model exists; browser provider is missing. |
| Canonical JSON | `repo://src/CanDoItAll.Economy.Cryptography/DefaultTransactionCanonicalizer.cs` | Ready | Must remain identical for browser signatures. |
| Payload hashing | `repo://src/CanDoItAll.Economy.Cryptography/SignaturePayloadBuilder.cs` | Partial | Needs WASM-safe hash implementation. |
| Account backup service | `repo://src/CanDoItAll.Economy.BusinessObjects/AccountBackup.cs` | Strong base | Existing AES-GCM/PBKDF2 format and integrity checks. |
| Transaction assembly | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` | Strong base | Needs signing split/browser signer boundary. |
| Account facade | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs` | Partial | Useful behavior, but key source must not expose persisted private keys. |
| Node HTTP client | `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs` | Strong base | Browser auth/config profile needed. |
| Node endpoints | `repo://src/CanDoItAll.Economy.Node/EconomyNodeApiEndpoints.cs` | Strong base | CORS/auth/PWA-hosting decisions pending. |
| Account workspace UI | `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` | Reusable after adapter | Parameter/callback driven. |
| Transaction UI | `repo://src/CanDoItAll.Economy.Components/Components/Transactions` | Reusable after adapter | Composer/history can call host-neutral actions. |
| UTXO UI | `repo://src/CanDoItAll.Economy.Components/Components/Account/UtxoExplorer.razor` | Reusable after adapter | Needs API data model wiring. |
| Component demo | `repo://examples/CanDoItAll.Economy.Components.Demo` | Reference only | Interactive server, not WASM/PWA. |

## Missing Implementation

| Missing item | Why it matters | Owner |
| --- | --- | --- |
| Browser crypto driver | Blocks account creation, address derivation, encryption, signing | SB01 |
| IndexedDB encrypted vault | Blocks private key persistence | SB01 |
| Browser-compatible backup vectors | Blocks backup/import correctness | SB02 |
| Account setup UI with real services | Existing backup panel is visual only | SB02 |
| Browser-safe API registration/auth/CORS | Blocks API-only AccountApp | SB03 |
| Unsigned assembly/signature application split | Prevents proving browser-side signing | SB04 |
| WASM session adapter for shared components | Existing sessions sign/load SSR-side | SB05 |
| New Blazor WASM PWA project | No app exists | SB06 |
| Real browser proof suite | Required for WebCrypto/IndexedDB/PWA | SB07 |

## Detailed XLSX Inventory

- The workbook `bundle://plan/account-wasm-pwa-planning.xlsx` contains sortable checklists for component readiness, crypto gaps, API contracts, UI reuse, VEFramework flow mapping, subbundle plan, validation, and risks.
