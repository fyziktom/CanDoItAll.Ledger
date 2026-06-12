# Economy Experiment Readiness

Economic simulation output is usable only when the model path is separated from projection, runtime, and UI evidence. A browser animation can validate replay behavior, but it is not economic evidence by itself.

## Readiness Bands

`EconomyExperimentReadinessReport` reports independent validity bands:

| Band | Meaning |
| --- | --- |
| `scenario` | Scenario definition, input pack hashes, strict-mode policy, and allowed warnings are valid. |
| `simulation` | Backend transition semantics completed without model-adjacent errors. |
| `metrics` | Metric and invariant definitions are known and evaluators completed without strict diagnostics. |
| `projection` | Visual/WebGL projection diagnostics when projection was exercised. Headless runs may mark this `not-run`. |
| `runtime` | Browser/WebGL runtime status when browser runtime was exercised. |
| `ui` | UI workflow status when UI was exercised. |
| `oracle` | Independent economic oracle evidence status when oracle evidence was exercised. |
| `browser-observer` | Combined browser runtime and UI observer evidence status. Missing browser evidence is reported as `not-run`, not as economic correctness. |
| `performance` | Headless deterministic budgets are hard gates; visual/browser budgets are reported separately. |

Runtime, UI, and projection failures must not be reported as economic-model failures. They reduce their own bands and can lower confidence for visualization evidence, but the scenario/simulation/metrics bands still explain whether the economic result itself is usable.

## Machine Status

Schema `economy-experiment-readiness/v3` adds top-level `status`, `researchReady`, `warningBudgetReport`, evidence records, and `gates`.

| Status | Meaning |
| --- | --- |
| `engineering-demo` | Default state before a report is finalized. |
| `exploratory` | Headless output may exist, but warnings or budgets prevent claim-ready evidence. |
| `headless-valid` | Scenario, simulation, metrics, and headless performance gates pass; oracle evidence is absent only when the report carries an explicit `no-oracle` coverage label. |
| `oracle-valid` | Headless gates pass and independent oracle evidence was exercised and valid. |
| `browser-observer-valid` | Headless gates pass and browser runtime/UI observer evidence was exercised and valid. |
| `research-ready` | Headless gates, oracle evidence, browser observer evidence, and zero-warning budget all pass. |
| `not-comparable` | Required deterministic identifiers are missing or headless performance budgets failed while economic scenario/simulation/metric gates remain independently explainable. |
| `failed` | A hard headless gate failed. |

The `researchReady` boolean is stricter than confidence. It is true only when the report can support research claims: valid headless economic evidence, valid oracle evidence, valid browser observer evidence, and no warnings in the warning budget.

Readiness metadata includes `oracleCoverageLabel`. Reports with exercised valid oracle evidence use `golden-oracle`; reports that intentionally skip oracle coverage must set `no-oracle`. A clean headless report with neither valid oracle evidence nor an explicit `no-oracle` label remains `exploratory`, so scenario classification cannot rise above exploratory by accident.

## Operator Runbook

Use this workflow when producing claim-oriented experiment evidence. Keep each output directory immutable after the run; rerun into a new directory when inputs, code, policy, oracle corpus, or browser evidence changes.

### 1. Exploratory Smoke

Use exploratory runs to confirm a scenario loads and emits artifacts. These runs can be useful for debugging, but they must not be cited as research-ready evidence.

```powershell
dotnet run --project src\CanDoItAll.Economy.Cli\CanDoItAll.Economy.Cli.csproj -- scenario run `
  --catalog src\CanDoItAll.Economy.SimulationSandbox\ScenarioPacks\EconomySimulationSandbox `
  --scenario multi-goods-elite `
  --output artifacts\economy\exploratory\multi-goods-elite `
  --clean `
  --no-oracle
