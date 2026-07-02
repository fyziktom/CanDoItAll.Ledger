# SB03 Proof Manifest

## Status

- Result: `Completed`
- Date: `2026-07-02`

## Source Proof

- Secure message domain: `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs`
- Account message security adapter: `repo://src/CanDoItAll.Economy.BusinessObjects/AccountMessageSecurity.cs`
- IPFS storage parser and driver: `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs`
- Semantic contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Hashes

- `FFD861DF9C7BBA1DFDFC52F407E823F56B910FFF06126956EE1410F2371D824E` `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs`

## Test Proof

- `repo://tests/CanDoItAll.Ledger.Tests/SecureLedgerMessageTests.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`
- Passing transcript: `bundle://proof/SB03/transcripts/sb03-proof-audit.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/sb03-proof-audit.txt`
- Failing-first: N/A, process non-production exemption; adversarial negative behavior is covered by deterministic unit tests rather than a separate failed command transcript.

## Closure

- Subject, body, attachment descriptors, and attachment bytes are encrypted before ledger or IPFS persistence.
- Attachments decrypt only through the explicit attachment-open path.
- Both sender and receiver can decrypt their own conversation content using the same ECDH protocol.
