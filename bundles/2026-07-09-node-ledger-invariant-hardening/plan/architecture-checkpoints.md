# Architecture checkpoints

## After SB01

- `LedgerExecutor` delegates stage logic to top-level validators/pipeline.
- Preflight executes before any hasher call.
- No partial/nested/helper-manager architecture added.
- Direct unit tests instantiate extracted validators.

## After SB02/SB03

- Policy and conservation errors agree and do not create contradictory acceptance paths.
- Existing builders still produce accepted transactions.
- BO carrier/owner semantics are enforced by Ledger even for raw callers.

## After SB04

- Both custom in-memory and PostgreSQL/EF confirmation contracts are atomic.
- Store code does not reimplement domain validation.
- Relational constraints are provider-compatible and do not break valid authority BO rows.

## Before SB06

- Dependency direction unchanged or every new reference justified.
- Full targeted tests green.
- Docker demo behavior changes are understood before rebuild/reset.

## Final gate

- Old executor owns fewer responsibilities.
- No new service locator or partial class.
- Every critical finding has negative proof.
- Residual proposals are compatibility-scoped, not presented as shipped fixes.

## Second-wave checkpoints

### After SB08

- migration/adoption SQL lives only in PostgreSQL persistence;
- invalid legacy rows block before history mutation;
- Node callers share the migrator contract;
- migration assembly selection is proven.

### After SB09/SB10

- replay version registry has exact v1/v2 owners and rejects unsupported versions;
- Core/Ledger still have no storage/EF dependency;
- v1 golden vectors pass unchanged;
- token definition persistence contains immutable metadata while display metadata remains outside the ID hash.

### After SB11

- JWT authentication and authorization are separate;
- endpoint privilege classification is explicit and denied scopes never invoke handlers.

### After SB12

- NCalc package/types exist only in the driver;
- rule orchestration can be tested with a fake evaluator;
- BusinessObjects did not gain a service locator/global mutable provider.

### Second-wave final gate

- direct `.csproj` graph has no cycle/forbidden reference;
- no new partial class;
- each extracted service/strategy/adapter has direct tests;
- architecture review is Pass or Pass with named follow-up;
- failed CodeAnalytics snapshot remains recorded rather than treated as an empty clean graph.

Closure: Passed on 2026-07-10. Evidence is in `../reviews/csharp-architecture-gate.md` and `../proof/second-wave/summary.md`.
