# AccountApp E2E Messaging

This bundle coordinates implementation of large-screen AccountApp messaging as a real end-to-end encrypted peer-to-peer communication feature backed by ledger BusinessObjects.

## Profile

- `initiative`

## Mission

Replace the current security-tool messaging surface with a mailbox-style user experience and a secure production protocol: sender encrypts message body and attachments in the browser with a shared ECDH-derived key, receiver discovers sender public key from the signed transaction, and plaintext is decrypted only when a user opens a message or explicitly decrypts an attachment.

## Outcome Contract

- Requested outcome: Bob and Alice can exchange ledger-backed encrypted messages in Docker WASM AccountApps, both directions, with and without encrypted IPFS attachments.
- Hard constraints:
  - Remove `Neblio` from default BO tags; the current app is not a Neblio app.
  - Do not implement messaging as a cosmetic wrapper around plain BOs.
  - Signed transactions must include signer public key material and validation must continue to reject mismatched keys.
  - Sender must discover receiver public key only from production transaction signatures whose public key derives to the receiver address.
  - WASM encryption must use browser WebCrypto, not .NET-only ECDH.
  - Message body and attachment bytes must be encrypted before ledger/IPFS persistence.
  - Inbox/thread list must not decrypt all messages; it may show direction, time, nickname/address, locked state, tags, and attachment metadata only.
  - Decrypt message content only when a message is opened; decrypt attachments only on explicit request.
  - UI target is large screen only for this bundle.
- Evidence required before closure:
  - Prepared and completed bundle validator transcripts.
  - Unit/component tests for protocol, discovery, encryption, negative cases, and lazy decryption semantics.
  - Browser/WebCrypto proof for ECDH/AES-GCM compatibility and no private-key leakage.
  - Playwright proof in Docker WASM AccountApps: Alice to Bob and Bob to Alice, with and without attachments.
  - Screenshots of mailbox list, composer, opened decrypted message, encrypted attachment decrypt-on-request, and failure state when receiver public key is unavailable.
  - Docker rebuild/recreate/smoke transcripts.
- Known blockers or explicit scope exceptions:
  - Small and medium screen tuning is outside this bundle unless large-screen changes accidentally break layout.
  - Message notifications beyond visible mailbox refresh are out of scope.
  - Legacy unencrypted message BOs may be displayed as legacy/plain objects but do not count as secure messaging proof.

## Bundle Layout

- `inputs/` raw request, source artifacts, and UI proposal images
- `analysis/` current state, assumptions, risks, and reopen triggers
- `requirements/` normalized requirements and acceptance criteria
- `architecture/` protocol, service, storage, and UI architecture
- `plan/` dependency map and phase gates
- `traceability/` raw note to requirement/subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review, execution report, gate results, and raw-note closure

## Recommended Execution Order

1. `subbundles/01-01-sb01-tags-and-current-state`
2. `subbundles/02-02-sb02-crypto-key-discovery-protocol`
3. `subbundles/03-03-sb03-encrypted-message-and-attachment-domain`
4. `subbundles/04-04-sb04-messaging-ui`
5. `subbundles/05-05-sb05-docker-browser-e2e-validation`

## Dependency And Validation Map

- The protocol subbundle is a critical foundation for all later work.
- The encrypted message/attachment domain subbundle is a critical foundation for the UI and Docker E2E proof.
- The UI subbundle cannot close from screenshots alone; it must prove lazy decryption behavior in browser.
- The final Docker/browser subbundle must prove both directions and attachment encryption against rebuilt containers.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`
