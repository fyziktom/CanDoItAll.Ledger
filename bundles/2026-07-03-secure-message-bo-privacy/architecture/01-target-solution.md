# Target Solution

## Public Secure-Message Envelope

New secure-message transactions retain a minimal public envelope:

```json
{
  "objectType": "secure-message",
  "schemaVersion": "1.0",
  "stateJson": {
    "format": "candoitall-secure-message-v1",
    "schemaVersion": "1.0",
    "createdAtUtc": "2026-07-03T00:00:00Z",
    "senderAddress": "...",
    "receiverAddress": "...",
    "content": {
      "algorithm": "ECDH-P256+A256GCM",
      "senderPublicKeyHash": "...",
      "receiverPublicKeyHash": "...",
      "nonce": "...",
      "cipherText": "...",
      "authenticationTag": "..."
    },
    "attachments": []
  }
}
```

The `attachments` public array remains for v1 compatibility but new messages write it empty. All attachment descriptors, encrypted storage locators, nonces, tags, and hashes move into the encrypted message content.

## Encrypted Message Content

The decrypted content contains:

- subject and body
- attachment id
- original file name, content type, size, and description
- encrypted DataItem locator returned from IPFS upload
- attachment nonce, authentication tag, and ciphertext hash

This lets the message list stay metadata-light and only reveal user content after the participant opens the message.

## Opaque Message Identity

Secure-message object ids use `bo:secure-message:{opaque-id}` where `{opaque-id}` is random/hash material. The ID no longer accepts a display name or subject. The external ref remains `secure-message://{objectId}` after the id becomes safe.

## BO Envelope Compatibility

Readers accept both forms:

- legacy: `"stateJson": "{...json string...}"`
- structured/new display: `"stateJson": { ...json object... }` or `"state": { ...json object... }`

Projection contracts continue to expose `LedgerBusinessObjectHolding.StateJson` as a string. When an object-valued state is read, the reader serializes that object back to compact JSON for existing consumers. Raw tx display expands parseable state strings into objects so inspection is structured.

## Security Properties

- AES-GCM AAD binds attachment encryption to sender address, receiver address, public key hashes, and attachment id.
- Public transaction data still contains sender signature public key and receiver output address because those are ledger-level facts, not message content.
- Attachment ciphertext hash can remain inside encrypted content for integrity but must not be visible before decrypting the message.

## Compatibility Strategy

- New messages use encrypted attachment locators.
- Existing v1 payloads with public attachment locators are accepted by `DecryptAttachmentAsync` as fallback.
- Tests assert both new privacy invariants and legacy parse/decrypt behavior.
