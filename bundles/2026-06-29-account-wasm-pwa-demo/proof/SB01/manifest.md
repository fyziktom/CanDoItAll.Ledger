# SB01 Proof Manifest - Browser Crypto Vault

## Status

- Closure result: `Passed`
- Date: `2026-06-29`
- Scope closed: host-neutral crypto driver contract, .NET crypto provider, browser WebCrypto provider, IndexedDB vault store, encrypted vault service, compatibility and negative tests, dependency audit.
- Downstream gate: SB02, SB04, SB06, and SB07 may consume SB01; they remain blocked on their own prerequisites.

## Implementation Summary

- Added `IEconomyCryptoDriver` with async random, SHA-256, PBKDF2-SHA256, AES-256-GCM, and ECDSA P-256 operations.
- Added `DotNetEconomyCryptoDriver` using built-in .NET cryptography for non-WASM hosts.
- Added `EconomyAddressCodec` so browser paths can derive hashes and addresses through the injected crypto driver instead of direct `SHA256.HashData` calls.
- Added `AccountVaultService` and `IAccountVaultStore`, with encrypted private key persistence, metadata authenticated as AES-GCM associated data, unlock verification, delete, and in-memory test store.
- Added `CanDoItAll.Economy.Cryptography.Browser` Razor class library with JS interop registration, `BrowserEconomyCryptoDriver`, `BrowserIndexedDbAccountVaultStore`, and `wwwroot/candoitall-economy-crypto.js`.
- Updated the P-256 verifier to accept WebCrypto raw 64-byte IEEE P1363 signatures while preserving existing .NET signatures.
- Added `BrowserCryptoVaultTests` covering compatibility vectors, encryption, vault round trip, wrong password, and tamper rejection.
- Updated the planning workbook so SB01 rows are closed/passed without closing downstream subbundles.

## Source Assertions

- Browser path does not reference `NBitcoin`, `BouncyCastle`, `Org.BouncyCastle`, or `Portable.BouncyCastle`.
- Browser key material generation uses WebCrypto `ECDSA` P-256 with extractable SPKI/PKCS8 material because SB02 backup/import needs encrypted export/import semantics.
- Browser private key persistence stores only encrypted PKCS8 bytes in IndexedDB, plus public metadata.
- AES-GCM associated data binds vault id, address, algorithm, public key hash, public key, salt, nonce, KDF iterations, KDF name, and encryption name.
- Unlock verifies that decrypted private key exports the stored public key, derives the stored address/hash, and can sign/verify a fixed challenge.
- WebCrypto ECDSA signatures are raw 64-byte P1363; .NET verifier accepts that format for node-side validation.

## Production Behavior Artifact Matrix

| Artifact | Behavior Proven | Evidence |
| --- | --- | --- |
| `AccountVaultRecord` | Stores public metadata plus encrypted private key only | `transcripts/04-browser-crypto-vault-tests-green.txt`, `browser-harness-result.json` |
| `AccountVaultService.CreateAsync` | Generates P-256 key, derives address/hash, encrypts PKCS8 private key, persists encrypted record | `BrowserCryptoVaultTests.Account_vault_creates_encrypted_record_and_unlocks_private_key` |
| `AccountVaultService.UnlockAsync` | Rejects wrong password/tamper and verifies decrypted key ownership | `BrowserCryptoVaultTests.Account_vault_rejects_wrong_password`, `BrowserCryptoVaultTests.Account_vault_rejects_tampered_ciphertext` |
| `DotNetEconomyCryptoDriver` | Provides non-WASM SHA, random, KDF, AES-GCM, P-256 signing/verify | `transcripts/04-browser-crypto-vault-tests-green.txt` |
| `BrowserEconomyCryptoDriver` | Builds with JS interop provider and production static asset | `transcripts/05-browser-crypto-project-build.txt` |
| `candoitall-economy-crypto.js` | Uses browser WebCrypto and IndexedDB in a real browser | `browser-harness-result.json`, `sb01-browser-crypto-harness.png` |

## Negative Fixture Proof

