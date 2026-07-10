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

## Second-wave dependency plan

Current relevant direct references before edits:

| Project | References |
| --- | --- |
| Core | none |
| Cryptography | Core |
| Ledger | Core, Cryptography |
| Storage.InMemory | Core |
| Persistence.EFCore | Core |
| Persistence.PostgreSql | Persistence.EFCore |
| BusinessObjects | Core, Cryptography, Ledger, NCalc package |
| SDK | BusinessObjects, Core, Cryptography |
| Node | BusinessObjects, Components, Core, Cryptography, Ledger, EFCore, PostgreSql, SDK |

Target changes:

- Storage.InMemory -> Ledger is not required; Node creates replay state using the existing concrete store.
- Node -> Storage.InMemory is added for replay composition only.
- Rules.NCalc -> BusinessObjects and NCalc package.
- SDK -> Rules.NCalc so SDK composition registers the default driver.
- Tests -> Rules.NCalc for direct driver vectors.
- BusinessObjects drops the NCalc package and does not reference Rules.NCalc.

Forbidden: Core/Ledger -> storage or EF; BusinessObjects -> Rules.NCalc; EFCore -> Npgsql; contracts typed as NCalc objects; service-location from rule execution. Cycle risk is low because both new outer references point inward. Manual before/after `.csproj` inventory and a full build substitute for failed CodeAnalytics snapshot `snap-20260710110637-cfb21d28-failed`.
