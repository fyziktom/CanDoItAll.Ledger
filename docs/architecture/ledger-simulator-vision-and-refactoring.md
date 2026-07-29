# Project Finance Simulator Vision

Status: work in progress. The Simulator is experimental and its output is not
authoritative accounting, forecasting, tax, legal, compliance, economic, or
investment evidence.

## Product Intent

The Simulator is intended to help a project or company understand financial
possibilities before and during execution.

It has two complementary responsibilities:

1. **Planning:** model business units, accounts, business objects, assumptions,
   planned events, deterministic scenarios, interventions, projections, issues,
   and evidence hashes.
2. **Observation:** interpret confirmed Ledger transactions as actual project
   income, expenses, assets, liabilities, and settlements.

Planning and observation share project, business-unit, category, period, and
evidence identities. They do not share truth semantics:

- planned and simulated values are projections;
- accepted Ledger transactions are confirmed facts;
- financial views derived from confirmed facts are actual observations;
- reconciliation compares projections with actuals without rewriting either;
- calibration creates a reviewed assumption version rather than mutating a
  completed run.

## Intended Uses

- estimate project cash needs and expense timing;
- compare alternative execution scenarios;
- model business-unit and account relationships;
- test the effect of planned interventions;
- reconcile expected and confirmed activity;
- surface material variances, missing evidence, and assumption drift;
- support later financial analysis tools built on Ledger evidence.

## Current Capabilities

The current Simulator supports project workspaces, business units, accounts,
planned economic events, scenario materialization, run lifecycle, interventions,
projections, graphs, issue reporting, import/export, API services, and reusable
Razor components.

The implementation is deterministic and well covered by regression tests, but
important limitations remain:

- project workspaces, runs, and generated graphs are currently held in memory by
  the main host lifecycle;
- EF Core projection providers are registered but are not yet the authoritative
  lifecycle store;
- several Simulator primitives still resemble canonical Ledger concepts without
  using a completed observation adapter;
- sample account snapshots and rule evidence are projections, not confirmed
  Ledger facts;
- public Simulator contracts may change and are intentionally not published as
  NuGet packages.

## Architecture

| Surface | Responsibility |
| --- | --- |
| `CanDoItAll.Ledger.Simulator` | Projects, scenarios, assumptions, lifecycle, projections, reconciliation models, and APIs |
| `CanDoItAll.Ledger.Simulator.Components` | Project editing, run observation, charts, and reconciliation UI |
| `CanDoItAll.Ledger.Simulator.Persistence.EFCore` | Optional projection and graph persistence providers |
| `CanDoItAll.Ledger.Simulator.App` | Local composition, sample data, health, and interactive browser host |
| `CanDoItAll.Ledger.Simulation.Abstractions` | Generic simulation contracts |
| `CanDoItAll.Ledger.Simulation.Ledger` | Ledger snapshot projection into backend-neutral simulation frames |

```text
Simulator App -> Components + Simulator + Simulator.Persistence.EFCore
Components    -> Simulator
Persistence   -> Simulator

future Ledger observation adapter
  -> Simulator observation/reconciliation contracts
  -> canonical Ledger, BusinessObjects, and SDK contracts
```

The Simulator core must remain independent from its browser host, UI, and EF
implementation. A projection provider must not silently become a source of
confirmed Ledger truth.

## Roadmap

### 1. Stable planning contracts

Stabilize project, scenario, event, intervention, projection, and evidence
contracts with deterministic hashes and versioned import/export formats.

### 2. Durable Simulator lifecycle

Connect project, run, graph, and issue lifecycle operations to explicit
persistence ports. Define consistency and recovery behavior before treating the
EF providers as durable stores.

### 3. Ledger observation adapter

Map confirmed Ledger and BusinessObject records into Simulator actuals. Every
actual must retain its source transaction and evidence identity. The adapter
must prove compatibility for amounts, accounts, assets, categories, periods,
and business objects.

### 4. Planned-versus-actual analysis

Add reconciliation, variance classification, evidence completeness, and
reviewable calibration recommendations. Never replace confirmed transaction
history or silently edit completed scenarios.

### 5. Finance application surfaces

Build reusable analysis and reporting capabilities only after the truth,
persistence, and reconciliation contracts are stable. Domain-specific products
remain responsible for accounting policy, tax, compliance, and human review.

## Acceptance Principles

- A user can always distinguish projection, actual observation, and confirmed
  Ledger fact.
- Deterministic inputs produce deterministic outputs and evidence hashes.
- Every actual observation can be traced to confirmed Ledger evidence.
- Reconciliation creates links and variances, not rewritten history.
- Invalid storage or scenario configuration fails explicitly.
- Long-running work is server-owned and cancellation-safe.
- UI refreshes respond to lifecycle revisions rather than continuous full-model
  reconstruction.
- No public Simulator package is produced until compatibility and support
  expectations are documented.
