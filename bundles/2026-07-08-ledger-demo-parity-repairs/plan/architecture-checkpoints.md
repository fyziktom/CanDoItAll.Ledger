# Architecture Checkpoints

## Checkpoint A - Before SB01

- Dependency direction: no new project references planned.
- Partial-class policy: no partial files planned.
- Testability: console proof must use shared account APIs.
- Unlock decision: SB01 may start after prepared validator passes.

## Checkpoint B - After SB01

- Confirm `DemoAccountDriver` remains a thin facade over `BusinessObjectAccount`.
- Confirm no console-local transaction assembly was added.
- Confirm tests prove UTXO split/merge behavior and do not rely only on output text.
- Unlock decision: SB02 may start only if SB01 proof manifest exists.

## Checkpoint C - After SB02

- Confirm SSR development fallback default uses one-token BO carrier semantics.
- Confirm SSR split/merge proof goes through session/account APIs.
- Confirm no project reference or partial-class expansion occurred.
- Unlock decision: final closure may run after proof manifests and completed validator pass.
