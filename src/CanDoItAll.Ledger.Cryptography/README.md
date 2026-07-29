# CanDoItAll.Ledger.Cryptography

Ledger-owned hashing, key, and signature support used by transaction validation and node flows.

The project uses the `CanDoItAll.Ledger.Cryptography` identity. Keep it focused
on cryptographic drivers and avoid host, persistence, UI, and simulator-lane
dependencies.

Validate with the Ledger test suite and boundary audits before changing signature behavior.
