# SB03 - Encrypted Message And Attachment Domain

## Status

- Completed

## Objective

Implement BO-backed secure messages and encrypted attachment storage so message body, subject, and IPFS attachment bytes are encrypted before persistence and decrypted only through explicit message/attachment actions.

## Covered Inputs

- N004
- N005
- N006
- N008
- N009
- N010
- N013

## Prerequisites

- SB02 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Core/Messages.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/AccountMessageSecurity.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItems.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectPayload.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://tests/CanDoItAll.Ledger.Tests`

## Deliverables

- Secure message envelope and typed view/model for encrypted message BOs.
- Encrypted attachment envelope and upload/read/decrypt service.
- WASM adapter methods to send secure messages and decrypt selected messages/attachments.
- Lazy decrypt tracking that prevents list views from decrypting message bodies or attachment bytes.

## Dependency Impact

- Critical foundation for SB04 and SB05.

## Validation Depth

- Unit tests, component/service tests, storage plaintext-negative tests, anti-stub audit, semantic adequacy proof.

## Implementation Steps

1. Define secure message and attachment envelopes.
2. Implement create/send secure message service using SB02 key discovery and encryption.
3. Implement encrypted IPFS attachment upload: encrypt bytes first, upload encrypted stream, store encrypted CID metadata.
4. Implement selected-message decrypt and selected-attachment decrypt APIs.
5. Add tests that assert BO state and IPFS bytes do not contain plaintext.
6. Add tests that count/prove list rendering does not decrypt all messages.
7. Record proof manifest and semantic invariants.

## Scope Exceptions

- Final visual UX belongs to SB04.
- Docker cross-app proof belongs to SB05.

## Do Not Do

- Do not reuse the generic BO wizard upload for secure attachments unless encryption happens before upload.
- Do not store plaintext subject/body in the secure message BO state.
- Do not cache decrypted attachments globally.

## Acceptance Checklist

- [ ] Secure message body/subject encrypted before ledger persistence.
- [ ] Attachment bytes encrypted before IPFS upload.
- [ ] Receiver decrypts selected message with own private key and sender public key.
- [ ] Attachment decrypt happens only when requested.
- [ ] Negative tests prove plaintext is absent and tampering fails.

## Proof Required

- `proof/SB03/transcripts/failing-first-plaintext-message-storage.txt`
- `proof/SB03/transcripts/failing-first-plaintext-ipfs-attachment.txt`
- `proof/SB03/transcripts/passing-secure-message-domain-tests.txt`
- `proof/SB03/transcripts/source-assertions.txt`
- `proof/SB03/transcripts/anti-stub-audit.txt`
- `proof/SB03/changed-file-hashes.json`
- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`

## Browser Validation Logging

- Supporting browser proof may be captured if service behavior is only reachable through WASM, but main runtime UI proof is SB04/SB05.

## Progression Gate

- SB04 may start only after tests prove encrypted BO state, encrypted IPFS bytes, selected-message decrypt, and selected-attachment decrypt semantics.

## Suggested Agent Prompt

Execute SB03. Build secure message and encrypted attachment domain services on top of SB02, prove no plaintext persistence and lazy decrypt semantics, and do not start UI work until domain proof passes.
