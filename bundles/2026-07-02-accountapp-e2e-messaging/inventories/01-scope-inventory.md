# Scope Inventory

| Surface | Files | Planned change |
| --- | --- | --- |
| Default tags | `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor`; `repo://src/CanDoItAll.Economy.Components/Components/VenftBusinessObjectApp.razor` | Remove `Neblio` from default tag strings. |
| Core crypto abstractions | `repo://src/CanDoItAll.Economy.Cryptography/CryptoDrivers.cs`; `repo://src/CanDoItAll.Economy.Cryptography/EcdhP256MessageCipher.cs` | Add driver-backed ECDH/secure payload helpers or equivalent reusable protocol services. |
| Browser crypto bridge | `repo://src/CanDoItAll.Economy.Cryptography.Browser/BrowserEconomyCryptoDriver.cs`; `repo://src/CanDoItAll.Economy.Cryptography.Browser/wwwroot/candoitall-economy-crypto.js` | Add WebCrypto ECDH derive support and browser proof harness. |
| Message/security models | `repo://src/CanDoItAll.Economy.Core/Messages.cs`; `repo://src/CanDoItAll.Economy.Components/Models/MessageSecurityModels.cs` | Introduce secure message/attachment envelopes and UI/service request models. |
| Key discovery | `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerContracts.cs`; `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs`; new service if needed | Query production transactions and extract trusted receiver public keys from signatures. |
| Message domain | `repo://src/CanDoItAll.Economy.BusinessObjects/AccountMessageSecurity.cs`; new message services | Encrypt/decrypt secure message body and attachments with explicit lazy APIs. |
| IPFS upload | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs`; AccountApp upload path | Encrypt attachment bytes before upload for messaging. |
| WASM adapter | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` | Replace encryption failure stubs with browser-safe secure messaging operations. |
| Messaging UI | `repo://src/CanDoItAll.Economy.Components/Components/Account/*`; `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css` | Add mailbox, composer, message detail, and attachment decrypt-on-request controls. |
| Tests | `repo://tests/CanDoItAll.Ledger.Tests` | Add protocol, discovery, attachment, component, and adapter tests. |
| Docker proof | `repo://docker-compose.yml`; `repo://examples/CanDoItAll.Economy.AccountApp` | Rebuild/recreate and validate Alice/Bob flows in containers. |
