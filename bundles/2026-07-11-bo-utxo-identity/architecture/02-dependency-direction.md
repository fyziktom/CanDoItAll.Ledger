# Dependency Direction

## Project roles and references

| Project | Role | Relevant dependencies |
|---|---|---|
| Economy.Core | contracts/domain primitives | none |
| Economy.Cryptography | wire canonicalization | Core |
| Economy.Ledger | protocol validation/runtime | Core, Cryptography |
| Economy.BusinessObjects | application builders and payloads | Core, Cryptography, Ledger |
| Economy.Persistence.EFCore | persistence/projections | Core |
| Economy.Sdk | node transport adapter | Core, BusinessObjects, Cryptography |
| Economy.Node | composition root and read models | application, persistence, SDK |
| Economy.Components | Blazor UI/application adapters | BusinessObjects, SDK, Ledger |

## Decision

No new project reference is required. Outpoint identity conversion and lineage-key primitives live in Core so all outward adapters can depend inward. Payload/reference construction stays in BusinessObjects. EF and Node map persisted wire state outward without Core depending on persistence or transport.

## Forbidden directions

- Core must not reference BusinessObjects, Node, EF, SDK, or Components.
- Ledger validation must not call Node/read-model services to determine identity.
- Components must not reinterpret persisted lineage columns.
- Persistence must not call UI helpers or synthesize metadata-based public IDs.
- Public transport/UI DTOs must not reconstruct a `BusinessObjectRef` from a public outpoint; protocol lineage and public identity are separate types and boundaries.

Spendable transport may cross the boundary with both canonical `ObjectId` and explicitly named `LineageKey`. The SDK must first validate `ObjectId == OutPoint` and may only then reconstruct the V1/V2 protocol reference from `LineageKey`.

## Tool note

The scoped CodeAnalytics snapshot `snap-20260711135741-e0c59c57-failed` was unusable because its host resolved a missing .NET SDK from `C:\repositories\CanDoItAll\global.json`. Direct `.csproj` inspection found no cycle-producing reference change, and the final proof must include a solution build.