```

Expected artifacts:

- `event-stream.json`
- `simulation.frames.json`
- `frame-hashes.json`
- `metrics-invariants.json`
- `warnings.json`
- `readiness-report.json`
- `run-summary.json`
- `headless-run-manifest.json`

Interpretation: `exploratory` means some evidence exists, but at least one policy, warning, oracle, artifact, or comparability gate prevents claim-ready use. Inspect `readiness-report.json` first, then `warnings.json` and `headless-run-manifest.json`.

### 2. Headless-Valid Economic Evidence

For a headless economic claim without oracle coverage, keep the explicit `--no-oracle` flag. A clean run can reach `headless-valid` only when scenario, simulation, metrics, and hard headless performance gates pass.

```powershell
dotnet run --project src\CanDoItAll.Economy.Cli\CanDoItAll.Economy.Cli.csproj -- scenario run `
  --catalog src\CanDoItAll.Economy.SimulationSandbox\ScenarioPacks\EconomySimulationSandbox `
  --scenario multi-goods-elite `
  --output artifacts\economy\headless\multi-goods-elite `
  --clean `
  --no-oracle `
  --json
```

Expected status: `headless-valid` with `researchReady: false`.

Failure interpretation:

- `failed`: a hard scenario, simulation, metric, or artifact gate failed. Treat it as an economic or input-pack defect until diagnostics say otherwise.
- `not-comparable`: deterministic identifiers are missing or a hard headless performance budget failed. Do not compare this run to another run, but do not rewrite it as an economic-model failure.
- `exploratory`: warnings, missing oracle label, non-research policy, or incomplete evidence capped the run.

### 3. Oracle-Valid Evidence

The CLI can record an oracle coverage label, but a label alone is not oracle evidence. Use `golden-oracle` only after an independent oracle runner or focused test has produced non-empty oracle evidence for the same scenario semantics.

```powershell
dotnet test tests\CanDoItAll.Economy.Tests\CanDoItAll.Economy.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~GoldenOracleSuite_ProvesKnownFinalStoresFlowsIssuesMetricsAndHashes"
```

Expected evidence: the golden oracle corpus covers final stores, flows, issues, relationships, metrics, invariants, and deterministic frame hashes. A broken oracle value must fail with a path-addressed diff. A readiness report reaches `oracle-valid` only when valid oracle evidence records are passed into `EconomyExperimentReadinessReporter`; setting `--oracle-coverage golden-oracle` without evidence leaves the oracle band unexercised.

### 4. Browser-Observer-Valid Evidence

Browser evidence is observer evidence. Capture it after headless and oracle gates pass, and store it beside the run evidence.

Expected browser artifacts:

- browser-loaded run-document hash
- expected run-document hash
- runtime idle/settled diagnostics
- final browser-exported object positions
- UI workflow assertions
- screenshot only as supporting evidence, never as the only evidence

Interpretation: browser document-hash mismatch, runtime non-idle, stale object positions, or UI assertion failure blocks `browser-observer-valid` and visual demo claims. It must not mutate `runHash`, `scenarioPackHash`, frame hashes, final stores, flows, metrics, or oracle results.

### 5. Research-Ready Closure

`research-ready` requires all of the following in the same evidence set:

- headless economic gates pass
- hard headless performance budgets are comparable
- oracle evidence is exercised and valid
- browser runtime/UI observer evidence is exercised and valid
- warning budget is zero
- manifests and cited evidence artifacts are non-empty and current

Run manifest comparison before making comparative claims:

```powershell
dotnet run --project src\CanDoItAll.Economy.Cli\CanDoItAll.Economy.Cli.csproj -- scenario manifest-diff `
  --baseline artifacts\economy\headless\baseline\headless-run-manifest.json `
  --candidate artifacts\economy\headless\candidate\headless-run-manifest.json `
  --json
```

Treat scenario, model, policy, oracle, artifact, or runtime categories in the diff as explicit review items. A design-matrix hash mismatch means the runs are not comparable unless the comparison intentionally declares a single-run exploratory design.

### Domain-Driver Guidance

Economy-specific visual semantics belong in `CanDoItAll.Economy.Simulation.WebGlBridge`, not in Components. Domain-owned actions such as Economy transfer, claim, contribution, fee, and relationship visuals should map through `IWebGlRunDomainMappingDriver` into generic WebGL action kinds before a run document reaches `WebGlRunLib`.

