# Assumptions And Risks

## Assumptions

- AccountApp Docker Alice/Bob accounts have or can create at least one signed transaction each, allowing public-key discovery.
- P-256 SPKI/PKCS#8 material generated for ECDSA can be imported by WebCrypto for ECDH with the same named curve; this must be proven in browser before closing SB02.
- Existing node APIs can query transactions by address with signature bundles; if not, SB02 must add or expose the minimal read path.
- IPFS API upload is available in Docker for AccountApp attachment proof.
- Large-screen UX is the only responsive target for this bundle.

## Critical Path Risks

- WebCrypto ECDH and .NET `ECDiffieHellman.DeriveKeyFromHash` may produce incompatible derived keys unless derivation is specified exactly.
- Reusing signing account keys for ECDH has security implications; proof must show the protocol is explicit, versioned, bound with AAD, and does not expose private key material.
- Receiver public-key discovery can be spoofed if it trusts arbitrary BO metadata instead of signed transaction input public key material.
- Attachment encryption can be accidentally skipped if the implementation reuses the generic BO wizard upload path.
- UI may appear correct while eagerly decrypting list previews; tests must explicitly reject bulk list decryption.
- Docker E2E may pass message text without proving IPFS attachment bytes are encrypted; validation must inspect stored content or gateway response.

## Validation Risks

- Component tests alone cannot prove WebCrypto behavior; SB02 requires browser proof.
- Browser screenshots alone cannot prove crypto behavior; tests and network/storage assertions are required.
- Happy-path Alice/Bob fixtures can hide key-discovery problems; negative proof must include an address with no discoverable public key and a mismatched public key.
- If Playwright cannot access camera/file-picker or IPFS upload controls reliably, final proof must use deterministic file upload and direct API/gateway inspection while still validating the UI path.

## Reopen Triggers

- Any proof shows secure message BO state contains plaintext subject/body.
- Any proof shows IPFS stored attachment bytes contain plaintext or original file signature/content.
- Any proof shows inbox list decrypted message body before open.
- Receiver public-key discovery accepts a public key whose derived address does not equal the receiver address.
- Browser WebCrypto and .NET derive different shared keys for the same account pair.
- Docker WASM path uses server-side signing/encryption instead of browser WebCrypto.
- A later UI or Docker phase exposes a protocol weakness; reopen SB02 or SB03 before continuing.
