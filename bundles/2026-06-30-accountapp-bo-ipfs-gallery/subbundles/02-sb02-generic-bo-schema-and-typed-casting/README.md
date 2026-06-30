# SB02 Generic BO Schema And Typed Casting

## Status

- Status: Completed

## Objective

Introduce a robust generic BO schema/catalog and typed builder/parser/caster architecture for all in-scope generic BO families.

## Covered Inputs

- RN-02
- RN-03
- RN-04
- RN-05
- REQ-03
- REQ-04
- REQ-06

## Prerequisites

- SB01 closure gate passed.
- Current scope inventory remains accurate.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItems.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCasting.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/ServiceCollectionExtensions.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCastingTests.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectDataItemTests.cs`
- `bundle://inventories/01-scope-inventory.md`

## Deliverables

- Catalog for in-scope generic BO families, aliases, icons, required/optional fields, and display facts.
- Builder/parser helpers that produce stable BO state JSON and parse legacy/media-compatible payloads.
- Typed view model(s) or reusable typed view representation exposed through `IBusinessObjectView`.
- Registered casters for in-scope families with generic fallback reserved for unsupported/invalid data.

## Dependency Impact

- SB03 depends on this shared layer to avoid ad hoc gallery-only parsing.
- SB04 depends on typed casting to verify real ledger objects from Alice/Bob after refresh.

## Validation Depth

- Critical foundation
- Unit and semantic parser proof
- Negative malformed-input proof

## Implementation Steps

1. Add the generic BO catalog and payload model.
2. Implement builder/parser helpers with clear schema version handling.
3. Extend casting registration for in-scope generic families.
4. Add tests for aliases, positive family parsing, malformed payload handling, media backward compatibility, and excluded families.
5. Update source assertions in proof manifest.

## Scope Exceptions

- Domain-specific VEFramework families are excluded and should remain generic or unsupported.
- SB02 does not redesign wizard UI; it exposes the model that SB03 consumes.

## Do Not Do

- Do not add one-off JSON parsing inside `BusinessObjectLedgerGallery` as the primary architecture.
- Do not accept a typed family view when required fields are absent without representing the missing-field state.
- Do not depend on VEFramework assemblies.

## Acceptance Checklist

- [x] Every in-scope family has a catalog definition.
- [x] Every in-scope alias maps to the correct family.
- [x] Parser handles the current `BusinessObjectMediaPayload` shape.
- [x] Tests cover typed family casting and generic fallback for unsupported families.
- [x] `AddBusinessObjectCasting` registers the new caster path.

## Proof Required

- `proof/SB02/transcripts/01-focused-casting-tests-failing-first.txt` or explicit process exemption if tests are new and baseline is source absence.
- `proof/SB02/transcripts/02-focused-casting-tests-passing.txt`
- `proof/SB02/transcripts/03-anti-stub-audit.txt`
- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`

## Browser Validation Logging

- N/A for SB02. Browser proof occurs after UI consumes the schema in SB03.

## Progression Gate

- SB03 may start only after unit tests prove all in-scope generic families cast through the shared registry.

## Suggested Agent Prompt

```text
Implement SB02 only. Add the generic BO schema/catalog/parser/caster layer and tests, keep VEFramework as reference only, and stop if any in-scope family lacks typed parser coverage.
```
