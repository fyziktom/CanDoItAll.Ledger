# Target Solution

## Protocol

Create a versioned secure-message envelope, tentatively `candoitall-secure-message-v1`.

Plain routing metadata allowed outside encryption:

- object type `message` or `secure-message`
- sender address
- receiver address
- created timestamp
- attachment count and encrypted attachment descriptors without plaintext file content
- optional local-only UI state such as read/decrypted cache in browser storage

Encrypted payload:

- subject
- body
- message-specific fields that would reveal content
- attachment content encryption records

Key agreement:

- account keys remain P-256.
- .NET path uses existing `ECDiffieHellman` derivation semantics.
- Browser path imports SPKI public and PKCS#8 private material for `ECDH` and derives 256-bit shared secret bits.
- The symmetric AES key is `SHA-256(sharedSecret || "cdia.economy.ecdh.p256.v1")`, matching .NET `DeriveKeyFromHash(..., secretAppend: label)`.

Encryption:

- AES-256-GCM.
- 12-byte random nonce per message payload and per attachment.
- 128-bit authentication tag.
- AAD binds protocol version, sender address, receiver address, sender public key hash, receiver public key hash, BO object id or attachment id, and content purpose (`message-body`, `attachment-bytes`, `attachment-metadata`).
- Tamper, wrong receiver, wrong sender key, or wrong AAD must fail.

Public-key discovery:

- Query transactions for the receiver address.
- Inspect input signature bundles.
- Accept only signature public key material where `Address.FromPublicKey(publicKeyMaterial) == receiverAddress`.
- Cache discovered keys locally with source tx id and timestamp if useful, but do not trust user-entered key text for encryption.
- If no valid public key is found, secure send is blocked and UI explains that the receiver must have at least one signed transaction.

Message storage:

- Message BO state stores a secure envelope, not plaintext message content.
- Message send transaction is signed by sender, making sender public key available to receiver directly in transaction input signature bundle.
- Attachments are encrypted before IPFS upload. DataItems store encrypted CID, encrypted size/hash metadata, original filename/content type only if encrypted or acceptable as user-visible metadata; otherwise store display-safe labels.

Decryption:

- Inbox list uses BO route metadata and address-book nicknames only.
- Selected message loads its transaction detail if needed to find sender public key, then decrypts message payload in browser.
- Attachment decrypt/open reads encrypted bytes from IPFS only when requested, derives the same key/AAD, decrypts bytes, then creates a browser object URL/download/open action.

## UI

Large-screen AccountApp gets a primary Messages surface:

- left folder/filter rail: Inbox, Sent, Attachments, Favorites/Bookmarks
- center message list: partner nickname/address, direction, locked/decrypted state, timestamp, tags, attachment count
- right detail: selected message decrypt-on-open area, security status, attachment chips with explicit decrypt/open controls
- compose dialog/panel: recipient from address directory/bookmarks, public-key readiness, subject/body, attachments, encrypt-and-send action
- security metadata is behind tabs/details, not primary BO terminology

## Boundaries

- Do not remove generic BO gallery/details behavior.
- Do not make the node perform private-key encryption for WASM users.
- Do not store or transmit private key material.
- Do not use generated UI images as acceptance proof.
