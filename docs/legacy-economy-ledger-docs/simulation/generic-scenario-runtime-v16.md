# Generic Scenario Runtime v16

The Economy simulator is a bounded simulation and experimental-economics framework. It is not a universal economic oracle, a real-world causal engine, or legal, accounting, securities, tax, regulatory, or investment authority. Claims require declared scenario inputs, headless runs, invariant/oracle evidence, reproducibility hashes, and, when visualization is cited, separate browser/WebGL observer evidence.

## Runtime Boundary Map

| Layer | Responsibility | Must not do |
| --- | --- | --- |
| `Simulation.Abstractions` | Backend-neutral scenarios, events, frames, deltas, hashes, experiment packs, metrics, and invariants. | Reference UI, Components, WebGL, ledger persistence, Node, or scenario-specific vocabulary. |
| `Simulation.SimpleAccounts` | In-memory transition engine, generic event handlers, rule/effect policies, and autonomous drivers registered through `IEconomicSimulationDriverRegistry`. | Resolve behavior by concrete scenario pack names or depend on host catalogs. |
| `SimulationSandbox` | Host-facing scenario catalogs, probe matrices, source-manifest lookup, real scenario-pack execution, oracle aggregation, artifact export, and WebGL projection orchestration. | Treat visualization rows as economic evidence or bypass source manifests with hardcoded pack paths. |
| `Simulation.ExperimentalEconomics` | Smith-style laboratory contracts, institutions, induced-value protocols, sessions, payoff journals, and bounded mechanism comparisons. | Carry memory-economy attention mechanisms in the default Smith or generic market registry. |
| `Simulation.MemoryEconomy` | Explicit cognitive-memory economy extension for attention-token allocation mechanisms and their probe/oracle providers. | Become part of the default generic simulator or Smith core. |
| `Simulation.Visualization` | Economy visual DTOs and action mappings that remain independent of Components and WebGL types. | Embed Components runtime concerns. |
| `Simulation.WebGlBridge` | Economy-owned mapping from visual frames/actions to generic Components WebGL scene/run documents, pose GLB variants, symbols, and diagnostics. | Push Economy scenario semantics into Components packages. |
| Basic scenario-pack catalog | Simulator-owned scenario packs, source manifests, visual mappings, and scenario manifests. | Require a browser host or ledger-backed package to run headless scenarios. |
| Browser host adapter | Optional route host for `/economy/simulation-sandbox` and runtime static assets. | Own scenario-pack content, source ids, or basic simulator backend semantics. |

The v16 default probe matrix covers shared-resource, finite productive-capacity, market/exchange, Smith lab, scenario-pack, bridge, and WebGL observer rows. WebGL observer rows validate projection and runtime rendering only; scenario, simulation, metric, and oracle rows validate economic behavior.

## Scenario Packs

Add a new scenario pack without touching generic runtime code:

1. Create a closed scenario folder under `src/CanDoItAll.Economy.SimulationSandbox/ScenarioPacks/EconomySimulationSandbox/<scenario-id>/`.
2. Include `experiment.json`, `scenario.definition.json`, `placement.json`, `parameters.json`, `run.plan.json`, `expected.invariants.json` when a pack-level invariant contract exists, `visual.mapping.json` when browser evidence is expected, and `scenario.manifest.json`.
3. Keep domain vocabulary in the pack files, tests, fixtures, or an explicit domain extension. Generic code should see only source ids, family ids, probe kinds, and driver ids.
4. Add or update a source entry in `scenario-probe-sources.json`, for example `scenario-source.my-pack` to `<scenario-id>`. Probe runners resolve the concrete pack through the source manifest.
5. Use `EconomySimulationScenarioManifestUpdater` or the existing manifest update tooling so `requiredFiles`, `fileHashes`, and `packHash` match the closed pack.
6. Add a probe row through an `IScenarioProbeProvider` or a host-owned default provider. Use `ScenarioPack(rowId, familyId, sourceId)` for pack execution, not scenario-name branches inside `EconomyScenarioProbeExecutor`.
7. Add focused tests for strict pack loading, manifest closure, invariant expectations, source-manifest resolution, and expected failure classification.

A scenario pack can fail for pack-data reasons, bridge-mapping reasons, generic transition reasons, or visualization reasons. The probe row must classify the owner instead of forcing a one-off success path.

## Domain Drivers

Add a new domain driver without leaking terms into generic layers:

