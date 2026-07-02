# Source Artifacts

| Artifact | Path | Role |
| --- | --- | --- |
| Raw user request | `bundle://inputs/00-original-request.md` | Literal scope, constraints, and validation requirements. |
| UI proposal 1 | `bundle://inputs/ui-proposals/proposal-01-three-pane-inbox.png` | Planning aid for mailbox/thread list layout. |
| UI proposal 2 | `bundle://inputs/ui-proposals/proposal-02-compose-secure-message.png` | Planning aid for compose/security readiness layout. |
| UI proposal 3 | `bundle://inputs/ui-proposals/proposal-03-message-detail-attachments.png` | Planning aid for opened message, security side panel, and decrypt-on-request attachments. |
| VEFramework reference | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\MessageNFT.cs` | Behavior reference for encrypted message NFT semantics. Do not import dependencies. |
| Existing WebCrypto module | `repo://src/CanDoItAll.Economy.Cryptography.Browser/wwwroot/candoitall-economy-crypto.js` | Current browser random/SHA/PBKDF2/AES-GCM/ECDSA bridge; lacks ECDH. |
| Existing browser crypto driver | `repo://src/CanDoItAll.Economy.Cryptography.Browser/BrowserEconomyCryptoDriver.cs` | C# JS interop driver for WebCrypto. |
| Existing .NET ECDH helper | `repo://src/CanDoItAll.Economy.Cryptography/EcdhP256MessageCipher.cs` | Existing .NET-only shared-secret/encrypted-message implementation. |
| Existing message security service | `repo://src/CanDoItAll.Economy.BusinessObjects/AccountMessageSecurity.cs` | Current sign/verify/encrypt/decrypt APIs; .NET ECDH only. |
| WASM workspace adapter | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` | Current browser adapter; signing works and encryption/decryption intentionally fail. |
| Message/security UI | `repo://src/CanDoItAll.Economy.Components/Components/Account/MessageSecurityPanel.razor` | Current security-tool UI, not the target mailbox UX. |
| BO wizard/uploads | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | Current upload path stores IPFS attachments before BO transaction. |
| Data-item upload service | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs` | Current IPFS upload driver/service. |
| Transaction contracts | `repo://src/CanDoItAll.Economy.Core/Transactions.cs`; `repo://src/CanDoItAll.Economy.Core/Signatures.cs`; `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerContracts.cs` | Signed transaction and summary shape. |
| Node read client/contracts | `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs`; `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeContracts.cs` | API access for transaction query/detail and signature bundles. |

## UI Proposal Review

- Reuse: three-pane large-screen mailbox layout, compact security status row, contact/bookmark nickname display, compose readiness panel, decrypt-on-open message detail, decrypt-on-request attachments.
- Do not copy literally: purple-dominant buttons, decorative card heaviness, any text that suggests decrypted previews in unopened rows.
