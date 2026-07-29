# Persisted Identity Compatibility

CanDoItAll.Ledger uses `CanDoItAll.Ledger.*` for active projects, assemblies,
namespaces, types, configuration, infrastructure, examples, tools, and tests.

Some persisted browser, backup, and relational identifiers predate the current
product identity. They remain frozen because existing data relies on their exact
values. Their text is a compatibility contract, not a naming pattern for new
features.

## Persisted Compatibility Allowlist

- `candoitall-economy-accountapp-vault-backup-v1`
- `candoitall-economy-address-directory-v1`
- `candoitall-economy-tags-v1`
- `candoitall-economy-node-operations-public-v1`
- `candoitall-economy-accountapp-settings-backup-v1`
- `candoitall-economy-account-backup-v1`
- PostgreSQL table, index, primary-key, and check-constraint names with the
  `Economy_`, `PK_Economy_`, `IX_Economy_`, or `CK_Economy_` prefixes

Changing one of these values requires an explicit data-compatibility design,
including dual-read or schema-migration behavior, upgrade evidence, and a
tested rollback path. Do not rename them during unrelated cleanup.

## Current Identity Rules

- Use `CanDoItAll.Ledger.*` for .NET namespaces and package identities.
- Use `Ledger__*` for Compose environment configuration.
- Use `CANDOITALL_LEDGER_*` for tool and process environment variables.
- Use a new versioned Ledger identifier for every new persisted format.
- Do not copy a compatibility identifier as the basis for a new contract.
- Keep `CanDoItAll.Ledger.Engine` distinct from contract-focused
  `CanDoItAll.Ledger.Core`.

## Enforcement

Run:

```powershell
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
```

The validator scans active projects, examples, tests, tools, Docker assets, the
solution, and Compose configuration. It permits only the exact compatibility
identifiers above and proves the rules reject invalid product identities.
