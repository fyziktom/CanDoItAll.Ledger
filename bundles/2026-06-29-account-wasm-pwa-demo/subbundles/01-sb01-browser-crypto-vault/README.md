# SB01 Browser Crypto Vault

## Status

- `Closed - proof passed`

## Objective

- Build and prove the browser/non-browser crypto boundary, encrypted IndexedDB key vault, and compatibility vectors required before any AccountApp account flow or transaction signing work continues.

## Success Criteria

- Account private key material is encrypted before persistence and stored in IndexedDB only as encrypted data plus public metadata.
- Browser WebCrypto and non-WASM .NET providers produce compatible hash/address/signature/encryption behavior for supported algorithms.
- Wrong password, corrupted vault data, and tampered backup material are rejected by tests.
- No NBitcoin or BouncyCastle dependency is introduced into the AccountApp/browser path.

## Covered Inputs

- RQ06, RQ07, RQ08, RQ11, RQ12, RQ13, RQ14, RQ15, RQ16, RQ30.
- Architect note that encryption is the first step and must be validated/tested before anything else.
- VEFramework crypto precedent and warnings captured in `bundle://evidence/veframework-reference-map.md`.

## Prerequisites

- `bundle://analysis/01-current-state.md`
- `bundle://architecture/01-target-solution.md`
- `bundle://evidence/veframework-reference-map.md`

## Exact Source References

- repo://src/CanDoItAll.Economy.Core/ValueObjects.cs
- repo://src/CanDoItAll.Economy.Core/Signatures.cs
- repo://src/CanDoItAll.Economy.Cryptography/EcdsaP256SignatureProvider.cs
- repo://src/CanDoItAll.Economy.Cryptography/EcdsaP256KeyMaterial.cs
- repo://src/CanDoItAll.Economy.Cryptography/EcdhP256MessageCipher.cs
- repo://src/CanDoItAll.Economy.Cryptography/Hex.cs
- repo://src/CanDoItAll.Economy.BusinessObjects/AccountBackup.cs
- repo://tests/CanDoItAll.Ledger.Tests
- bundle://evidence/veframework-reference-map.md

## Deliverables

- Host-neutral crypto driver abstractions for random bytes, SHA256, AES-GCM, password KDF, ECDSA key generation/import/export/sign/verify, and signature format conversion.
- Non-WASM provider using built-in .NET cryptography.
- WASM provider using browser WebCrypto via JS interop.
- Browser IndexedDB encrypted vault service with create, save, unlock, delete, and metadata-read operations.
- Deterministic compatibility vectors shared by .NET tests and browser tests.
- Dependency audit proving AccountApp/browser path excludes NBitcoin and BouncyCastle.

## Dependency Impact

- SB02 cannot implement create/unlock/import/backup without this vault.
- SB04 cannot prove browser-side signing without this signer.
- SB06 cannot launch meaningful AccountApp flows without this foundation.
- SB07 final closure must reuse this proof to reject key-leak and stubbed-crypto regressions.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Identify current .NET crypto calls that must route through an abstraction in WASM.
2. Define the smallest crypto-driver contracts and DI registrations for non-WASM and WASM.
3. Implement .NET provider behavior using built-in .NET cryptography.
4. Implement WASM provider behavior using WebCrypto and secure browser randomness.
5. Implement IndexedDB vault storage for encrypted private key material and public metadata.
6. Add deterministic compatibility vectors and negative fixtures.
7. Add browser tests that exercise real WebCrypto and IndexedDB.
8. Update workbook rows, execution report, and proof manifest.

## Scope Exceptions

- Do not build the full AccountApp UI in this subbundle.
- Do not wire transaction composer flows beyond the minimal signature/vector harness needed for proof.

## Do Not Do

- Do not add NBitcoin or BouncyCastle to the AccountApp/browser path.
- Do not store raw private keys in local storage, IndexedDB, logs, test output, or node requests.
- Do not use `DateTime` or other weak sources for IV/random generation.
- Do not copy VEFramework static `SymetricProvider.jsRuntime` or broad `AppData` design.

## Acceptance Checklist

- Browser vault create/save/unlock succeeds with correct password.
- Browser vault unlock fails with wrong password.
- Tampered encrypted data fails closed.
- Browser-created address/hash matches .NET vector.
- Browser-created signature verifies through .NET/node provider.
- Non-WASM .NET crypto tests still pass.
- Dependency audit confirms no forbidden browser dependencies.

## Proof Required

- `proof/SB01/manifest.md` with command transcript links, changed-file hashes, anti-stub audit, and source assertion list.
- Unit test command output for crypto vectors and negative fixtures.
- Browser test output for WebCrypto and IndexedDB operations: `proof/SB01/browser-harness-result.json`.
- Proof artifact showing encrypted IndexedDB record shape without raw key material: `proof/SB01/browser-harness-result.json` and `proof/SB01/sb01-browser-crypto-harness.png`.
- Production Behavior Artifact Matrix for vault records and key lifecycle states.
- Negative fixture proof for wrong password, tampered cipher text, and incompatible signature format.

## Browser Validation Logging

- Target route: temporary crypto/vault test harness route or AccountApp setup route created by this subbundle.
- Viewports: desktop 1440x1000 and mobile 390x844.
- Actions: create key, encrypt, persist, reload context, unlock, reject wrong password, tamper record, verify signature.
- Screenshots/evidence: `proof/SB01/browser-vault-desktop.png`, `proof/SB01/browser-vault-mobile.png`, and IndexedDB inspection artifact.
- Review questions: Is any raw private key visible in DOM, console, network, local storage, or IndexedDB?

## Progression Gate

- Downstream subbundles may proceed only after SB01 proof shows real browser WebCrypto plus IndexedDB behavior, .NET compatibility, negative tests, and no forbidden dependencies.

## Suggested Agent Prompt

```text
Implement SB01 only. Build the browser/non-browser crypto driver and IndexedDB encrypted vault foundation, prove it with .NET and real browser tests, update the workbook and execution report, and stop if browser crypto compatibility cannot be proven.
```
