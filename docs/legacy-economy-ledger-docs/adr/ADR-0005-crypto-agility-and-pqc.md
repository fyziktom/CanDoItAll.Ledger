# ADR-0005: Crypto agility and post-quantum readiness

## Status

Accepted

## Context

Signature algorithms may change over the lifetime of the ledger. Post-quantum signatures are becoming available but remain platform-dependent.

## Decision

Use algorithm-agile signature bundles. Implement VEF secp256k1/ECDSA first. Add ML-DSA support through an isolated provider and enable hybrid signature policies later.

## Consequences

- Historical signatures remain verifiable by algorithm id.
- New address and admin key algorithms can be introduced without schema redesign.
- Production PQC can be enabled when platform support is ready.
