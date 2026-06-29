# SB04 Semantic Invariants

## Transaction Lifecycle

- Unsigned assembly is a distinct lifecycle state: draft transactions contain inputs, outputs, data, timestamp, kind, and signing mode, but no input signatures, no signature payload hashes, and no transaction id requirement.
- Signing is the only step that applies `TxId`, `SignaturePayloadHash`, and `SignatureBundle` values to inputs.
- Existing signed builder methods remain signed wrappers over the unsigned draft methods, preserving non-WASM/SSR behavior.
- The AccountApp/browser path can assemble drafts from API-sourced spendable outputs without calling `GetPrivateKeyMaterialAsync`.
- The AccountApp/browser path signs with the unlocked browser key only after the vault is unlocked.
- Signed broadcast sends `LedgerTransaction` data with signature/public key material and never sends private key fields or private key material.

## Cryptography

- Browser/account signing supports `EcdsaP256Sha256` through `IEconomyCryptoDriver`.
- WebCrypto P-256 signatures are raw 64-byte P1363 signatures and verify through the existing .NET/node verifier.
- Payload bytes signed by browser and .NET signers are UTF-8 bytes of the canonical input payload hash value.

## API Boundary

- AccountApp communication with the node stays API-only: read spendable outputs/list/detail and POST signed transactions.
- Browser-safe SDK registration uses `CryptoDriverAccountInputSigner` and exposes no static API key or bearer token configuration.
- The node is not asked to assemble, sign, or receive unsigned account drafts.

## Regression Scope

- Existing server/SSR transaction builder methods still return signed transactions.
- Existing ledger verification semantics and canonical transaction hashes remain unchanged.
- The SB04 proof does not implement full AccountApp UI; SB05 and SB06 own host-neutral UI adapters and app wiring.