Components `WebGlLib` and `WebGlRunLib` may use generic primitives such as movement, pose, symbol, patch, wait, and directed-flow visualization. They must not learn Economy scenario, market, store, claim, fee, or oracle semantics. Put traceability into allowlisted `source.*` metadata and keep domain terms in the consuming bridge, scenario pack, oracle corpus, or operator documentation.

For a non-Economy simulator, create a domain-owned bridge/driver in that simulator's repository. Use the pass-through driver only when the upstream action vocabulary is already generic.

## Hard Gates And Warnings

The `gates` array reports machine-readable pass/fail/warning counts for:

- `headless-economic-correctness`
- `performance-budget`
- `oracle-economic-correctness`
- `browser-observer`
- `warning-budget`

The warning budget counts unique warning messages across readiness bands and reports whether each warning is allowlisted. A warning-only report can be useful for debugging or exploratory comparison, but it must not be marked `research-ready`.

## Performance Budgets

`EconomyExperimentPerformanceProfiles` defines `small`, `medium`, `large`, and `stress` profiles. Each profile carries named budgets for headless materialization, metric evaluation, snapshot build, snapshot serialization, artifact serialization, visual projection, browser load, and browser batch settle timing.

Headless budget failures are hard comparability failures. A report with valid `scenario`, `simulation`, and `metrics` bands but failed headless performance is marked `not-comparable`, not `failed`, so performance noise is not mistaken for an economic-model defect. The separate `performance-budget` gate records hard failure count, warning count, profile id, measurement count, and top bottlenecks.

Visual and browser budgets are observer/runtime warnings. They can block visual-evidence claims or lower confidence, but they do not mutate headless artifacts or rewrite economic validity bands.

## Experiment Policy

`SimulationExperimentPolicy.ResearchStrict` is the policy for readiness reports used to support research claims. It sets `mode` to `research-grade`, enables strict input-pack hash validation, uses a zero-warning budget, and fails unknown event handlers, insufficient stock, missing references, ambiguous stores, unknown metric kinds, and unknown invariant kinds.

`SimulationExperimentPolicy.ExploratoryDemo` remains available for demos and permissive exploration. Even when a demo run is otherwise clean, readiness reports mark it `exploratory` and `researchReady: false`.

`EconomyHeadlessExperimentRunner` defaults to `ResearchStrict`. Callers that intentionally need permissive behavior must pass `Policy = SimulationExperimentPolicy.ExploratoryDemo`; setting only the legacy mode is not enough for claim-oriented runner configuration.

## Metric And Invariant Registries

Research-grade runs use explicit metric and invariant kind registries. Unknown kinds are errors, not fallback defaults. Registered metric descriptors define default units, precision, and required metadata keys; registered invariant descriptors define required metadata keys and one-of key groups such as `min|max` for threshold bounds.

Every metric result includes:

- `kind`
- `unit`
- `precision`
- `sourceFrameHash`
- `evaluatorVersion`

Metric values are rounded with the descriptor precision using deterministic decimal rounding. Current oracle-covered metrics include resource totals, top-owner share, resource-owner HHI, average access cost, stock depletion rate, admin burden, and trade/transfer volume.

Strict invariant evaluation rejects underspecified known invariants before evaluating them. For example, `max-resource-share` requires `resourceId` and one of `maxShare` or `max`; resource conservation invariants require `resourceId` and `expectedTotal`.

## Behavior Expansion Profiles

Automatic behavior expansion is explicit in strict and research-grade runs. Scenario definitions must declare an expansion profile such as `none`, `simple-need`, `trade-policy-v1`, or `all-v1`; exploratory runs default to `all-v1` only for demo convenience.

Each profile has a descriptor with a profile id, version, rule-set id, rule fingerprints, and deterministic profile hash. The profile hash is included in:

- compiled event-stream metadata
- every explicit or derived event
- scenario manifests
- frame and delta metadata before deterministic hashes are calculated
- readiness-report metadata
- headless `frame-hashes.json` and `run-summary.json`

Derived events also carry `parentEventId`, `sourceEventId` when applicable, `phase`, `expansion`, and `expansionRuleHash`. Changing a profile id or profile descriptor changes the scenario definition hash and the downstream frame/run hash chain.

