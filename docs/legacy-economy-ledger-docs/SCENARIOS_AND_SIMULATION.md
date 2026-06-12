# Scenarios And Simulation

The simulator has two lanes. Keep them separate when adding packs, running evidence, or explaining results.

## Basic-Accounts Simulator Lane

The basic-accounts simulator lane is the default path for scenario packs, state/account drivers, Smith-style experiments, stress campaigns, and browser observer visualization. It runs from neutral simulator-owned content under `src/CanDoItAll.Economy.SimulationSandbox/ScenarioPacks/EconomySimulationSandbox`.

This lane uses scenario definitions, parameters, placements, run plans, invariants, visual mappings, deterministic hashes, and headless artifact manifests. It must not require ledger persistence, transaction verification, BusinessObjects, SDK clients, or the browser host to run scenario suites and stress campaigns.

## Ledger-Backed Simulation Lane

The ledger-backed simulation lane is explicit. It may use ledger snapshots, transaction validation, durable projections, persistence, and host integration when a scenario is specifically about ledger-backed behavior.

Ledger-backed behavior belongs in ledger simulation packages or host registration code. It must not become the default backend selector, default scenario-pack catalog, or headless evidence path for basic-accounts experiments.

## Determinism

Given the same input pack, scenario definition, parameters, run plan, policy, and seed, simulator outputs must be deterministic. UI playback speed is intentionally excluded from deterministic setup hashes because it changes observation speed, not scenario identity.

## Product Evidence Commands

Use product tools and product output directories for local evidence generation.

Baseline scenario suite:

```powershell
dotnet run --project tools\CanDoItAll.Economy.ExperimentInputPackTools -- scenario-probe --suite baseline --output artifacts\economy\simulator\scenario-suite\baseline
```

Default stress and collapse campaign:

```powershell
dotnet run --project tools\CanDoItAll.Economy.ExperimentInputPackTools -- stress-campaign --output artifacts\economy\simulator\stress-campaign\default
```

Headless determinism and performance export:

```powershell
dotnet run --project tools\CanDoItAll.Economy.ExperimentInputPackTools -- simulator-headless-regression --output artifacts\economy\simulator\headless-regression
```

Large-screen browser observer evidence may use a registered `browser-observer-host`. The current app host is a `node-host-adapter`: it serves the route and copies neutral scenario-pack content into its output, but it does not own the basic packs or economic runtime semantics.

Browser observer evidence should use a large viewport and remains visualization evidence only. Economic claims are closed by headless matrices, invariants, oracles, deterministic hashes, and trajectory summaries.

## Scenario Pack Manifests

Basic scenario packs live under the neutral simulator catalog and are loaded through `IEconomySimulationScenarioCatalog`. Browser and UI flows select scenarios by catalog descriptor or scenario source, not by direct host file-system paths.

Each runtime scenario directory includes `scenario.manifest.json` with:

- stable `scenarioId`, title, version, backend, and summary metadata;
- every runtime-required file in `requiredFiles`;
- a strict `packHash` using `sha256:<64 lowercase hex characters>`;
- `fileHashes` entries for every required file.

The catalog rejects missing files, unsafe relative paths, missing hashes, mismatched hashes, changed pack hashes, and undeclared files unless exploratory extra files are explicitly allowed.

## Scenario Suites

Scenario suites are manifest-driven product contracts. A suite manifest declares suite id, required rows, expected run statuses, failure classification rules, and claim/evidence policy. Adding a new row should require manifest or scenario-pack data changes rather than generic runner code changes.

## Stress Campaigns

Stress campaigns are manifest-driven and classify expected terminal outcomes without treating tragedy as a runtime failure. Commons and finite-capacity families should expose stable, degraded, collapse or limit-state, mitigation or recovery, and long-run cases.

Trajectory outputs should make scarcity, concentration, policy blocking, recovery, non-convergence, and budget exhaustion visible in time-series JSON and summaries.

Stress campaign output includes `tragedy-observation-summary.json`, `tragedy-observation-summary.md`, and per-case `stress-time-series/*.json` files. Each time series carries store quantities, actor stocks, event/action counts, policy decision diagnostics, warning labels, trajectory metrics, and a deterministic hash in the campaign report.

## WebGL Replay

The simulation sandbox uses two browser replay modes:

- `full`: reset the browser scene and replay frames through the target frame.
- `incremental`: apply only the next contiguous forward frame when the browser has a known stable previous frame.

Manual Apply, scenario load, First/Last, backward Step, non-contiguous seek, and failed apply recovery should use full replay. A forward Step may use incremental replay only when the last stable browser frame is exactly the previous frame.

Pause must stop both host-side work and the WebGL runtime. Browser observer evidence records runtime diagnostics, screenshots, GLB model diagnostics, fallback reports, and pause cleanup, but it does not replace headless economic evidence.

## API Control

The simulator exposes `/api/simulator` route groups:

- `metadata` returns the function/API catalog and CFO coverage checklist.
- `projects` controls project state, validation, import/export, deterministic setup hashes, and run launch.
- `scenario-templates` exposes S1/S2/S3 business models and scenario rule packs.
- `scenarios` compiles scenario events or runs explicit scenario requests.
- `runs` lists run registry records, applies lifecycle commands and timeline step requests, and returns observations, graphs, source maps, and CFO finance analysis snapshots.
- `analysis` exposes standalone projection, actual validation, and calibration recommendation helpers.

Post-run finance analysis is management-oriented. Generic legal, tax, payroll, debt covenant, and transfer-pricing assumptions remain professional-review items, not statutory advice.
