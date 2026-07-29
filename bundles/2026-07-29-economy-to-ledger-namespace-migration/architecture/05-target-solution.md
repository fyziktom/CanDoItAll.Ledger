# Target Solution

## Naming Decision

`CanDoItAll.Ledger.Engine` replaces `CanDoItAll.Economy.Ledger`.

Engine is the narrowest accurate responsibility name because the project
executes and validates ledger transactions, enforces issuance/token policies,
and verifies chain/replay semantics. `Ledger.Core` already owns domain contracts;
`Ledger.Runtime` would be ambiguous with Node hosting and application runtime.

## End State

- All repository-owned active project identities are under
  `CanDoItAll.Ledger`.
- Folder, file, project, assembly, namespace, using, Razor import, test,
  tool/example, solution, config, and Docker identities agree.
- The project graph is unchanged apart from names.
- Current docs describe the Ledger-owned surface and breaking migration.
- Simulator documentation prominently says WIP and experimental/not
  production-ready.
- Historical evidence remains honest rather than being rewritten.

## Side Effects

- Public namespaces and assembly names are intentionally breaking.
- Compose adopts current SharedInfo defaults on the touched surface.
- Existing ignored `docker/data` remains untouched; new named-volume runtime
  validation does not imply migration or deletion of that local data.
