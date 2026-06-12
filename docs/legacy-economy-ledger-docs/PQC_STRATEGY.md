# Post-quantum cryptography strategy

## Position

The ledger should be crypto-agile from day one. It should not hardcode one signature algorithm into addresses, transaction inputs, admin signatures, or projection signatures.

## Recommended MVP algorithms

- `ECDSA_P256_SHA256` for fast development and broad platform support.
- `MLDSA65` as the preferred post-quantum signature option when the .NET 10 runtime and platform support it.
- Optional hybrid signature bundles requiring both classical and post-quantum signatures for admin keys.

## Why not only PQC immediately

Post-quantum support is emerging and platform-dependent. The ledger can support it without making the whole product blocked by platform availability.

## Signature bundle

Use a signature bundle instead of a single signature field:

```text
SignatureBundle
- signatures[]
    - algorithm
    - keyId
    - publicKeyHash
    - payloadHash
    - signatureBytes
```

A policy determines whether one valid signature is enough or whether multiple algorithms are required.

## Address record

```text
address
keyAlgorithm
publicKey
publicKeyHash
signaturePolicy
status
adminSignature
```

## Admin signature policy

The admin key is the highest-value key. Recommended policy:

- development: ECDSA only;
- production early: ECDSA + optional ML-DSA verification if supported;
- production hardened: hybrid admin signature requiring ECDSA and ML-DSA.

## PQC implementation boundary

Keep PQC code isolated in `CanDoItAll.Economy.Cryptography`. Do not let experimental APIs leak into domain contracts.

## Migration plan

1. Create algorithm-agile address and signature schema.
2. Implement ECDSA provider.
3. Implement ML-DSA provider behind runtime support checks.
4. Add admin-key rotation.
5. Add address-key rotation.
6. Add hybrid signature policy.
7. Add ledger verification support for historical algorithms.

## Key rotation

Address and admin key records must be versioned. Historical signatures must remain verifiable after rotation.

## Context/domain separation

Every signature payload should include a domain separator. Example:

```text
cdia.economy.input.independent.v1
cdia.economy.input.ordered.v1
cdia.economy.admin.ledger-entry.v1
cdia.economy.admin.projection.v1
```
