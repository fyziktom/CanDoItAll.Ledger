# SB03 Semantic Invariants

## Invariant: Encrypted Message And Attachment Persistence

- Invariant ID: `SB03-ENCRYPTED-PERSISTENCE`
- Source raw note: `N004`, `N005`, `N008`, `N013`
- Expected behavior: Message subject/body and attachment bytes are encrypted before ledger/IPFS persistence and can be decrypted only by sender or receiver key material.
- Disallowed shallow implementation: Storing plaintext BO JSON, plaintext IPFS attachment bytes, or decrypting attachments during mailbox listing.
- Failing-first test: `repo://tests/CanDoItAll.Ledger.Tests/SecureLedgerMessageTests.cs` asserts plaintext is absent from serialized message JSON and stored attachment bytes.
- Passing test: `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs` sends and opens Alice/Bob secure messages with attachment bytes.
- Changed source files: `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs`, `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`.
- Production assertions: AES-GCM authenticated data binds sender, receiver, public-key hashes, and attachment id to ciphertext.
- Red-team negative case: Stored encrypted bytes do not contain plaintext attachment content and tampered ciphertext fails authentication.
- Downstream dependency check: SB04 UI receives encrypted metadata for mailbox rows and calls explicit decrypt handlers for content and attachments.
