# Current State

## Production Findings

| Area | Current state | Impact |
| --- | --- | --- |
| Default tags | `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor` and `repo://src/CanDoItAll.Economy.Components/Components/VenftBusinessObjectApp.razor` still seed `#VEFramework #Neblio #NET #Blazor #OpenSource`. | `Neblio` must be removed from production defaults. Historical bundle evidence can keep old text. |
| Transaction signatures | `SignatureEnvelope` includes optional `PublicKeyMaterial`; ledger validation rejects missing/mismatched public key material for inputs. `CryptoDriverAccountInputSigner` includes public key material in browser-signed transactions. | The signer public key is already present in valid signed transactions; regression tests must protect this. |
| Transaction read model | `LedgerTransactionInputSummary` and node transaction details preserve `SignatureBundle`, including public key material. `EconomyNodeLedgerClient.QueryTransactionsAsync` can query address transactions. | Receiver public-key discovery can use production transaction signatures. |
| Current ECDH | `EcdhP256MessageCipher` implements .NET ECDH P-256 plus AES-GCM with AAD. | Good compatibility target, but not usable in WASM because it uses .NET `ECDiffieHellman`. |
| Browser crypto | `candoitall-economy-crypto.js` supports WebCrypto random, SHA-256, PBKDF2, AES-GCM, ECDSA key generation/sign/verify. | ECDH deriveBits/deriveKey is missing and must be added through WebCrypto. |
| Account message security | `AccountMessageSecurity` supports sign/verify and .NET encrypt/decrypt. | Needs driver-based ECDH/encrypted-payload services so browser and .NET share one protocol. |
| WASM adapter | `WasmAccountWorkspaceAdapter.SignMessageAsync` works through WebCrypto. `EncryptMessageAsync` and `DecryptMessageAsync` return explicit failure messages. | The main runtime gap is browser public-key discovery plus WebCrypto ECDH. |
| UI | `MessageSecurityPanel` is a technical sign/encrypt/verify workbench. Generic BO creation has a `message` type but not a mailbox. | Need new messaging UX that hides BO mechanics from normal user flows. |
| Attachments | BO wizard uploads files to IPFS before staging a BO and stores CIDs in `DataItems`. | Secure messaging must encrypt bytes before upload and store encrypted attachment metadata. |
| Bookmarks | Address directory/bookmarks can provide nicknames and tags. | Inbox list should use bookmark names without decrypting message content. |

## VEFramework Reference

`C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\MessageNFT.cs` models messages as NFTs/BOs, determines partner address from transactions, encrypts fields with a shared key, and decrypts lazily. The concept is useful; the implementation and Neblio dependency are not imported.

## Initial Architecture Decision

Implement a new protocol surface around existing P-256 account keys:

1. Query receiver transactions to find a signature whose public key material derives to the receiver address.
2. Sender derives ECDH shared key from sender private key and receiver public key using WebCrypto in WASM.
3. Encrypt message payload and every attachment with AES-256-GCM, unique random nonce per payload, and authenticated associated data binding sender, receiver, key hashes, object id/content id, and protocol version.
4. Store only encrypted body/metadata in ledger BO state and encrypted attachment bytes in IPFS.
5. Receiver derives the same key from own private key and sender public key from the signed transaction/envelope.
6. UI lists encrypted messages using unencrypted route metadata only; content decrypts on open; attachments decrypt on request.
