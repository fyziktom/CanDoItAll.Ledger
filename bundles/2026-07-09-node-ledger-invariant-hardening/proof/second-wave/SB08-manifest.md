# SB08 manifest

Status: Passed

- Source: PostgreSQL migrator, design-time factory, two migrations, Node schema-mode composition.
- Adversarial proof: incomplete/invalid legacy data refuses baseline; concurrent starts serialize.
- Positive proof: empty and valid legacy schemas converge; retained Docker DB migrated.
- Artifacts: `postgresql-idempotent-migrations.sql`, `second-wave-full-final-361.trx`.

