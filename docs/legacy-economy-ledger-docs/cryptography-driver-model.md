# Cryptography driver model

The ledger is crypto-agile.

## Signature envelope

Every signature includes enough metadata to dispatch verification by algorithm:

- algorithm id;
- key id;
- public key hash or public key material reference;
- payload hash;
- signature bytes.

## Mandatory fallback

The first real implementation must be a VEF-compatible secp256k1/ECDSA driver. The implementation includes an adapter interface:

```text
IVefSecp256K1SignatureAdapter
```

Codex should wire this to the implementation used in `virtualeconomyframework` or a compatible local implementation.

## PQC path

The PQC drivers are optional and runtime-gated. ML-DSA and SLH-DSA are signature algorithms. ML-KEM is a key encapsulation mechanism and must not be used as a transaction signature algorithm.

## Admin signatures

Admin signatures are an anti-tamper layer over persisted state and projections. They do not replace UTXO validation.