| Fixture | Expected Result | Evidence |
| --- | --- | --- |
| Wrong password | Vault unlock throws `CryptographicException` | `transcripts/04-browser-crypto-vault-tests-green.txt` |
| Tampered ciphertext | Vault unlock throws `CryptographicException` | `transcripts/04-browser-crypto-vault-tests-green.txt` |
| Tampered signed payload | WebCrypto verify returns false | `browser-harness-result.json` |
| Forbidden dependencies | No source/package references to NBitcoin/BouncyCastle in browser path | `transcripts/06-browser-dependency-audit.txt`, `transcripts/07-browser-package-audit.txt` |

## Browser Proof

- Harness: `browser-harness.html`
- Browser result: `browser-harness-result.json`
- Screenshot: `sb01-browser-crypto-harness.png`
- Semantic invariants: `semantic-invariants.md`
- Result summary:
  - `random:getRandomValues`
  - `digest:sha256`
  - `kdf:pbkdf2-sha256`
  - `cipher:aes-256-gcm`
  - `signature:ecdsa-p256-sha256`
  - `storage:indexeddb-vault`
  - raw signature length: `64`
  - unlocked vault signature length: `64`
  - public key material length: `91`
  - private key material length: `138`
- The final rerun encrypted the generated PKCS8 private key, saved it in IndexedDB, reloaded it, decrypted it, and signed with the decrypted private key.
- The only browser console error was a local proof-server `favicon.ico` 404.

## Command Transcripts

| Transcript | Result |
| --- | --- |
| `transcripts/01-failing-browser-crypto-vault-tests.txt` | Expected red test before SB01 implementation; missing API/compiler failures |
| `transcripts/02-browser-crypto-vault-tests-after-implementation.txt` | Blocked by live `CanDoItAll.Economy.Node` output lock |
| `transcripts/03-browser-crypto-vault-tests-after-lock-clear.txt` | Tests executed; exception exact-type issue found |
| `transcripts/04-browser-crypto-vault-tests-green.txt` | `BrowserCryptoVaultTests`: 6 passed |
| `transcripts/05-browser-crypto-project-build.txt` | Browser crypto project build succeeded |
| `transcripts/06-browser-dependency-audit.txt` | No forbidden dependency source hits |
| `transcripts/07-browser-package-audit.txt` | Browser package graph includes only `Microsoft.JSInterop` and DI abstractions |
| `transcripts/08-full-ledger-tests.txt` | Full `CanDoItAll.Ledger.Tests`: 173 passed |
| `transcripts/09-workbook-regeneration.txt` | XLSX regenerated; formula error scan matched 0 entries |
| `transcripts/10-solution-build-after-sb01.txt` | Full solution build failed outside SB01: IPFS NodeControl missing CanDoItAll.Components plus live Components.Demo output lock |

## Validation Commands

```powershell
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore --filter BrowserCryptoVaultTests
dotnet build src/CanDoItAll.Economy.Cryptography.Browser/CanDoItAll.Economy.Cryptography.Browser.csproj
rg -n "NBitcoin|BouncyCastle|Org\.BouncyCastle|Portable\.BouncyCastle" src/CanDoItAll.Economy.Cryptography.Browser src/CanDoItAll.Economy.Cryptography tests/CanDoItAll.Ledger.Tests
dotnet list src/CanDoItAll.Economy.Cryptography.Browser/CanDoItAll.Economy.Cryptography.Browser.csproj package --include-transitive
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore
dotnet build CanDoItAll.Ledger.slnx --no-restore
```

## Known Non-SB01 Blockers

- `dotnet build CanDoItAll.Ledger.slnx --no-restore` fails in `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl` because it references missing `CanDoItAll.Components` namespaces/projects.
- The same solution build also observed a live `CanDoItAll.Economy.Components.Demo (14860)` process locking copied DLLs. That process was not started by SB01 implementation.
- These blockers do not affect the SB01 project build or `CanDoItAll.Ledger.Tests` proof.

## Changed File Hashes

- See `changed-file-hashes.txt`.
