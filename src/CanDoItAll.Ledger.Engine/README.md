# CanDoItAll.Ledger.Engine

Deterministic transaction validation and execution for CanDoItAll.Ledger.

The engine enforces transaction structure, references, signatures, authority,
account rules, asset conservation, state transitions, replay, and chain
verification. It depends inward on Core contracts and cryptography while
remaining independent from hosts, user interfaces, and Simulator drivers.

Validate changes with the Ledger test suite, transaction golden vectors,
boundary audits, and node smoke tests when host-visible behavior changes.
