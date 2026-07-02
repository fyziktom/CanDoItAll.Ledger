# SB02 Proof Manifest

## Status

- Result: `Completed`
- Date: `2026-07-02`

## Source Proof

- Receiver public-key discovery: `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerPublicKeyDiscovery.cs`
- Browser WebCrypto ECDH bridge: `repo://src/CanDoItAll.Economy.Cryptography.Browser/wwwroot/candoitall-economy-crypto.js`
- Crypto driver contract: `repo://src/CanDoItAll.Economy.Cryptography/CryptoDrivers.cs`
- Semantic contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Hashes

- `12F7E0FE80514FE0079BF715067002CF9230A8E246E63111A921329A0ED744CB` `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerPublicKeyDiscovery.cs`
- `CC342C7C93BDF90020B002115E3C32169835BE9CDA93E8EC4EE2B6737CF2A2F0` `repo://src/CanDoItAll.Economy.Cryptography.Browser/wwwroot/candoitall-economy-crypto.js`

## Test Proof

- `repo://tests/CanDoItAll.Ledger.Tests/MessagePublicKeyDiscoveryTests.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BrowserCryptoVaultTests.cs`
- Passing transcript: `bundle://proof/SB02/transcripts/sb02-proof-audit.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/sb02-proof-audit.txt`
- Failing-first: N/A, process non-production exemption; adversarial negative behavior is covered by deterministic unit tests rather than a separate failed command transcript.

## Closure

- Public keys are accepted only when the signed transaction input address and public-key hash match the requested receiver address.
- Browser P-256 ECDH derives the same key material as the .NET path and AES-GCM tamper rejection is proven.
