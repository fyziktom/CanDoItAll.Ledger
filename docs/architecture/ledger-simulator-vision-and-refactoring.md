# Ledger Simulator Vision And Refactoring Record

Status: active architecture record for the work-in-progress, experimental
BO/project Simulator owned by this repository. It is not production-ready and
its output is not authoritative financial, accounting, forecasting, or economic
evidence.

## Product intent

The Ledger simulator has two related jobs:

1. Model a project or company before and during execution. A simulation project owns business units, accounts, business objects, assumptions, planned economic events, deterministic run setup, scenario interventions, projections, issues, and evidence hashes.
2. Grow into the observation layer for real CanDoItAll projects. Confirmed Ledger transactions and business-object evidence become actual income, expense, asset, liability, and settlement observations. Those actuals reconcile against planned events and can produce explicit, reviewable calibration recommendations for later runs.

The planning and observation lanes must share project, business-unit, category, period, and evidence identities, but they must not share truth semantics:

- simulated and planned values are projections;
- confirmed Ledger facts are actuals;
- reconciliation links the two without rewriting either history;
- calibration is a new reviewed assumption version, never a silent mutation of a completed run;
- deterministic inputs, setup hashes, output hashes, and source evidence remain inspectable;
- financial, accounting, tax, legal, and regulatory review remains outside the simulator's authority.

This repository owns the BO/project simulator. It must not absorb the separate Economy simple-account runtime, Smith-style experimental economics, scenario-pack laboratory, or WebGL observer evidence.

## Current architecture evidence

| Surface | Current owner | Current responsibility | Architectural result |
| --- | --- | --- | --- |
| Project/scenario domain and deterministic services | `src/CanDoItAll.Ledger.Simulator` | Projects, BUs, events, scenario compilation, run lifecycle, projections, actual validation, reconciliation inputs, API services | Correct physical and `CanDoItAll.Ledger.Simulator` identity owner. |
| Reusable simulator UI | `src/CanDoItAll.Ledger.Simulator.Components` | Project editor, run console, graphs, reconciliation and actual-entry components | Correct UI boundary; sample data is still coupled to the component package. |
| Projection persistence | `src/CanDoItAll.Ledger.Simulator.Persistence.EFCore` | Optional EF graph projection/cache adapters using `IDbContextFactory` | Correct infrastructure direction, but no lifecycle path writes through the adapters; projects, runs, and generated graphs remain in memory. |
| Browser host | `examples/CanDoItAll.Ledger.Simulator.App` | Composition, local provider choice, sample seeding, Razor/interactive-server pipeline | Pre-repair it used profile-scoped Data Protection, Windows EventLog, a missing `/Error`, and stale launch docs; this repair makes those host concerns explicit. |
| Generic ledger-backed snapshot adapter | `src/CanDoItAll.Ledger.Simulation.Ledger` | Projects already-built ledger snapshots into backend-neutral frames | Separated correctly, but it is not currently composed into the BO/project simulator. |

Important limitation: `CanDoItAll.Ledger.Simulator.csproj` currently references only `Microsoft.AspNetCore.App`. `SimulatorPrimitives.cs` duplicates names and concepts that also exist in Ledger/Core, and `IEconomicPostingService` is not used by a production path. The current runtime is therefore a deterministic BO/project simulator and future observation foundation, not yet a real Ledger-backed observation implementation. Its current account snapshots and rule evidence are sample projections, not confirmed Ledger facts, and must not be presented as actuals. Canonical Ledger contract adoption requires a compatibility/migration slice with golden serialization proof; it must not be hidden inside this startup repair.

CodeAnalytics snapshot `snap-20260718213913-5e195798-failed` was attempted with the six simulator projects. Its backend could not resolve the pinned .NET SDK and loaded zero projects/types, so no negative conclusion is based on that snapshot. Manual project/source inspection and successful SDK 10.0.300 builds are the evidence for this record.

## Responsibility inventory

| Source | Member or area | Responsibilities today | Target owner | Test seam | Risk |
| --- | --- | --- | --- | --- | --- |
| `SimulationRunLifecycleServices.cs` | `InMemorySimulationRunLifecycleService` (about 2,060 lines before this repair) | In-memory store, lifecycle transitions, live background stepping, scenario compilation, intervention policy, result materialization, graph/KPI/source-map construction, presentation state | Keep lifecycle/store here; extract scenario compilation, interventions, result hashing, and scenario KPI values to one required top-level materializer | Instantiate the materializer directly; lifecycle regression tests prove delegation | High |
| `Program.cs` | storage-mode branch | Reads raw keys and chooses EF provider; duplicates the UI's storage-mode interpretation | Persistence composition extension plus immutable resolved selection | Register from in-memory configuration; assert provider and invalid-mode behavior | Medium |
| `Program.cs` | default host infrastructure | Razor pipeline relies on an unwritable user-profile key ring and default Windows EventLog; production handler targets missing `/Error` | App host composition and configuration | Real root-page/health smoke plus direct option/registration tests where practical | High |
| `RunObservation.razor` | view-model getter and one-second refresh loop | Rebuilds the complete observation query during rendering and rerenders every second while any seeded run says `Running`, even when no lifecycle value changed | Stable page snapshot plus lightweight lifecycle-revision observation | Browser interaction remains responsive across refresh intervals; changed run revisions replace the snapshot | High |
| `SimulatorPrimitives.cs` | Ledger-like primitives | Local `Address`, `Amount`, token/transaction/account-rule shapes duplicate canonical Ledger/Core concepts | Future Contracts/Adapter migration, not this repair | Golden JSON/hash compatibility and adapter contract tests | Critical follow-up |
| `LedgerSimulatorSampleData.cs` | sample project/run builders | Reusable UI defaults, app bootstrap, and test fixtures | Future Samples/Fixtures project or app-owned seed provider | Components receive explicit models; fixtures test seed determinism | Medium follow-up |