1. Implement `IEconomicSimulationDriver` in a driver package that matches the intended scope. If the driver is domain-specific, use an explicit domain extension or a deliberately scoped driver folder with vocabulary audit allowances and reasons.
2. Model inputs as typed or named parameters. Prefer neutral resource/actor/source/sink parameters when the behavior can be generic.
3. Register the driver through `EconomicSimulationDriverRegistration` and an `IEconomicSimulationDriverRegistry`. Default generic drivers live in `EconomicSimulationDriverRegistryDefaults`; app-specific drivers can be injected with `CreateDefaultWith(...)`.
4. Fill `DriverKind`, `Version`, `SupportedScenarioFamilies`, `RequiredParameterIds`, and `DefaultEvidenceOracleIds` so the row is auditable before execution.
5. Add an oracle provider or invariant report coverage for any economic claim the driver is meant to close.
6. Add non-happy-path tests for unsupported driver ids, missing required parameters, hidden source/sink creation, failure owner classification, and vocabulary leakage.

Do not add `if scenarioId == ...` branches in generic execution, transition, visualization, or WebGL code. If behavior depends on a scenario, the dependency belongs in scenario data, driver parameters, rule/effect policies, bridge mapping, or a registered extension.

## Smith And Memory-Economy Extensions

Smith experiments remain a bounded lab layer. They can model mechanisms, induced values, subject-private information, sessions, payoff journals, and treatment comparisons. They do not validate browser rendering, and their browser observer route cannot close economic claims by itself.

Memory-economy attention allocation is separate from the internal generic simulator commons and the external Agent/Workflow commons. Attention-token rules belong to `Simulation.MemoryEconomy` and explicit extension providers. They are not default generic market rules, and they must carry their own owner, scope, expected benefit, maintenance cost, enforcement cost, validation cost, complexity delta, simplification path, and retirement condition before becoming durable policy.

## GLB Pose And Symbol Mapping

Economy-side visual mapping converts scenario events to generic action kinds, pose keys, symbol categories, and anchors:

- `actionMappings` select action kind, pose key, symbol category, and anchor alias for an event kind.
- `poseHints` and `rendererBinding.poseAssetIds` can pin a pose key to a GLB asset id such as `asset.actor.pose.walking`.
- `rendererBinding.poseVariantIds` can pin a pose key to a concrete model variant such as `asset.actor.pose.walking.variant.model-high`.
- `symbolMappings` and `rendererBinding.symbolAssetIds` map overhead/status symbols to generic symbol GLBs.
- `fallbacks.allowNoOpPoseFallback` and `fallbacks.allowNoOpSymbolFallback` should stay `false` for strict browser evidence.

The Economy WebGL bridge resolves pose keys through `EconomyVisualPoseAssetResolver`, symbol categories through `EconomyVisualSymbolAssetResolver`, and GLB variants from `EconomyWebGlAssetCatalogFactory`. Components receive generic scene/run documents and `source.*` metadata. Components must not need to understand scenario packs, economic claims, or domain-specific resources.

Browser evidence must show loaded GLB diagnostics, positive GLB request counts, zero failed GLB requests, zero fallback objects, screenshots, and runtime snapshots. A primitive-only profile or screenshot-only evidence is not enough for v16 visualization closure.

## Large-Screen-Only Evidence

The Economy simulator sandbox is a large-screen exploratory surface. Current evidence for `/economy/simulation-sandbox` must use the v18 large-screen policy of `1440x1100` or larger. Small, medium, narrow, and mobile screenshots are out of scope for this route and should be rejected by `EconomySimulatorViewportPolicy`.

This is a local product rule, not a global UI rule. Other Economy or Components surfaces can make their own responsive commitments.

## Minimum Evidence For Changes

For scenario-runtime changes, capture:

- Release build transcript.
- Focused tests for the touched runtime, pack, vocabulary, WebGL, and oracle surfaces.
- Scenario matrix artifact pack with matrix JSON/CSV, failures, terminal distribution, cycle stagnation, performance, before/after comparison, and per-scenario artifacts.
- Source assertions proving registry/source-manifest dispatch instead of hardcoded scenario behavior.
- Anti-stub/red-team audit for hardcoded scenario ids, split-string vocabulary bypasses, WebGL evidence without GLB, stale `--no-build` evidence, and hidden ledger dependencies.
- Changed-file hashes for code, docs, evidence runners, and validation status files.
