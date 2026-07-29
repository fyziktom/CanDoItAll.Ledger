# CanDoItAll.Ledger.Sdk

Ledger-owned client facade for node and ledger operations.

The SDK uses the `CanDoItAll.Ledger.Sdk` identity. New client-facing behavior
must document public API impact and follow
`docs/architecture/namespace-compatibility.md`.

Boundary: this SDK talks to Ledger services. It must not become a bridge back into the separated simulator repository.
