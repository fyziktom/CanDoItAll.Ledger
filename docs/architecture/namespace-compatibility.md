# Namespace Migration And Persisted Compatibility

The repository-owned .NET identity migration from `CanDoItAll.Economy.*` to
`CanDoItAll.Ledger.*` is complete. Active projects, assemblies, root/source
namespaces, types, configuration prefixes, Docker assets, examples, tools, and
tests use the Ledger identity.

No compatibility namespace, facade, type-forwarding assembly, or duplicate
project was added. Consumers of repository-built assemblies must update their
package/project references and imports to the target identities.

## Completed Namespace Map

| Former namespace/project | Target namespace/project | Owner | Sunset milestone |
| --- | --- | --- | --- |
| `CanDoItAll.Economy.Core` | `CanDoItAll.Ledger.Core` | Ledger contracts owner | Complete |
| `CanDoItAll.Economy.Cryptography` | `CanDoItAll.Ledger.Cryptography` | Ledger cryptography owner | Complete |
| `CanDoItAll.Economy.Ledger` | `CanDoItAll.Ledger.Engine` | Ledger execution owner | Complete |
| `CanDoItAll.Economy.BusinessObjects` | `CanDoItAll.Ledger.BusinessObjects` | Ledger BusinessObject owner | Complete |
| `CanDoItAll.Economy.Sdk` | `CanDoItAll.Ledger.Sdk` | Ledger SDK owner | Complete |
| `CanDoItAll.Economy.Node` | `CanDoItAll.Ledger.Node` | Ledger node owner | Complete |
| `CanDoItAll.Economy.Persistence.EFCore` | `CanDoItAll.Ledger.Persistence.EFCore` | Ledger persistence owner | Complete |
| `CanDoItAll.Economy.Persistence.PostgreSql` | `CanDoItAll.Ledger.Persistence.PostgreSql` | Ledger persistence owner | Complete |
| `CanDoItAll.Economy.Components` | `CanDoItAll.Ledger.Components` | Ledger UI owner | Complete |
| `CanDoItAll.Economy.Simulation.Abstractions` | `CanDoItAll.Ledger.Simulation.Abstractions` | Ledger simulation owner | Complete |
| `CanDoItAll.Economy.Simulation.Ledger` | `CanDoItAll.Ledger.Simulation.Ledger` | Ledger simulation owner | Complete |
| `CanDoItAll.Economy.Storage.InMemory` | `CanDoItAll.Ledger.Storage.InMemory` | Ledger storage owner | Complete |
| `CanDoItAll.Economy.Cli` | `CanDoItAll.Ledger.Cli` | Ledger CLI owner | Complete |
| `CanDoItAll.Economy.Simulator*` | `CanDoItAll.Ledger.Simulator*` | Experimental Simulator owners | Complete |

`CanDoItAll.Ledger.Engine` is deliberately named for its validation, execution,
replay, and chain-verification responsibility. It avoids the misleading
`CanDoItAll.Ledger.Ledger` collision and remains distinct from contract-focused
`CanDoItAll.Ledger.Core`.

## Persisted Compatibility Allowlist

The source namespace migration does not rename already-persisted wire, browser,
backup, or relational schema identities. These values are compatibility
contracts rather than active product namespaces:

- `candoitall-economy-accountapp-vault-backup-v1`
- `candoitall-economy-address-directory-v1`
- `candoitall-economy-tags-v1`
- `candoitall-economy-node-operations-public-v1`
- `candoitall-economy-accountapp-settings-backup-v1`
- `candoitall-economy-account-backup-v1`
- PostgreSQL table, index, primary-key, and check-constraint names with the
  `Economy_`, `PK_Economy_`, `IX_Economy_`, or `CK_Economy_` prefixes

Changing those values requires a separately designed compatibility migration
with dual-read/explicit schema migration behavior and downgrade evidence. They
must not be mechanically renamed as part of source identity cleanup.

## Enforcement

Run:

```powershell
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
```

The validator scans active product projects, examples, tests, tools, Docker
assets, the solution, and Compose configuration. It permits only the exact
versioned format identifiers above; database schema names remain outside its
namespace/config/type patterns.

## New-Code Guidance

- Use `CanDoItAll.Ledger.*`, `Ledger__*`, and `CANDOITALL_LEDGER_*` for all new
  source, configuration, and infrastructure identities.
- Do not reintroduce Economy namespace shims or `Ledger.Ledger` names.
- Treat persisted compatibility identifiers as frozen contracts, not naming
  examples for new formats.
- Keep references to the separate `CanDoItAll.Economy` repository confined to
  ownership/boundary documentation and adversarial audit fixtures.
