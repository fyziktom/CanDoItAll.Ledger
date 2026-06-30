# SB03 Semantic Invariants

## SB03-INV-01 Catalog-Backed Wizard And Menu

- Invariant ID: SB03-INV-01
- Source raw note: RN-02 and RN-05 require all BO types we create to be available and testable while excluding domain-specific VEFramework-only types.
- Expected behavior: the AccountApp menu and wizard options are generated from `BusinessObjectFamilyCatalog`.
- Disallowed shallow implementation: duplicate a hard-coded menu list that can drift from parser/caster behavior.
- Failing-first test and transcript: a catalog/menu mismatch would fail SB04 browser creation for one or more families.
- Passing test and transcript: `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: `bundle://proof/SB03/transcripts/04-source-assertions-and-anti-stub.txt`.
- Red-team negative case: domain-specific VEFramework categories are not present in the menu.
- Downstream dependency check: SB04 imports Alice and Bob and uses the menu to create real records.

## SB03-INV-02 Typed Gallery And Details

- Invariant ID: SB03-INV-02
- Source raw note: RN-02 requires correct data in gallery for Posts, Tickets, and the other created BO families.
- Expected behavior: gallery cards, details, and media renderer show typed facts from `ITypedBusinessObjectView`.
- Disallowed shallow implementation: show a generic card with raw JSON or only a title.
- Failing-first test and transcript: browser proof would show missing or raw data if typed facts were not rendered.
- Passing test and transcript: `bundle://proof/SB04/screenshots/alice-all-generic-types-gallery.png`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`.
- Red-team negative case: generic fallback remains available for unsupported types but does not swallow in-scope family records.
- Downstream dependency check: Bob receives and views a transferred typed Message BO in SB04.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `TypedBoWizardPayload` | `bundle://proof/SB03/transcripts/04-source-assertions-and-anti-stub.txt` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs` | `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt` | Catalog/menu mismatch rejected by browser family matrix proof |
| `TypedGalleryFacts` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor` | `bundle://proof/SB04/screenshots/alice-all-generic-types-gallery.png` | `bundle://proof/SB03/transcripts/04-source-assertions-and-anti-stub.txt` |
