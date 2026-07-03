# SB01 Semantic Invariants

- Invariant ID: `SB01-RAW-PUBLIC-SURFACE`
- Source raw note: Secure-message raw transactions must not reveal message subject, body, attachment file names, content types, IPFS locators, or attachment plaintext.
- Expected behavior: Public transaction `Data` and BO output surfaces contain only routing/minimal type data, ciphertext, nonce, authentication tag, public keys, and hashes needed for verification.
- Disallowed shallow implementation: Hiding values in Razor UI while persisted transaction JSON still leaks them is not acceptable.
- Failing-first test: N/A process exemption; the user-supplied raw transaction was the negative fixture and the committed assertions now scan the persisted transaction surface.
- Passing test: `bundle://proof/SB01/transcripts/privacy-tests.txt`
- Changed source files: `repo://tests/CanDoItAll.Ledger.Tests/SecureLedgerMessageTests.cs`, `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`
- Production assertions: The focused tests serialize ledger transaction data and outputs, then reject known forbidden values.
- Red-team negative case: Known subjects, bodies, attachment file name, content type strings, IPFS route text, and plaintext attachment bytes are searched in raw transaction JSON.
- Downstream dependency check: `repo://tests/CanDoItAll.Ledger.Tests/TransactionUiComponentTests.cs` confirms raw tx display still renders structured transaction data.
