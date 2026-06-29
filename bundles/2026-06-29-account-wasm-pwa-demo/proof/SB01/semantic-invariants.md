# SB01 Semantic Invariants

## Shallow-Pass Trap

A shallow implementation could expose methods named `Encrypt`, `Sign`, or `SaveVaultRecord` while still storing raw private key material, using non-browser crypto, accepting any signature format without proof, or only checking non-empty outputs.

SB01 rejects that trap with behavior tests and browser proof that exercise real key material, wrong-password/tamper rejection, raw WebCrypto signatures, and IndexedDB persistence.

## Invariants

| Invariant | Proof |
| --- | --- |
| Private key material is encrypted before persistence | `BrowserCryptoVaultTests.Account_vault_creates_encrypted_record_and_unlocks_private_key`; `browser-harness-result.json` |
| Wrong password cannot unlock a vault | `BrowserCryptoVaultTests.Account_vault_rejects_wrong_password` |
| Tampered ciphertext cannot unlock a vault | `BrowserCryptoVaultTests.Account_vault_rejects_tampered_ciphertext` |
| Browser WebCrypto SHA-256 is vector-compatible for address/hash work | `BrowserCryptoVaultTests.Browser_safe_address_codec_matches_core_address_derivation`; browser SHA-256 `abc` vector in `browser-harness.html` |
| Browser/WebCrypto P-256 signatures use raw 64-byte format and verify through the .NET provider | `BrowserCryptoVaultTests.Dotnet_driver_uses_webcrypto_compatible_p256_signatures`; `browser-harness-result.json` |
| IndexedDB stores encrypted vault record data, not raw private key bytes | `browser-harness.html` asserts `encryptedPrivateKeyBase64 !== privateKeyMaterialBase64` |
| Unlocked IndexedDB vault private key can sign after decrypt | `browser-harness-result.json` has `vaultUnlockSignatureLength: 64` |
| Browser project graph excludes NBitcoin/BouncyCastle | `transcripts/06-browser-dependency-audit.txt`; `transcripts/07-browser-package-audit.txt` |

## Adversarial Negative Proof

- Wrong password: vault decrypt throws a normalized `CryptographicException`.
- Tampered ciphertext: vault decrypt throws a normalized `CryptographicException`.
- Tampered browser signature payload: WebCrypto verify returns false.
- Forbidden dependency names: source audit returns no hits in browser/crypto path.

## Semantic Positive Proof

- .NET path creates an account vault, persists encrypted key data, unlocks it, re-derives the address/hash, and verifies key ownership.
- Browser path generates a P-256 key pair, encrypts the actual PKCS8 private key, stores it in IndexedDB, reloads it, decrypts it, signs with it, and verifies the signature.

## Raw-Note Literal Closure

- "Signatures and keys are on the side of the WASM app": SB01 provides browser WebCrypto signing and IndexedDB vault storage; SB04 still owns transaction-level signing integration.
- "Use browser API because dotnet does not support Cryptography lib in WASM": SB01 adds `BrowserEconomyCryptoDriver` and production JS module backed by WebCrypto.
- "No NBitcoin/BouncyCastle dependencies": SB01 browser package/source audit confirms no such dependencies in the browser crypto path.
- "Encryption is superimportant first step": SB01 closes encryption before SB02 starts and keeps downstream phases blocked until they consume this proof.
