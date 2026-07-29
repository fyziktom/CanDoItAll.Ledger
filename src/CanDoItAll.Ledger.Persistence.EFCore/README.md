# CanDoItAll.Ledger.Persistence.EFCore

Entity Framework Core persistence for Ledger state, projections, and
repository registrations.

Database and migration changes must document provider impact and test relevant
in-memory and relational lanes. Existing `Economy_` relational names are frozen
schema compatibility identifiers; see
[Persisted Identity Compatibility](../../docs/architecture/namespace-compatibility.md).

This project persists confirmed Ledger data. It must not treat Simulator
projections as authoritative transaction state.
