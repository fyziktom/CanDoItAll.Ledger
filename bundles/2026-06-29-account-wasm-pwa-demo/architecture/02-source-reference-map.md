# Source Reference Map

## Current Ledger Solution

- `repo://CanDoItAll.Ledger.slnx`: solution inventory; all current projects target `net10.0`.
- `repo://src/CanDoItAll.Economy.Core/Transactions.cs`: immutable transaction records used by node, SDK, account builders, and future browser assembly.
- `repo://src/CanDoItAll.Economy.Core/Signatures.cs`: signature envelope/provider abstractions already exist.
- `repo://src/CanDoItAll.Economy.Core/ValueObjects.cs`: address hashing/checksum currently uses `SHA256.HashData`; this is a WASM-sensitive dependency.
- `repo://src/CanDoItAll.Economy.Cryptography`: canonicalization, hashing, ECDSA P-256, ECDH/AES-GCM, and signature registry live here.
- `repo://src/CanDoItAll.Economy.BusinessObjects/AccountKeys.cs`: account key source and input signer currently expose private key material to .NET signing.
- `repo://src/CanDoItAll.Economy.BusinessObjects/AccountBackup.cs`: encrypted backup format and tests are a strong base for browser backup/import compatibility.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`: transaction assembly coverage is broad but tied to `IAccountInputSigner`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs`: account facade submits transactions through a gateway.
- `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs`: HTTP client already covers spendable outputs, account transactions, transaction details, query APIs, and signed transaction submit.
- `repo://src/CanDoItAll.Economy.Node/EconomyNodeApiEndpoints.cs`: node endpoints expose read APIs and `POST /ledger/transactions`.
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`: account workspace is callback-driven and likely reusable after SSR session decoupling.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs`: SSR node session currently loads keys from disk and signs server-side; not valid for AccountApp but useful as behavior map.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs`: in-memory demo session; not valid for AccountApp but useful for expected workspace shape.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectAccountBackupTests.cs`: existing backup tests to extend for browser-compatible encrypted backup vectors.

## VEFramework Local Context

- `bundle://evidence/veframework-reference-map.md`: durable summary of local VEFramework references and copy/no-copy decisions.

## Component MCP Findings

- BaseLib `PageScaffold`, `Grid`, `Stack`, `FormRow`, `FormStack`, `FormSection`, and storage cards are available and should be preferred for new or adapted AccountApp UI surfaces.
- Existing ledger components already use BaseLib patterns; the plan should refactor service coupling before building any new layout.
