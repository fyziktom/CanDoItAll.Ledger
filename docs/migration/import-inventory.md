# Ledger Import Inventory

Status: SB01 import plan, before code movement.

## Repository Role

`CanDoItAll.Ledger` owns ledger, node, UTXO/blockchain persistence, ledger UI, ledger demos, CLI, SDK, and ledger-specific tests. It does not own the Economy basic simulator, Smith-like experimental economics, scenario packs, or WebGL observer evidence.

## Planned Imports From Economy

| Economy source | Ledger target | Notes |
| --- | --- | --- |
| `src/CanDoItAll.Economy.Core` | `src/CanDoItAll.Economy.Core` initially | Ledger transaction/UTXO/storage contracts. Namespace retained temporarily as a compatibility shim. |
| `src/CanDoItAll.Economy.Cryptography` | `src/CanDoItAll.Economy.Cryptography` initially | Signature, hashing, and key support. Namespace retained temporarily. |
| `src/CanDoItAll.Economy.Ledger` | `src/CanDoItAll.Economy.Ledger` initially | Ledger validation and confirmation logic. Namespace retained temporarily. |
| `src/CanDoItAll.Economy.BusinessObjects` | `src/CanDoItAll.Economy.BusinessObjects` initially | Ledger-bound BusinessObject model and transaction builders. Namespace retained temporarily. |
| `src/CanDoItAll.Economy.Sdk` | `src/CanDoItAll.Economy.Sdk` initially | Node HTTP driver and ledger facade. Namespace retained temporarily. |
| `src/CanDoItAll.Economy.Simulation.Ledger` | `src/CanDoItAll.Economy.Simulation.Ledger` initially | Ledger-backed simulation adapter; must not bring basic scenario packs. |
| `src/CanDoItAll.Economy.Storage.InMemory` | `src/CanDoItAll.Economy.Storage.InMemory` initially | In-memory ledger store. |
| `src/CanDoItAll.Economy.Node` | `src/CanDoItAll.Economy.Node` initially | Ledger node host and admin UI. |
| `src/CanDoItAll.Economy.Cli` | `src/CanDoItAll.Economy.Cli` initially | Ledger CLI. |
| `src/CanDoItAll.Economy.Persistence.EFCore` ledger files | `src/CanDoItAll.Economy.Persistence.EFCore` initially | Ledger EF store/projections; simple economy EF files stay in Economy or are split. |
| `src/CanDoItAll.Economy.Persistence.PostgreSql` ledger files | `src/CanDoItAll.Economy.Persistence.PostgreSql` initially | Ledger PostgreSQL registration/migrations; simple economy PostgreSQL files stay in Economy or are split. |
| `src/CanDoItAll.Economy.Components` ledger/admin/BusinessObject UI | `src/CanDoItAll.Economy.Components` initially | Ledger UI components; simulator UI remains in Economy. |
| `examples/CanDoItAll.Economy.AppHost*` | `examples/` | Ledger node apphost/demo scaffolding. |
| `examples/CanDoItAll.Economy.ConsoleDemo` | `examples/` | Ledger console demo. |
| `tools/CanDoItAll.Economy.DemoSeeder` | `tools/` | Ledger demo seeder. |
| ledger tests from `tests/CanDoItAll.Economy.Tests` | `tests/CanDoItAll.Ledger.Tests` | Move ledger/node/persistence tests; keep basic simulator tests in Economy. |

## Compatibility Namespace Deletion Plan

The first migration pass may preserve `CanDoItAll.Economy.*` namespaces inside the Ledger repo to keep behavior stable while physical ownership moves. These compatibility namespaces are liabilities with owner, cost, and sunset:

| Shim | Owner | Benefit | Cost | Sunset condition |
| --- | --- | --- | --- | --- |
| `CanDoItAll.Economy.Core` in Ledger repo | Ledger repo maintainer | Avoids broad semantic rename during physical split. | Confusing package identity and docs. | Rename to `CanDoItAll.Ledger.Core` after both repos build and public API mapping is documented. |
| `CanDoItAll.Economy.Ledger` in Ledger repo | Ledger repo maintainer | Keeps ledger tests and demos stable during import. | Old repo name remains visible in code. | Rename with package migration guide and compatibility type-forwarding if needed. |
| `CanDoItAll.Economy.Node` in Ledger repo | Ledger repo maintainer | Preserves node host startup while moving host ownership. | Old host name implies Economy ownership. | Rename once Economy no longer depends on node browser evidence. |
| `CanDoItAll.Economy.BusinessObjects` and `.Sdk` in Ledger repo | Ledger repo maintainer | Keeps ledger API clients and object workflows stable. | Old namespace hides Ledger ownership. | Rename after ledger package boundaries and client docs are updated. |

## Ledger Audit Gate

`scripts/audit-ledger-repo-does-not-contain-basic-simulator.ps1` fails if basic simulator scenario packs or drivers are copied into the Ledger repo.
