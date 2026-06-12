# Headless node and UI integration

## Decision

Ledger core must be runnable as a small node without UI.

## Paths

```text
CanDoItAll.Economy.Node
  - hosted node
  - validation and confirmation loop
  - projection maintenance
  - admin signatures

CanDoItAll.Economy.Cli
  - ledger init
  - address register
  - tx build/sign/submit
  - utxo list
  - projection rebuild
  - ledger verify

integrations/CanDoItAll.Modules.Economy
  - optional UI shell
  - BaseLib dashboard/components
```

The UI must not be required to run or validate ledger transactions.
