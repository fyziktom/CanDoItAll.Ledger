# CanDoItAll.Ledger.Persistence.PostgreSql

PostgreSQL registration, migrations, health checks, and provider-specific
Ledger persistence wiring.

Keep provider behavior aligned with the EF Core persistence project and
[Persisted Identity Compatibility](../../docs/architecture/namespace-compatibility.md).
Migration changes must document runtime, deployment, backup, and rollback
effects.

The provider persists confirmed Ledger state. Experimental Simulator
projections use their own persistence contracts.
