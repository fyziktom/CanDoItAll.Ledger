# SB04 Business Object Wizard Proof Manifest

## Delivered

- Create uses template -> details -> review with preloaded valid JSON.
- Transfer, Update, Close, Clone, Merge, and Split Payment share source -> selection -> details -> review wizard composition.
- Clone presents the literal requested Mine / Another address / Direct reference choices; direct supports object ID or data hash.
- Selected BOs are compact cards that show type, version, owner, operation, object ID, and data-hash preview.
- Owned-object actions require a selected workspace object; Merge requires two different, type/schema-compatible objects.

## Evidence

- Direct tests cover populated card selection, direct lookup result delivery, unavailable owned-object guard, merge incompatibility, and validation readiness.
- Empty AccountApp visual pass validates all source/wizard entry states without pretending data exists.
- `BusinessObjectLookupPanel` delegates actual external lookup through the adapter; no Razor component performs network access directly.

## Semantic Invariants

- Transfer/Update/Close/Split Payment cannot submit an arbitrary generated draft object ID.
- Clone allows external sources; owned operations require current-account inventory.
- Merge blocks duplicate IDs and incompatible type/schema pairs.
- Split Payment requires both receivers and positive allocations.
