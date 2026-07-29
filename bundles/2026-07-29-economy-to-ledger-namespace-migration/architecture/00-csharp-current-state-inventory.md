# C# Current-State Inventory

## Evidence

- Inspected `CanDoItAll.Ledger.slnx`, all 27 repository-owned `.csproj` files,
  project reference entries, partial declarations, and active identity hits.
- CodeAnalytics snapshots
  `snap-20260729100803-74c7bb7f-failed` and
  `snap-20260729100957-17ef6d18-failed` are unhealthy (zero projects/types;
  blocking SDK resolution diagnostic). Negative findings from them are invalid.
- Manual inventory is recorded in `analysis/01-current-state.md` and
  `inventories/01-scope-inventory.md`.

## Responsibilities And Owners

- Core owns contracts/domain primitives.
- Engine owns validation, execution, chain/replay verification, issuance and
  token policies.
- BusinessObjects and SDK own higher-level transaction/account APIs.
- Node owns composition, hosting, APIs, auth, and admin shell.
- Persistence projects own EF Core and PostgreSQL implementations.
- Components/examples/tools/tests consume those owners.
- Simulator projects form a separate BO/project simulation lane.

## Large/Partial Types

- No responsibility extraction is planned.
- Existing `EconomyNodeHost`, `SimulationDeterministicHash`, Simulator constant
  partials, and EF migration partials are renamed in place.
- No partial file, constructor dependency, or direct-instantiation topology may
  be added by this migration.

## Tests And Gaps

- Existing coverage is centralized in `CanDoItAll.Ledger.Tests`.
- The missing architecture-specific proof is an automated identity/path
  residual guard and before/after graph equivalence assertion.
- Runtime composition proof is the Node/DemoSeeder/AccountApp Compose smoke.
