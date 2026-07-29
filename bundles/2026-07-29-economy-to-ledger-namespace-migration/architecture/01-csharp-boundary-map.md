# C# Boundary Map

## Target Boundaries

- Existing responsibilities and project edges remain unchanged.
- The namespace root changes from `CanDoItAll.Economy` to
  `CanDoItAll.Ledger`.
- The former Ledger-named assembly becomes the more specific Engine boundary:
  `CanDoItAll.Ledger.Engine`.

## Contracts Versus Implementations

- `Ledger.Core` and `Ledger.Simulation.Abstractions` remain inner boundaries.
- `Ledger.Engine`, `Ledger.BusinessObjects`, `Ledger.Sdk`, persistence, rules,
  storage, components, Node, examples, and tools remain outward consumers.
- No contract/implementation type moves across project boundaries.

## Composition Root

- `Ledger.Node`, `Ledger.AppHost`, CLI, ConsoleDemo, and test/bootstrap paths
  keep their existing composition responsibility and change only referenced
  identities/config prefixes.

## Temporary Bridges

- None. The requested result is a direct breaking rename.
- Historical documentation/bundle evidence is not a runtime bridge.
