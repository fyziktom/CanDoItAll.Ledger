# CanDoItAll.Ledger.Engine

Ledger-owned transaction validation, execution, asset-conservation checks, chain verification, and runtime policies.

This is the main ledger runtime layer. It may depend on core contracts and cryptography, but it must not import separated Ledger simulator drivers or scenario-pack content.

Validate runtime changes with ledger tests, boundary audits, and node smoke where host behavior is affected.
