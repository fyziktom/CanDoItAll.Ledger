# Repository Boundaries

## Ownership

`CanDoItAll.Ledger` owns ledger core, node host, UTXO/blockchain persistence, ledger PostgreSQL/EF wiring, ledger UI components, ledger SDK, CLI, demos, ledger-specific tests, and the BO/project simulator runtime.

`CanDoItAll.Economy` owns basic accounts, market helpers, simple-account simulator drivers, Smith-like experimental economics, scenario packs, visualization, WebGL observer evidence, and the new simple/Smith Economy simulator host.

## Dependency Rule

Economy must not reference Ledger.

Ledger may reference a small neutral Economy helper package only if it has no dependency on simulation, Node, Ledger, EF, WebGL, or runtime host code. Any such reference must be documented with an owner, expected benefit, complexity cost, validation cost, and retirement condition.

## Namespace Rule

All Ledger-owned projects and source use `CanDoItAll.Ledger.*`. The former
Economy compatibility namespaces are retired; they may appear only in migration
history, explicit repository-boundary rules, and adversarial audit fixtures.

Versioned backup/browser format identifiers and the existing `Economy_`
PostgreSQL schema names remain stable compatibility contracts. Their exact
allowlist and change requirements are recorded in
`namespace-compatibility.md`.

## Basic Simulator Exclusion

Ledger must not copy Economy scenario packs, `Simulation.SimpleAccounts`, `Simulation.ExperimentalEconomics`, Smith-like experiment drivers, WebGL observer evidence, or simple/Smith simulator host projects.

Ledger may own `CanDoItAll.Ledger.Simulator*` projects only for the BO/project simulator runtime moved from Economy. Those projects must not import simple-account drivers, Smith mechanisms, scenario packs, or WebGL observer evidence.

The BO/project Simulator is work in progress and experimental. It is not
production-ready and its output is not authoritative financial, accounting,
forecasting, or economic evidence.
