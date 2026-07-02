# Normalized Requirements

| ID | Requirement | Acceptance |
| --- | --- | --- |
| RQ01 | Remove `Neblio` from production default tags. | Source search across `src`, `examples`, `tools`, and `tests` shows no production default `#Neblio`; intentional historical/test text is documented if present. |
| RQ02 | Preserve signed transaction public key material. | Tests prove browser-signed transactions include signer public key material and ledger validation rejects missing or mismatched key material. |
| RQ03 | Discover receiver public key from production API transactions. | A service queries receiver transactions and returns only public key material from a signature whose public key derives to the receiver address. |
| RQ04 | Reject unsafe public-key discovery. | Negative tests prove no-key, mismatched-key, empty-key, and metadata-only-key cases do not enable encryption. |
| RQ05 | Implement WASM-compatible ECDH via WebCrypto. | Browser proof shows WebCrypto derives the same shared key as .NET for Alice/Bob P-256 keys and rejects tampered ciphertext. |
| RQ06 | Encrypt message payload with ECDH-derived AES-GCM. | Secure message BO state stores encrypted body/subject data only; receiver decrypts with own private key plus sender public key. |
| RQ07 | Encrypt IPFS attachment bytes the same way as message data. | Uploaded attachment content is encrypted before IPFS add; gateway/API content does not contain plaintext; receiver can decrypt on request. |
| RQ08 | Keep message list lazy. | Inbox/thread list renders sender/receiver nickname/address, direction, time, locked state, and attachment metadata without decrypting bodies or attachments. |
| RQ09 | Decrypt message only on open. | Opening a message invokes decrypt once and renders plaintext only in the selected message detail. |
| RQ10 | Decrypt attachment only on request. | Attachment chips remain locked until user requests decrypt/open; decrypted bytes are not prefetched for list rendering. |
| RQ11 | Present messaging as email/chat UX. | Large-screen AccountApp has a Messages view with folders/list/detail/compose flows, using bookmark nicknames when present and hiding BO terms from primary UX. |
| RQ12 | Preserve BO semantics behind the UI. | Messages are still BO-backed and details can expose transaction/BO/security metadata in a secondary details area. |
| RQ13 | Validate Docker WASM Alice to Bob. | In rebuilt Docker apps, Alice sends encrypted message to Bob with and without attachment; Bob receives and decrypts message and attachment on request. |
| RQ14 | Validate Docker WASM Bob to Alice. | In rebuilt Docker apps, Bob sends encrypted message to Alice with and without attachment; Alice receives and decrypts message and attachment on request. |
| RQ15 | Avoid shortcut/hack implementation. | Source assertions and anti-stub audit prove production services, not test fixtures, implement protocol, discovery, storage, and UI flows. |

## Literal Scope Preservation

- `must be fully working and secure p2p e2e encrypted communication` maps to RQ02-RQ07 and RQ13-RQ15.
- `Same loaded content to ipfs must be encrypted same way` maps to RQ07 and final Docker attachment proof.
- `It cannot decrypt all messages` maps to RQ08 and RQ09.
- `attachments are decrypted also only on request` maps to RQ10.