## Architecture decision for this repair

### Isolated responsibilities

- Scenario materialization is isolated from the in-memory lifecycle/store. The lifecycle decides *when* a step changes; the scenario materializer compiles events and decides *what deterministic scenario result* that step produces.
- Persistence-provider selection is isolated from the browser host. The persistence project owns validation and registration of its EF implementations; the host owns only composition.
- Local interactive-server infrastructure is made explicit in the host: writable Data Protection storage, non-privileged logging, health reporting, and a real error route.
- A registered automatic run is server-owned: cancellation of the HTTP request cannot strand it in `Running`, and two starts in the same timestamp second cannot overwrite one another.

### Dependency direction

```text
Simulator App -> Components + Simulator + Simulator.Persistence.EFCore
Components    -> Simulator
Persistence   -> Simulator
Materializers -> Simulator contracts/services

future Ledger observation adapter -> Simulator materialization/observation contracts
                                  + canonical Ledger/BusinessObjects/SDK
```

The simulator core must not reference the host, UI, or EF implementation. No new project reference or partial class is required by this repair.

### Pattern selection record: scenario materialization

- Observed force: the lifecycle embeds a cohesive compiled-scenario algorithm—scenario lookup, intervention application, event compilation, projection, hashes, and KPI values—that changes for reasons different from lifecycle state transitions.
- Rejected simpler option: another private method or partial file leaves the lifecycle responsible for the algorithm and does not create a direct test seam.
- Selected pattern: Extract Service behind the required top-level `ISimulationScenarioRunMaterializer` port. Scenario projects must carry a nonblank `scenarioCode`; invalid projects fail validation instead of silently using the generic sample projection.
- Rejected patterns: a service locator, a new partial class, a facade that retains the existing logic, or a speculative multi-strategy resolver before a real Ledger observation materializer exists.
- Test seam: instantiate `LedgerScenarioRunMaterializer` without
  `InMemorySimulationRunLifecycleService`; retain lifecycle scenario/run
  regressions and DI validation as composition proof.
- Migration: move one cohesive materialization slice, delegate production calls, delete the moved lifecycle logic, and keep serialized property names, ordering, and deterministic hashes unchanged. Generalize the port into strict multi-strategy resolution only when the real Ledger observation implementation supplies a second algorithm.

### Pattern selection record: persistence selection

- Observed force: InMemory and PostgreSQL are composition-time infrastructure choices already known by the host.
- Selected design: a validated composition extension and immutable resolved selection. No runtime Strategy or factory is justified.
- Negative proof: an unknown mode and PostgreSQL without a connection string fail fast instead of silently falling back to in-memory state.

## Acceptance criteria

- The documented launch command uses the current Ledger project path and works with persistent build servers disabled when necessary.
- The app's root page and liveness-only `/health` endpoint respond successfully in a constrained local process without access to the user-profile Data Protection directory or Windows EventLog.
- A production exception has a real `/Error` endpoint.
- Projection-provider mode has one normalized source; unknown values do not silently select the in-memory provider. The run lifecycle remains explicitly in memory until a later write-through slice connects it to that provider.
- Initial server rendering materializes only the active simulator tab. The measured root response fell from about 2.80 MB to 47 KB while navigation coverage continued to exercise every project and observation tab.
- The interactive run-observation page keeps a stable view model and rerenders only when the lifecycle registry revision changes; an unchanged seeded `Running` record does not trigger continuous full-page rendering.
- No new partial class, nested architecture type, broad helper/manager, service location, or project-reference cycle is introduced.
- The selected materialization behavior is owned by top-level types and directly tested without constructing the lifecycle service.
- Same-second run starts produce distinct registered histories, and request cancellation after a successful start does not abandon the server-owned run.
- Existing deterministic scenario schedules, hashes, projections, lifecycle behavior, APIs, and components remain covered by the focused simulator suite.
- The final browser smoke exercises project listing, run navigation, and at least one API/health request; the repaired simulator remains running for manual exploration.

## Explicit follow-up boundary

The next architecture slice should define canonical observation contracts and adapters between confirmed Ledger/BusinessObject records and simulator actual/reconciliation models. It must inventory every duplicated primitive, define compatibility conversions, preserve stored manifests and hashes, and add ledger-confirmation-to-actual-entry integration tests before removing local compatibility types. That work is intentionally separate from this host and materialization repair.
