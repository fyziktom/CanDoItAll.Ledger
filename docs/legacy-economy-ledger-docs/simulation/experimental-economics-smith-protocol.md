# Smith Experimental Economics Protocol

`CanDoItAll.Economy.Simulation.ExperimentalEconomics` models a bounded laboratory-style market experiment. The target is not a bigger visual demo; it is an auditable protocol where preferences, institutions, subject information, repeated sessions, payoff salience, and outcome metrics are explicit.

## Readiness

As of 2026-06-10, the Smith layer supports bounded, deterministic, headless laboratory-style mechanism experiments. It supports stable induced-value assignment, multi-unit value and cost schedules, environment/institution/outcome separation, repeated sessions, payoff-journal hashes, analytic surplus metrics, treatment comparison reports, and subject-policy baselines that generate institution messages through the same mechanism engine.

The browser observer surface is deliberately weaker than the economic evidence surface. `/economy/simulation-sandbox/smith-observer` can show public projection, redaction, route availability, rendered WebGL frame hashing, nonblank canvas rendering, page-error capture, fallback diagnostics, GLB model diagnostics, and pause cleanup. It cannot close economic correctness without the headless protocol, session, payoff, metric, treatment, and campaign artifacts.

## Core Separation

- Environment: goods, private buyer values, seller costs, budgets, information sets, competitive-equilibrium prediction, and maximum surplus.
- Institution: message space, matching, price, allocation, settlement, disclosure, and period rules.
- Outcome: allocations, trade prices, payoffs, efficiency, surplus, convergence, volatility, budget balance, and rejection reasons.

The same environment can run under multiple institutions, and the same institution can run over multiple environments. Institution hashes must not embed private value-card ids.

## Induced Values And Payoffs

An `InducedValueProtocol` owns subject value cards, seller cost cards, buyer schedules, payoff redemption, privacy, salience, dominance, nonsatiation, and budget gates. Subject observations expose only the requesting subject's private cards plus public summaries. Payoff journals are separate from accounting evidence: accounting validates conservation; the subject payoff journal validates induced rewards.

Required gates:

- buyer payoff is private value minus price;
- seller payoff is price minus private cost;
- budget checks reject spending beyond the assigned budget unless credit is explicitly allowed;
- salience checks reject decisions that change allocation without changing the declared payoff journal;
- dominance checks reject protocols where modeled noise or participation cost can dominate controlled payoff differences.

## Institutions And Campaigns

`IMarketInstitutionMechanism` implementations provide reusable mechanisms for continuous double auction, posted offer, first-price sealed bid, second-price/Vickrey, uniform price, and price-ceiling rationing. Each mechanism emits a message log, trade log, order-book snapshot, period summary, budget-balance report, and mechanism-performance report.

Campaign evidence uses `MechanismComparisonCampaign` with independent session replications. Treatment comparisons must hold the environment and induced-value protocol fixed unless a factorial design justification is explicit. Single-run anecdotes do not close mechanism-comparison claims.

## WebGL Observer Boundary

`Simulation.WebGlBridge` can project Smith observations into visual frames showing the session room, roles, public market board, payoff-journal summary, treatment block, and convergence state. These frames must redact private value-card and cost-card ids. Browser screenshots, GLB requests, or observer frames validate visualization only; Smith economic claims must cite headless protocol, session, mechanism, payoff, metric, and campaign artifacts.

The current Node live observer route is:

```http
GET /economy/simulation-sandbox/smith-observer
```

The route renders a `WebGlSceneView` from public Smith observer frames and exposes hidden machine-readable JSON for browser automation. The evidence surfaces include the public observer JSON, rendered WebGL run JSON, runtime diagnostics, runtime snapshot, fallback report, page-error log, canvas screenshot, and a private-leak verdict.

## Evidence Expectations

Critical evidence should include:

- protocol and assignment hashes;
- microeconomic system environment/institution/outcome hashes;
- negative privacy, budget, salience, and confounded-treatment tests;
- positive surplus, payoff-journal, session-replication, and mechanism-comparison tests;
- package-boundary evidence that the Smith core does not reference WebGL, Components, UI, Node, EF, ledger persistence, BusinessObjects, SDK, or the main Economy ledger;
- final red-team evidence that stale browser evidence, local-only artifacts, or observer-only evidence cannot close the economic evidence boundary.

## Rerun Commands

Focused Smith tests:

```powershell
dotnet test tests\CanDoItAll.Economy.Tests\CanDoItAll.Economy.Tests.csproj --no-restore /m:1 --filter "FullyQualifiedName~Smith"
```

Headless Smith package build:

```powershell
dotnet build src\CanDoItAll.Economy.Simulation.ExperimentalEconomics\CanDoItAll.Economy.Simulation.ExperimentalEconomics.csproj --no-restore /m:1
```

Boundary audit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\audit-smith-no-ledger-boundary.ps1 -RunNegativeFixture
```

Live observer route:

```powershell
dotnet run --project src\CanDoItAll.Economy.Node\CanDoItAll.Economy.Node.csproj --no-build --no-launch-profile --urls http://127.0.0.1:56439
```

Open `http://127.0.0.1:56439/economy/simulation-sandbox/smith-observer` with Playwright or a browser evidence capture tool.

Scenario-probe matrices:

```powershell
dotnet run --project tools\CanDoItAll.Economy.ExperimentInputPackTools -- scenario-probe --suite baseline --output artifacts\economy\simulator\scenario-suite\baseline
```

## Current Limitations

- This is not human-subject IRB, legal, accounting, securities, tax, or regulatory validation.
- Results are bounded model claims, not real-world causal claims.
- The package does not establish universal equilibrium existence or convergence.
- Strategic learning is limited to the implemented subject policies; deeper learning claims require additional policy memory and evidence.
- Browser observer evidence validates public projection, live WebGL rendering, diagnostics, redaction, and cleanup only. Economic claims require headless artifacts.
