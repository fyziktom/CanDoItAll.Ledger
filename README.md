# CanDoItAll.Ledger

CanDoItAll.Ledger owns the ledger, node, UTXO/blockchain persistence, ledger UI, ledger SDK/CLI, demos, and ledger-specific tests that used to live inside the Economy repository.

CanDoItAll.Economy remains the basic simulator repository: basic accounts, scenario packs, Smith-like experimental economics, visualization, WebGL observer evidence, and simulator host behavior stay there.

## Local Commands

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\audit-ledger-boundaries.ps1
```

## Namespace Migration

The first repository split may temporarily preserve imported `CanDoItAll.Economy.*` namespaces to avoid combining a physical move with a broad public API rename. Those compatibility namespaces are tracked in `docs/migration/import-inventory.md` and must be retired after the two-repo build/test matrix is stable.
