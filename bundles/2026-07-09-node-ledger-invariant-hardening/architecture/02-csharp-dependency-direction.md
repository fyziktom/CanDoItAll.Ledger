# Dependency direction

## Current product direction

```text
Node / SDK / BusinessObjects / CLI
  -> Ledger
  -> Cryptography
  -> Core

Persistence.EFCore / Storage.InMemory
  -> Core

Persistence.PostgreSql
  -> Persistence.EFCore
```

The composition root references concrete persistence and policy implementations. Core has no host, persistence, UI, or provider dependency.

## Changes allowed in this bundle

- No new project reference is required for validation/policy/store hardening.
- Ledger continues to reference only Core and Cryptography.
- In-memory and EF stores continue to reference Core, not Ledger.
- Node may configure server request limits but domain rules do not move into Node.

## Forbidden changes

- Core -> Ledger/Persistence/Node.
- Ledger -> EF/PostgreSQL/Node/BusinessObjects.
- Storage.InMemory -> Ledger.
- Validator access through `IServiceProvider`.
- A new `Common`, `Helpers`, or partial-class dumping ground.

## Analytics evidence

CodeAnalytics snapshot `snap-20260709210242-9c7cb5f2-failed` loaded zero projects due its host SDK resolution failure. Dependency evidence is therefore direct `.csproj` inspection plus successful build/test, recorded as an explicit validation gap.