## Confidence Levels

| Level | Typical use |
| --- | --- |
| `L0-invalid-scenario` | Scenario or input pack is invalid. |
| `L1-scenario-loaded` | Scenario loaded, but simulation is invalid. |
| `L2-simulation-computed` | Simulation ran, but metrics or invariants are invalid. |
| `L3-metrics-projection-checkable` | Deterministic output exists, but projection/performance failed when required or semantic warnings remain. |
| `L4-headless-strict-economic-result` | Strict headless economic run has valid scenario, simulation, metrics, and hard headless budgets. Browser is optional. |
| `L5-browser-runtime-and-ui-verified` | L4 plus valid exercised browser runtime and UI bands. |

Any model-adjacent warning prevents L4/L5. Warning allowlists explain the warning budget, but warning-capped reports remain exploratory and are not research-ready.

## Strict Scenario Setup

For strict or research-grade runs:

- Set `experimentMode` to `strict` or `research-grade`.
- Set `expansionProfileId` explicitly, even when the value is `none`.
- Use store resolution policies when more than one store can match a source, target, shared pool, or effect.
- Use only registered metric and invariant kinds.
- Keep scenario packs closed by default: add every companion file to `requiredFiles` and `fileHashes`, or set `allowExtraFiles` only for exploratory packs.

`EconomySimulationScenarioManifestUpdater` can rewrite scenario manifests after adding or removing files so `contentHash`, `fileHashes`, and `packHash` stay synchronized.

## Store Resolution

Research-grade transfers must not depend on incidental `StoreId` ordering. When a source, target, shared pool, or effect can match more than one store, declare `StoreResolutionPolicies` with strategies such as `store-id-exact`, `actor-owned`, `location-owned`, `shared-pool`, or `market-pool`.

Every generated flow copies `storeResolution.<role>.*` metadata from the event. The metadata records the policy, selected store id, reason, candidate count, and resource/pool context for source and target roles. Rejected flows keep the same metadata and add `rejected: true` plus `requestedQuantity`.

Strict/research policy emits structured diagnostics for `ambiguous-store-resolution`, `capacity-rejection`, `insufficient-stock`, and `zero-accepted-transfer` so conservation and rejection behavior can be tested by an oracle.

## Golden Oracles

Golden oracle scenarios cover single transfer, insufficient stock, capacity rejection, shared resource depletion, trade plus fee, relationship updates, and rule violation/enforcement. Each oracle asserts final stores, flows, issues, relationships, metrics, invariants, and deterministic frame hash chains independent of WebGL projection or browser replay.

A deliberately broken expected value should produce a path-addressed diff with expected and actual values. The frame hash chain is replayed for stability so oracle evidence can detect both semantic drift and non-deterministic materialization.

## Headless Runner

Use `EconomyHeadlessExperimentRunner` when comparing economic outcomes without UI/WebGL. It emits:

- `event-stream.json`
- `simulation.frames.json`
- `frame-hashes.json`
- `metrics-invariants.json`
- `warnings.json`
- `readiness-report.json`
- `run-summary.json`
- `headless-run-manifest.json`

The preferred API is catalog based: call `RunScenario(IEconomySimulationScenarioCatalog, scenarioId, outputDirectory)` for one scenario or `RunBatch(IEconomySimulationScenarioCatalog, outputRootDirectory)` for every scenario in a catalog. `Run(EconomySimulationScenarioSource, ...)` is available for pathless or in-memory sources. `Run(string experimentJsonPath, ...)` is retained as a legacy path API and reports `scenarioSourceKind: legacy-path`.

The CLI mirrors the catalog-first contract:

- `economy scenario run --catalog <directory> --scenario <id> --output <directory> [--clean] [--no-oracle]`
- `economy scenario run --catalog <directory> --all --output <directory> [--clean] [--no-oracle]`
- `economy scenario run --experiment <experiment.json> --output <directory> [--clean] [--no-oracle]`

