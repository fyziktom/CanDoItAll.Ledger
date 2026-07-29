# C# Dependency Direction

## Current And Target Graph

The target graph is exactly the current `.csproj` graph after applying the
project rename map in `inventories/01-scope-inventory.md`. No edge is added,
removed, or reversed.

```mermaid
flowchart LR
  Core["Ledger.Core"] --> Crypto["Ledger.Cryptography"]
  Core --> Engine["Ledger.Engine"]
  Crypto --> Engine
  Engine --> BO["Ledger.BusinessObjects"]
  BO --> SDK["Ledger.Sdk"]
  Core --> EF["Ledger.Persistence.EFCore"]
  EF --> PG["Ledger.Persistence.PostgreSql"]
  Core --> Memory["Ledger.Storage.InMemory"]
  BO --> Rules["Ledger.Rules.NCalc"]
  BO --> UI["Ledger.Components"]
  SDK --> UI
  Engine --> Node["Ledger.Node"]
  BO --> Node
  SDK --> Node
  EF --> Node
  PG --> Node
  Memory --> Node
```

## Forbidden References

- Core to Engine/Node/UI/persistence.
- Engine to BusinessObjects/SDK/Node/UI/persistence.
- Abstractions to `Simulation.Ledger`.
- Any new cycle or sibling source dependency beyond the pre-existing explicit
  Components/IPFS solution/demo references.

## Required Proof

- Normalize each pre/post `ProjectReference` through the rename map and compare.
- Build the canonical solution.
- Rerun CodeAnalytics dependencies if the MCP health recovers; otherwise record
  manual graph plus build evidence in the architecture review gate.
