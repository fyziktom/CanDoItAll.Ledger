# CanDoItAll.Economy.Sdk

Ledger-owned client facade for node and ledger operations.

The compatibility namespace remains during the split. New client-facing behavior should document public API impact and follow `docs/architecture/namespace-compatibility.md`.

Boundary: this SDK talks to Ledger services. It must not become a bridge back into the separated simulator repository.
