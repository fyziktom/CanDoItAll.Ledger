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

