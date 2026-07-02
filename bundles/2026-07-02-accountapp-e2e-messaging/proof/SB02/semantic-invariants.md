# SB02 Semantic Invariants

## Invariant: Receiver Public Key Discovery

- Invariant ID: `SB02-PUBLIC-KEY-DISCOVERY`
- Source raw note: `N002`, `N003`, `N012`
- Expected behavior: Receiver public keys are discovered from signed production ledger transactions only when the signer public key derives to the requested receiver address.
- Disallowed shallow implementation: Accepting arbitrary public-key material from transaction JSON or hard-coded account metadata.
- Failing-first test: `repo://tests/CanDoItAll.Ledger.Tests/MessagePublicKeyDiscoveryTests.cs` rejects mismatched signer addresses and public-key hashes.
- Passing test: `repo://tests/CanDoItAll.Ledger.Tests/BrowserCryptoVaultTests.cs` and `bundle://proof/SB02/transcripts/webcrypto-ecdh-proof.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerPublicKeyDiscovery.cs`, `repo://src/CanDoItAll.Economy.Cryptography.Browser/wwwroot/candoitall-economy-crypto.js`.
- Production assertions: Signed transaction input address, public-key hash, and derived address must agree before a key is returned.
- Red-team negative case: Mismatched signer public key returns no discovery result.
- Downstream dependency check: SB03 secure message encryption uses this discovery before sending to a receiver.
