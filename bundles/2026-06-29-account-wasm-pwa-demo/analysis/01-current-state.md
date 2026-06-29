# Current State

## Solution Shape

- The solution is a `net10.0` ledger workspace with core domain records, cryptography, business-object account flows, SDK/node API surfaces, Razor components, examples, and xUnit/bUnit tests.
- There is no existing Blazor WASM PWA account app in `examples/`.
- There is no existing browser vault, IndexedDB key store, or WebCrypto-backed signer in this repository.
- The existing worktree had a pre-existing `global.json` modification before bundle preparation; this bundle does not change it.

## Ready Building Blocks

- Core transaction records are ready in `repo://src/CanDoItAll.Economy.Core/Transactions.cs`.
- Signature abstractions are partially ready in `repo://src/CanDoItAll.Economy.Core/Signatures.cs`.
- Canonicalization and hash-payload builders are ready in `repo://src/CanDoItAll.Economy.Cryptography/DefaultTransactionCanonicalizer.cs`, `repo://src/CanDoItAll.Economy.Cryptography/TransactionHasher.cs`, and `repo://src/CanDoItAll.Economy.Cryptography/SignaturePayloadBuilder.cs`.
- Account backup/export/import service exists in `repo://src/CanDoItAll.Economy.BusinessObjects/AccountBackup.cs`, including AES-GCM/PBKDF2 metadata and address/public-key integrity checks.
- Transaction assembly coverage is broad in `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`, including create, transfer, payment, token, permission, rule, update, close, clone, merge, split, and price flows.
- Node HTTP client already implements `ILedgerGateway` and `ILedgerReadClient` in `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs`.
- Node API endpoints already expose signed transaction submit and account read endpoints in `repo://src/CanDoItAll.Economy.Node/EconomyNodeApiEndpoints.cs`.
- Existing ledger UI components are mostly callback/model driven: `EconomyAccountWorkspace`, transaction composer, transaction history, UTXO explorer, and dialogs are good candidates for reuse.
- Existing tests cover backup import/export integrity and smoke-test signing/encryption; these can be extended into browser-compatible vectors and account-app flows.

## Missing Or Unsafe For WASM

- `repo://src/CanDoItAll.Economy.Core/ValueObjects.cs` uses `SHA256.HashData` for address and checksum creation. The browser app needs a hash abstraction or browser-safe path.
- `repo://src/CanDoItAll.Economy.Cryptography/EcdsaP256SignatureProvider.cs` uses `ECDsa.SignData` and `VerifyData`; this cannot be assumed to work in Blazor WASM.
- `repo://src/CanDoItAll.Economy.Cryptography/EcdsaP256KeyMaterial.cs` uses .NET ECDsa/ECDH import/export APIs; the browser app needs WebCrypto-compatible key generation/import/export decisions.
- `repo://src/CanDoItAll.Economy.Cryptography/EcdhP256MessageCipher.cs` uses .NET ECDH, AES-GCM, random, and SHA; browser equivalent behavior needs dedicated proof before account messaging or encryption can be reused.
- `repo://src/CanDoItAll.Economy.BusinessObjects/AccountKeys.cs` signs by obtaining raw private key material from `IAccountKeySource`; the WASM account path needs a browser signer that does not expose persisted key bytes to node or SSR services.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` currently takes `IAccountKeySource` and signs during build; the AccountApp needs a clear assembly/sign/broadcast split.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` loads keys from local disk and signs server-side; this must not be reused in WASM.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs` is useful for shape and demos but depends on in-memory stores and development signing, not the node API-only browser path.
- `repo://src/CanDoItAll.Economy.Components/Components/Account/AccountBackupPanel.razor` is visual only and is not a functional backup/import implementation.
- Node hosting currently has API rate limiting and endpoint authorization filters, but no confirmed CORS/PWA hosting configuration for a separately served browser app.
- The SDK supports API-key/bearer headers; browser use must not require embedding node operator secrets in PWA assets.

## VEFramework Lessons

- VEFramework proves the general pattern: Blazor WASM can route crypto through browser APIs and persist encrypted account data in IndexedDB.
- VEFramework should be copied at the user-story level only: create account, unlock account, import backup/private key, backup export, load transactions/UTXOs after account load.
- VEFramework should not be copied at implementation level because it uses broad `AppData` services, static JS runtime assignment, weak WASM IV generation in a placeholder path, password hashing as a direct key source, and storage migration logic mixed into UI/session state.

## Component And Driver Readiness Summary

| Area | Ready | Missing | Planned owner |
| --- | --- | --- | --- |
| Core transaction records | `LedgerTransaction`, inputs, outputs, data records | Browser-facing assembly service boundary | SB04 |
| Signature model | `SignatureEnvelope`, registry abstractions | WebCrypto provider and browser-key handles | SB01 |
| Hashing/address | Canonical .NET SHA256 path | WASM-safe SHA256/address generation path | SB01 |
| Key storage | Backup service format | IndexedDB encrypted vault and unlock lifecycle | SB01 |
| Backup/import | Server-side service and tests | Browser flow and compatibility vectors | SB02 |
| Node API | Read/list/detail/broadcast endpoints exist | CORS/auth/browser-safe config and contract tests | SB03 |
| SDK | `EconomyNodeLedgerClient` exists | WASM DI/config profile without secrets | SB03 |
| Transaction builder | Broad business-object coverage | Unsigned assembly plus browser signing split | SB04 |
| Razor components | Workspace/history/UTXO/composer components | SSR session decoupling and WASM adapters | SB05 |
| WASM PWA app | None | New AccountApp project, manifest, service worker, route shell | SB06 |
| Browser proof | Existing unit tests only | Playwright/WebCrypto/IndexedDB/no-key-leak proof | SB07 |
