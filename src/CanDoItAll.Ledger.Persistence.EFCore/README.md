# CanDoItAll.Ledger.Persistence.EFCore

Ledger-owned EF Core persistence for ledger state, projections, and persistence registrations.

The project uses the `CanDoItAll.Ledger.Persistence.EFCore` identity. Database
and migration changes must document provider impact and test both in-memory and
provider-backed lanes when applicable. Existing `Economy_` relational names are
stable schema compatibility identifiers, not source namespaces.

Boundary: this project persists ledger data only and must not import separated simulator-lane content.
