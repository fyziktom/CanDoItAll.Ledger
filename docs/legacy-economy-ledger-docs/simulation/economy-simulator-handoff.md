# Economy Simulator Handoff

The Economy simulator is a bounded scenario runtime for deterministic scenario packs, driver-backed transitions, stress campaigns, and browser observer evidence. It is not a universal economic oracle, a real-world causal engine, or a substitute for legal, accounting, securities, tax, regulatory, or investment judgment.

## Run Scenarios

Use the product tools from the repository root:

```powershell
dotnet run --no-build -c Release --project tools\CanDoItAll.Economy.ExperimentInputPackTools -- scenario-probe --suite baseline --output artifacts\economy\simulator\scenario-suite\baseline
```

The baseline suite reads product manifests from `src/CanDoItAll.Economy.SimulationSandbox/ScenarioSuites/`, writes matrix artifacts under the requested output directory, and reports expected run status, failure classification, claim status, research-readiness labels, matrix hashes, and per-row artifacts.

For an end-to-end headless regression export:

```powershell
dotnet run --no-build -c Release --project tools\CanDoItAll.Economy.ExperimentInputPackTools -- simulator-headless-regression --output artifacts\economy\simulator\headless-regression
```

## Add Scenario Packs

Add a closed scenario folder under `src/CanDoItAll.Economy.SimulationSandbox/ScenarioPacks/EconomySimulationSandbox/<scenario-id>/` with the scenario definition, placement, parameters, run plan, expected invariants when applicable, visual mapping when browser evidence is expected, and `scenario.manifest.json`.

Register the scenario through the source manifest and probe-suite manifest rather than adding scenario-name branches to generic runtime code. The generic runtime should see source ids, family ids, probe kinds, driver ids, and manifest metadata; domain vocabulary belongs in the scenario pack, fixtures, tests, or an explicit extension.

Before relying on a new pack, update its manifest hashes and run focused tests for strict loading, source-manifest resolution, invariant coverage, expected failure classification, and browser mapping when the pack has a visual surface.

## Add Drivers

Implement `IEconomicSimulationDriver` in the smallest package that matches the driver scope. Register it with `EconomicSimulationDriverRegistration`, including `DriverKind`, `Version`, `SupportedScenarioFamilies`, `RequiredParameterIds`, and `DefaultEvidenceOracleIds`.

Driver behavior should be parameterized through typed inputs, rule/effect policies, and registered extensions. Do not add `if scenarioId == ...` branches to generic execution, transition, visualization, or WebGL code. Add non-happy-path tests for unsupported driver ids, missing required parameters, hidden source/sink creation, failure-owner classification, and vocabulary leakage.

## Evaluate Tragedy And Collapse Trajectories

Run the default stress campaign:

```powershell
dotnet run --no-build -c Release --project tools\CanDoItAll.Economy.ExperimentInputPackTools -- stress-campaign --output artifacts\economy\simulator\stress-campaign\default
```

The campaign manifest is in `src/CanDoItAll.Economy.SimulationSandbox/StressCampaigns/default-stress-campaign.json`. It declares the required surfaces, label rules, expected terminal outcomes, and minimum trajectory artifact requirements.

Inspect `tragedy-observation-summary.json`, `tragedy-observation-summary.md`, per-case time-series JSON, and event exports. The important signals are time to warning, time to collapse, time to recovery, recovery duration, first policy block step, minimum pool health, maximum concentration, unmet-demand area, event counts by kind, and policy decisions per step.

Treat collapse labels as scenario-runtime observations, not universal economic laws. A collapse claim needs the exact campaign manifest, parameter vector, seeds, trajectory artifacts, and evidence of how scarcity, concentration, policy blocking, recovery, or sustainable equilibrium emerged over time.

## Browser Observer Evidence

The simulator route is `/economy/simulation-sandbox`. Browser observer evidence for that route is large-screen-only and should use `1440x1100` or larger. A complete observer capture needs live WebGL runtime diagnostics, positive GLB request coverage, zero failed GLB requests, zero fallback objects unless explicitly explained, screenshots across required phases, runtime snapshots, and browser artifact hashes.

Browser evidence can validate projection and runtime rendering. It cannot close economic claims by itself, and it must not mutate headless run hashes, scenario pack hashes, frame hashes, final stores, flows, metrics, or oracle results.

## Out Of Scope

The simulator does not infer real-world causal validity from a scenario pack. It does not provide financial, legal, regulatory, accounting, tax, or securities advice. It does not make screenshots into economic claims. It does not allow workflow-specific assumptions to become global simulator behavior without a driver, manifest, tests, and evidence-owned lifecycle.
