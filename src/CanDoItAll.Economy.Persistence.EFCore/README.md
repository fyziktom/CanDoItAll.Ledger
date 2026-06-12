# CanDoItAll.Economy.Persistence.EFCore

Ledger-owned EF Core persistence for ledger state, projections, and persistence registrations.

The project keeps its compatibility namespace until the persistence migration plan is ready. Database and migration changes should document provider impact and test both in-memory and provider-backed lanes when applicable.

Boundary: this project persists ledger data only and must not import separated simulator-lane content.
