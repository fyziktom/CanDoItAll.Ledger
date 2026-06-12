# ADR-0007: VEF secp256k1 fallback signature driver

## Status

Accepted.

## Context

Many target systems will not support post-quantum algorithms yet. The existing VirtualEconomyFramework uses Bitcoin-style secp256k1/ECDSA concepts.

## Decision

Make VEF-compatible secp256k1/ECDSA the mandatory fallback driver. Keep PQC drivers optional and runtime-gated.

## Consequences

- Ledger signing works on broadly available systems.
- Historical transactions remain verifiable by algorithm id.
- Future ML-DSA/SLH-DSA signatures can be added without rewriting transaction format.
