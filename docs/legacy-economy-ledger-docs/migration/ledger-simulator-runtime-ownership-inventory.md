# Historical Ledger Simulator Runtime Ownership Inventory

Status: completed ownership landing and Ledger namespace migration.

This Ledger copy mirrors the Economy inventory for the v27 split. It records the
actual post-landing BO/project simulator runtime ownership in this repo.

## Ledger Landing Decision

SB02 created these physical Ledger projects:

- `src/CanDoItAll.Ledger.Simulator`
- `src/CanDoItAll.Ledger.Simulator.Components`
- `src/CanDoItAll.Ledger.Simulator.Persistence.EFCore`
- `examples/CanDoItAll.Ledger.Simulator.App`

The first landing pass kept `CanDoItAll.Economy.Simulator*` assembly and
namespace identities temporarily. Those identities are now retired; projects,
assemblies, namespaces, types, tests, and the browser host use
`CanDoItAll.Ledger.Simulator*`.

The Simulator remains work in progress and experimental. It is not
production-ready and must not be used as authoritative financial, accounting,
forecasting, or economic evidence.

## What Moves From Economy

| Economy source | Ledger target | Action | Reason |
|---|---|---|---|
| `src/CanDoItAll.Economy.Simulator/CanDoItAll.Economy.Simulator.csproj` | `src/CanDoItAll.Ledger.Simulator/CanDoItAll.Ledger.Simulator.csproj` | MoveToLedger | Old BO/project simulator contracts and services. |
| `src/CanDoItAll.Economy.Simulator/*.cs` | `src/CanDoItAll.Ledger.Simulator/*.cs` | MoveToLedger | Contains BO/project workspace, account addresses, tokens, run lifecycle, interventions, CFO/business projections, and `/api/simulator` route services. |
| `src/CanDoItAll.Economy.Simulator.Components/**` | `src/CanDoItAll.Ledger.Simulator.Components/**` | MoveToLedger | Old BO/project simulator UI. |
| `src/CanDoItAll.Economy.Simulator.Persistence.EFCore/**` | `src/CanDoItAll.Ledger.Simulator.Persistence.EFCore/**` | MoveToLedger | EF persistence for old simulator project/run/graph projections. |
| `examples/CanDoItAll.Economy.Simulator.App/**` | `examples/CanDoItAll.Ledger.Simulator.App/**` | MoveToLedger | Browser host for old BO/project simulator. |

## Tests To Import

| Economy test path | Ledger target | Reason |
|---|---|---|
| `tests/CanDoItAll.Economy.Tests/EconomySimulatorTests.cs` | `tests/CanDoItAll.Ledger.Tests/LedgerSimulatorTests.cs` | BO/project simulator golden, projection, and persistence tests. |
| `tests/CanDoItAll.Economy.Tests/EconomySimulatorComponentTests.cs` | `tests/CanDoItAll.Ledger.Tests/LedgerSimulatorComponentTests.cs` | Simulator component tests. |
| `tests/CanDoItAll.Economy.Tests/EconomySimulatorRuleAutomationTests.cs` | `tests/CanDoItAll.Ledger.Tests/LedgerSimulatorRuleAutomationTests.cs` | BO rule-token/account-rule automation tests. |
| `tests/CanDoItAll.Economy.Tests/SimulationApiEndpointTests.cs` | `tests/CanDoItAll.Ledger.Tests/SimulationApiEndpointTests.cs` | `/api/simulator` route tests; Economy copy deleted in SB03. |
| `tests/CanDoItAll.Economy.Tests/SimulationProjectManagementTests.cs` | `tests/CanDoItAll.Ledger.Tests/SimulationProjectManagementTests.cs` | BO project workspace, import/export, and run lifecycle tests; Economy copy deleted in SB03. |

## What Must Not Move

| Economy path | Target owner | Reason |
|---|---|---|
| `src/CanDoItAll.Economy.Simulation.SimpleAccounts/**` | Economy | Simple account runtime and drivers are the new Economy kernel target. |
| `src/CanDoItAll.Economy.Simulation.ExperimentalEconomics/**` | Economy | Smith-like experiments stay in Economy. |
| `src/CanDoItAll.Economy.SimulationSandbox/**` | Economy | Scenario packs, evidence, replay, and probe tooling stay in Economy. |
| `src/CanDoItAll.Economy.Simulation.Visualization/**` | Economy | Observer DTOs stay in Economy. |
| `src/CanDoItAll.Economy.Simulation.WebGlBridge/**` | Economy | WebGL observer proof is not Ledger economic proof. |
| `tools/CanDoItAll.Economy.ExperimentInputPackTools/**` | Economy | Scenario-pack tooling stays in Economy. |

## Existing Ledger Owners

| Ledger path | Action | Role |
|---|---|---|
| `src/CanDoItAll.Ledger.Core/**` | Keep/extend | Ledger transaction, address, token, rule, and BO primitives. |
| `src/CanDoItAll.Ledger.BusinessObjects/**` | Keep/extend | BO transitions and account behavior. |
| `src/CanDoItAll.Ledger.Components/**` | Keep/extend | Ledger/BO UI components. |
| `src/CanDoItAll.Ledger.Simulation.Ledger/**` | Keep/extend | Ledger-backed simulation adapter; must not import simple accounts or Smith. |
| `docs/architecture/repository-boundaries.md` | Update | Must distinguish Ledger BO simulator ownership from Economy simple/Smith runtime ownership. |

## Governance And Boundary Notes

- Scarce resources are provenance trust, build/test time, maintainer review, and
  future route ownership clarity.
- Ledger receives the BO/project simulator only. It does not receive Economy
  simple accounts, Smith experiments, scenario packs, or WebGL observer evidence.
- Former compatibility namespaces are retired. Persisted format and schema
  identifiers remain only under the exact compatibility policy in
  `docs/architecture/namespace-compatibility.md`.
- Ledger build/test/audit proof passed before Economy deleted the old Simulator
  projects. Current identity and boundary validators now guard namespace and
  cross-repository drift.