Each run writes a readiness status such as `exploratory`, `headless-valid`, or `research-ready` into the readiness report, run summary, reproducibility manifest, and CLI output. Batch runs also expose status and run-hash maps by scenario id. The run hash is deterministic for the same input pack and changes when economic inputs change.

`headless-run-manifest.json` uses schema `economy-headless-run/v4`. It records repository commit hashes, package versions for the simulation assemblies, scenario and catalog pack hashes, behavior profile id/version/hash, policy id, seed, frame hash chain, metric hashes, artifact hashes, approved volatile artifacts, and artifact citations. Artifact JSON files include their own `schemaVersion`; `readiness-report.json` also carries `artifactCitations` plus `artifactSetValid` metadata.

Use `EconomyHeadlessArtifactSetValidator.Validate(outputDirectory)` to verify that required artifacts exist and carry schema versions. Applying a failed validation to a readiness report marks the report `failed` and adds `artifact-set-incomplete` to the performance errors. Use `EconomyHeadlessRunManifestDiffTool.Compare(...)` to compare two run manifests; scenario/model/policy/artifact differences are categorized for audit review. `readiness-report.json` contains measured performance data, so its artifact hash is marked as approved volatile for deterministic manifest comparison.

## Experiment Design Harness

Use `EconomyExperimentDesignHarness` for repeated economic investigation runs that need explicit noise isolation. The harness consumes an `EconomyExperimentDesignMatrix` with:

- factor levels, such as treatment or parameter labels
- `seeds`
- `repetitions`
- `mode` set to `deterministic` or `stochastic`

The harness delegates every cell to `EconomyHeadlessExperimentRunner`, then emits `design-matrix-summary.json` with schema `economy-experiment-design-summary/v1`. The summary records the design matrix hash, per-run configuration hashes, run hashes, frame hash chains, metric values, metric means and variances, conclusion labels, and confidence labels.

In deterministic mode, repeated runs are grouped by scenario id, seed, and factor levels. `EvaluateDeterminism(...)` reports `non-determinism-detected` if any repeated group has different run hashes or frame hash chains.

In stochastic mode, the supplied seeds are recorded on the run records and included in configuration hashes. Conclusions are labeled `stochastic-estimate` with `variance-reported` confidence metadata. A deterministic backend can still produce zero variance; the summary preserves that as evidence rather than adding artificial randomness.

Use `EconomyExperimentDesignComparison.Compare(...)` before scenario comparisons. Comparisons require an explicit design matrix, or a single-run `single-run-exploratory` label, and matching design matrix hashes. A mismatch reports `design-matrix-hash-mismatch` so hidden configuration drift cannot be treated as an economic conclusion.

## Browser Observer Boundary

Browser/WebGL playback is observer evidence. It can support a visual demo claim, but it is not the economic source of truth. Economic truth comes from strict scenario loading, backend materialization, metrics/invariants, golden oracles when available, headless artifacts, and deterministic manifests.

Readiness reports keep this separation:

- clean headless runs with an explicit `no-oracle` label can reach `headless-valid` without browser evidence
- oracle-covered runs can reach `oracle-valid` without browser evidence
- only exercised and valid browser runtime/UI evidence can reach `browser-observer-valid`
- browser observer failures fail the `browser-observer` gate and block visual demo claims, but they do not mutate `runHash`, `scenarioPackHash`, or the validity of headless scenario/simulation/metrics bands

Use the Components observer comparison API to compare the expected run document with the browser-loaded document hash and runtime/UI diagnostics. A `browser-document-hash-mismatch` is visual evidence drift, not a reason to rewrite headless artifacts.

## Troubleshooting

If pause/play keeps moving after Pause, inspect the Components runtime idle diagnostics: active motions, queued motions, active stages, pending stages, and runtime stop count should settle to zero after `WaitForRuntimeIdleAsync`.

If a scenario pack fails catalog validation, check:

- missing `scenario.manifest.json`
- stale `packHash`
- stale per-file `fileHashes`
- undeclared extra files
- unsafe relative paths
- missing required input files

If a strict run is downgraded to L3, inspect semantic warnings in the scenario, simulation, and metrics bands first. Projection/runtime/UI warnings explain visualization readiness, but they should not be read as economic-model failures.
