# Structured Input

## Objectives

- Remove `Neblio` from default BO tags.
- Implement secure p2p E2E encrypted messaging in AccountApp WASM.
- Use production transaction signatures as public-key discovery, not a fake contact key table.
- Use browser WebCrypto for WASM ECDH and AES-GCM.
- Encrypt both message content and IPFS attachment bytes before storage.
- Present messaging as mailbox/chat UX while preserving BO-backed ledger semantics in lower-level details.
- Validate in Docker with Bob and Alice apps both directions, with and without attachments.

## Hard Constraints

- No plaintext message body, subject, or attachment bytes may be written to ledger/IPFS for secure messages.
- Public-key discovery must verify public key material derives to the target address.
- Receiver must decrypt with own private key plus sender public key from the signed transaction/direct envelope.
- The message list must not bulk decrypt message bodies or attachments.
- Attachments must decrypt only when requested by the user.
- The implementation must not be a fixture-specific or demo-only path.

## Validation Expectations

- Unit tests for .NET protocol and negative cases.
- Browser/WebCrypto test for ECDH compatibility with .NET derivation and AES-GCM tamper rejection.
- Component tests for lazy decryption and mailbox layout state.
- Docker Playwright proof for Alice to Bob and Bob to Alice, including encrypted attachments.
- Network/storage inspection showing IPFS stored attachment content is encrypted bytes and does not contain plaintext.
