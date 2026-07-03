# SB03 Semantic Invariants

- Invariant ID: `SB03-ENCRYPTED-ATTACHMENT-LOCATOR`
- Source raw note: Attachment DataItems must not be public because visible IPFS locators, file metadata, and ciphertext hashes link off-chain files to sender/receiver transactions.
- Expected behavior: New secure-message public payloads keep attachment arrays empty; encrypted message content carries the encrypted locator descriptor and attachment crypto metadata for participants.
- Disallowed shallow implementation: Removing the public attachment entry without preserving decryptable locator metadata and attachment byte recovery is not acceptable.
- Failing-first test: N/A process exemption; the raw tx leak report supplied the negative fixture and the repaired tests prove public omission plus decryptability.
- Passing test: `bundle://proof/SB03/transcripts/attachment-privacy-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs`, `repo://tests/CanDoItAll.Ledger.Tests/SecureLedgerMessageTests.cs`, `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`
- Production assertions: Tests verify `Payload.Attachments` is empty for new messages, decrypted descriptors contain encrypted locator metadata, and attachment recovery remains byte-equal.
- Red-team negative case: Raw transaction JSON is scanned for IPFS route text, file names, content types, and plaintext attachment bytes.
- Downstream dependency check: Docker Alice/Bob proof confirms participant UX still opens messages with attachments after locator hiding.
