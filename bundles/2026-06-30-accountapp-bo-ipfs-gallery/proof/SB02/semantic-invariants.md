# SB02 Semantic Invariants

## SB02-INV-01 Shared Generic BO Catalog

- Invariant ID: SB02-INV-01
- Source raw note: RN-05 asks for generic BO families such as posts, tickets, events, payments, purchases, receipts, messages, and related generic records while excluding domain-specific VEFramework families.
- Expected behavior: in-scope families are defined once in the BusinessObjects layer and shared by casting, wizard, menu, gallery, and details.
- Disallowed shallow implementation: hard-code a UI menu without parser/caster support.
- Failing-first test and transcript: unsupported and malformed records must not become typed family views without catalog matches; see source assertions and focused casting tests.
- Passing test and transcript: `bundle://proof/SB02/transcripts/01-focused-casting-tests.txt`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: `bundle://proof/SB02/transcripts/03-source-assertions-and-anti-stub.txt`.
- Red-team negative case: domain-specific VEFramework-only families are not added to the catalog.
- Downstream dependency check: SB03 and SB04 use the same catalog for browser UI proof.

## SB02-INV-02 Central Parser Builder Caster

- Invariant ID: SB02-INV-02
- Source raw note: RN-04 requires a better builder/parser architecture for BO parsing than the older VEFramework NFT implementation.
- Expected behavior: payload construction, parsing, field normalization, and typed fact generation live in shared BusinessObjects code.
- Disallowed shallow implementation: parse JSON separately in each Razor component.
- Failing-first test and transcript: line-delimited IPFS and varied family payloads would fail isolated or single-shape parsers.
- Passing test and transcript: `bundle://proof/SB02/transcripts/01-focused-casting-tests.txt` and `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`.
- Red-team negative case: a gallery-only implementation would not satisfy casting tests that operate below the UI.
- Downstream dependency check: SB03 renders `ITypedBusinessObjectView` facts and SB04 proves those facts after real browser creation.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `BusinessObjectFamilyCatalog` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs` | `repo://src/CanDoItAll.Economy.Components/Components/VenftBusinessObjectApp.razor` | `bundle://proof/SB04/screenshots/alice-imported-expanded-menu.png` | `bundle://proof/SB02/transcripts/03-source-assertions-and-anti-stub.txt` |
| `BusinessObjectPayloadBuilder` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | `bundle://proof/SB02/transcripts/01-focused-casting-tests.txt` | Focused casting tests vary object type and fields instead of checking only non-empty strings |
