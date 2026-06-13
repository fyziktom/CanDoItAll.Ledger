# Ledger Simulator Runtime Ownership Inventory

Status: SB04 review update after SB02 landing and SB03 Economy removal.

This Ledger copy mirrors the Economy inventory for the v27 split. It records the
actual post-landing BO/project simulator runtime ownership in this repo.

## Ledger Landing Decision

SB02 created these physical Ledger projects:

- `src/CanDoItAll.Ledger.Simulator`
- `src/CanDoItAll.Ledger.Simulator.Components`
- `src/CanDoItAll.Ledger.Simulator.Persistence.EFCore`
- `examples/CanDoItAll.Ledger.Simulator.App`

The first landing pass kept `CanDoItAll.Economy.Simulator*` assembly and
namespace identities inside these Ledger project folders to reduce migration
risk. This is temporary compatibility debt owned by the Ledger repo maintainer.
It is reviewed at SB04 and must either be renamed or given a concrete sunset
record at SB10.

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
| `tests/CanDoItAll.Economy.Tests/EconomySimulatorTests.cs` | `tests/CanDoItAll.Ledger.Tests/EconomySimulatorTests.cs` | Old BO/project simulator golden, projection, and persistence tests; Economy copy deleted in SB03. |
| `tests/CanDoItAll.Economy.Tests/EconomySimulatorComponentTests.cs` | `tests/CanDoItAll.Ledger.Tests/EconomySimulatorComponentTests.cs` | Old simulator component tests; Economy copy deleted in SB03. |
| `tests/CanDoItAll.Economy.Tests/EconomySimulatorRuleAutomationTests.cs` | `tests/CanDoItAll.Ledger.Tests/EconomySimulatorRuleAutomationTests.cs` | BO rule-token/account-rule automation tests; Economy copy deleted in SB03. |
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
| `src/CanDoItAll.Economy.Core/**` | Keep/extend | Ledger transaction, address, token, rule, and BO primitives. |
| `src/CanDoItAll.Economy.BusinessObjects/**` | Keep/extend | BO transitions and account behavior. |
| `src/CanDoItAll.Economy.Components/**` | Keep/extend | Ledger/BO UI components. |
| `src/CanDoItAll.Economy.Simulation.Ledger/**` | Keep/extend | Ledger-backed simulation adapter; must not import simple accounts or Smith. |
| `docs/architecture/repository-boundaries.md` | Update | Must distinguish Ledger BO simulator ownership from Economy simple/Smith runtime ownership. |

## Governance And Boundary Notes

- Scarce resources are provenance trust, build/test time, maintainer review, and
  future route ownership clarity.
- Ledger receives the BO/project simulator only. It does not receive Economy
  simple accounts, Smith experiments, scenario packs, or WebGL observer evidence.
- Compatibility namespaces are a temporary local migration rule with measured
  benefit, complexity delta, validation cost, and sunset condition. They must not
  become a permanent global naming policy.
- Ledger build/test/audit proof passed before Economy deleted the old simulator
  projects. SB04 and SB10 remain the audit gates for compatibility namespace
  debt and cross-repo boundary drift.
