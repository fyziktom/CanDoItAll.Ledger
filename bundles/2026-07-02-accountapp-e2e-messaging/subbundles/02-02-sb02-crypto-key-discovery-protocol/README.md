# SB02 - Crypto Key Discovery Protocol

## Status

- Completed

## Objective

Implement and prove the cryptographic foundation: receiver public-key discovery from production signed transactions, signed transaction public-key preservation, and browser WebCrypto ECDH compatibility with the .NET protocol.

## Covered Inputs

- N002
- N003
- N004
- N005
- N012
- N013

## Prerequisites

- SB01 progression gate passed.
- Existing browser crypto driver and JS bridge are available.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Cryptography/CryptoDrivers.cs`
- `repo://src/CanDoItAll.Economy.Cryptography/EcdhP256MessageCipher.cs`
- `repo://src/CanDoItAll.Economy.Cryptography.Browser/BrowserEconomyCryptoDriver.cs`
- `repo://src/CanDoItAll.Economy.Cryptography.Browser/wwwroot/candoitall-economy-crypto.js`
- `repo://src/CanDoItAll.Economy.BusinessObjects/AccountMessageSecurity.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerContracts.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BrowserCryptoVaultTests.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`

## Deliverables

- A production public-key discovery service/function that queries transactions for an address and accepts only matching signer public keys.
- Browser crypto driver supports ECDH P-256 shared-key derivation compatible with .NET.
- WASM adapter has enough primitives to encrypt/decrypt later secure messages without server private-key use.
- Tests protect signed transaction public key material.

## Dependency Impact

- Critical foundation for SB03, SB04, and SB05. If weak, every later proof is invalid.

## Validation Depth

- Unit tests, browser/WebCrypto harness, source assertions, anti-stub audit, and semantic adequacy proof are required.

## Implementation Steps

1. Add ECDH derive API to `IEconomyCryptoDriver` or a dedicated protocol service.
2. Implement .NET and browser WebCrypto ECDH derivation with the same key derivation label and output.
3. Add public-key discovery from transaction signatures.
4. Add negative tests for no transactions, no public key, mismatched public key, and metadata-only key.
5. Add positive tests for Alice/Bob shared secret equality and transaction public-key discovery.
6. Add browser harness/Playwright proof for WebCrypto ECDH, AES-GCM, and tamper rejection.
7. Capture source assertions and changed-file hashes.

## Scope Exceptions

- Full message UI is SB04.
- Attachment encryption is SB03.

## Do Not Do

- Do not trust user-entered or BO metadata public keys for secure send.
- Do not perform WASM encryption on the server.
- Do not continue to SB03 until browser ECDH compatibility is proven.

## Acceptance Checklist

- [ ] Browser and .NET derive identical shared key for the same Alice/Bob key pair.
- [ ] Discovery returns a receiver public key only from a signed transaction input matching the receiver address.
- [ ] No-key/mismatched-key discovery fails.
- [ ] Signed transaction public-key material is still emitted and validated.
- [ ] Semantic adequacy proof names shallow-pass trap and negative/positive cases.

## Proof Required

- `proof/SB02/transcripts/failing-first-key-discovery.txt`
- `proof/SB02/transcripts/failing-first-browser-ecdh.txt`
- `proof/SB02/transcripts/passing-key-discovery-tests.txt`
- `proof/SB02/transcripts/browser-webcrypto-ecdh-proof.txt`
- `proof/SB02/transcripts/source-assertions.txt`
- `proof/SB02/transcripts/anti-stub-audit.txt`
- `proof/SB02/changed-file-hashes.json`
- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`

## Browser Validation Logging

- Record route/harness URL, large-screen viewport, actions/assertions, screenshot path, and result in `reviews/01-execution-report.md`.

## Progression Gate

- SB03 may start only after key discovery and browser ECDH compatibility pass with positive and adversarial negative proof.

## Suggested Agent Prompt

Execute SB02. Implement trusted public-key discovery from transaction signatures and browser WebCrypto ECDH compatibility, then prove it with failing-first and passing tests plus browser proof before any secure-message storage work.
