# Repository Boundaries

## Ownership

`CanDoItAll.Ledger` owns ledger core, node host, UTXO/blockchain persistence, ledger PostgreSQL/EF wiring, ledger UI components, ledger SDK, CLI, demos, ledger-specific tests, and the BO/project simulator runtime.

`CanDoItAll.Economy` owns basic accounts, market helpers, simple-account simulator drivers, Smith-like experimental economics, scenario packs, visualization, WebGL observer evidence, and the new simple/Smith Economy simulator host.

## Dependency Rule

Economy must not reference Ledger.

Ledger may reference a small neutral Economy helper package only if it has no dependency on simulation, Node, Ledger, EF, WebGL, or runtime host code. Any such reference must be documented with an owner, expected benefit, complexity cost, validation cost, and retirement condition.

## Compatibility Namespace Rule

Imported code may keep old `CanDoItAll.Economy.*` namespaces only as temporary compatibility shims while physical ownership is separated. New ledger code should use `CanDoItAll.Ledger.*`.

Compatibility shims must be tracked in `docs/migration/import-inventory.md` and removed after package boundaries and consumer docs are updated.

## Basic Simulator Exclusion

Ledger must not copy Economy scenario packs, `Simulation.SimpleAccounts`, `Simulation.ExperimentalEconomics`, Smith-like experiment drivers, WebGL observer evidence, or simple/Smith simulator host projects.

Ledger may own `CanDoItAll.Ledger.Simulator*` projects only for the BO/project simulator runtime moved from Economy. Those projects must not import simple-account drivers, Smith mechanisms, scenario packs, or WebGL observer evidence.
