# C# Dependency Direction

## Current Direction

- `CanDoItAll.Economy.Ledger` -> `CanDoItAll.Economy.Core`
- `CanDoItAll.Economy.BusinessObjects` -> `Core`, `Ledger`, read clients/signing abstractions
- `CanDoItAll.Economy.Components` -> `BusinessObjects`, `Core`, UI models

## Target Direction

- No project-reference changes.
- Node validation stays below drivers and UI.
- Builders satisfy node invariants without calling UI code.

## Cycle Risk

- None expected. This repair edits existing dependency directions only.

