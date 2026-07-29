# Historical Ledger Import And Namespace Migration Inventory

Status: completed repository split and Ledger identity migration.

## Repository Role

`CanDoItAll.Ledger` owns the ledger, node, UTXO/blockchain persistence, ledger
UI, demos, CLI, SDK, ledger-specific tests, and the experimental BO/project
Simulator. It does not own the separate Economy simple-account simulator,
Smith-style experiments, scenario packs, or WebGL observer evidence.

## Completed Target Inventory

| Imported responsibility | Current Ledger target |
| --- | --- |
| Transaction, UTXO, storage, and account contracts | `src/CanDoItAll.Ledger.Core` |
| Signature, hashing, and key drivers | `src/CanDoItAll.Ledger.Cryptography` |
| Validation, execution, replay, and chain verification | `src/CanDoItAll.Ledger.Engine` |
| BusinessObject transactions and account behavior | `src/CanDoItAll.Ledger.BusinessObjects` |
| SDK and node HTTP client | `src/CanDoItAll.Ledger.Sdk` |
| Ledger-backed simulation contracts and adapter | `src/CanDoItAll.Ledger.Simulation.Abstractions`, `src/CanDoItAll.Ledger.Simulation.Ledger` |
| In-memory storage | `src/CanDoItAll.Ledger.Storage.InMemory` |
| Node host | `src/CanDoItAll.Ledger.Node` |
| CLI | `src/CanDoItAll.Ledger.Cli` |
| EF Core and PostgreSQL persistence | `src/CanDoItAll.Ledger.Persistence.EFCore`, `src/CanDoItAll.Ledger.Persistence.PostgreSql` |
| Ledger and BusinessObject UI | `src/CanDoItAll.Ledger.Components` |
| AppHost, AccountApp, console, and component demos | `examples/CanDoItAll.Ledger.*` |
| Demo seeding | `tools/CanDoItAll.Ledger.DemoSeeder` |
| Ledger tests | `tests/CanDoItAll.Ledger.Tests` |

## Namespace Completion

The initial split temporarily retained `CanDoItAll.Economy.*` source identities.
That debt is retired. The current solution has 27 Ledger-owned projects and no
active Economy-named project, folder, assembly, root namespace, source namespace,
type, configuration prefix, or infrastructure identity.

The existing serialized format IDs and PostgreSQL `Economy_` schema names remain
stable compatibility contracts. See
`docs/architecture/namespace-compatibility.md`; they are not namespace shims.

## Audit Gates

```powershell
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
.\scripts\audit-ledger-repo-does-not-contain-basic-simulator.ps1 -RunNegativeFixture
.\scripts\audit-ledger-boundaries.ps1
```

The cross-repository audit remains named
`audit-economy-ledger-split.ps1` because Economy is the real name of the sibling
repository whose ownership boundary it validates.
