# SB01 Semantic Invariants

## Invariant 1

- Invariant ID: `SB01-EVENT-TICKET-DOMAIN-CONTROLS`
- Source raw note: `N003`, `N004`, and `N005` require better event/ticket minting forms, date-time controls, and predefined categories.
- Expected behavior: Event and ticket BO wizard fields use date-time controls for date fields and dropdown choices for class, duration, status, and boolean-like fields while preserving the existing ticket event picker and multimint panels.
- Disallowed shallow implementation: Labels or placeholder text that mention classes and dates while the production wizard still uses only generic text boxes.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-event-ticket-form.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing-tests-event-ticket-form.txt`
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`; `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs`
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-event-ticket-form.txt`
- Red-team negative case: Before the change, the source assertion found no date-time control test hooks, dropdown test hooks, or option lists, and exited non-zero.
- Downstream dependency check: SB02 must confirm detail rendering still shows important event and ticket facts after these fields are created.

## Invariant 2

- Invariant ID: `SB01-MINT-AUTHOR-AUTOFILL`
- Source raw note: `N006` says `mint author address must be autofilled based on from what address is it minting`.
- Expected behavior: Create-mode event and ticket drafts write `mintAuthorAddress` from `Workspace.Address.Value` in the production payload creation path; edit mode does not overwrite existing author state.
- Disallowed shallow implementation: A hard-coded address, a test-only seeded field, or an autofill that only appears in the UI but is absent from the staged draft payload.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-event-ticket-form.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing-tests-event-ticket-form.txt`
- Changed source files: `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`; `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`; `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs`
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-event-ticket-form.txt`
- Red-team negative case: A cosmetic control without `ApplyCreateDefaults` would fail the staged draft payload assertions.
- Downstream dependency check: SB02 must verify renderer/details behavior for `mintAuthorAddress`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `mintAuthorAddress` payload field | `bundle://proof/SB01/transcripts/source-assertions-event-ticket-form.txt`; `bundle://proof/SB01/transcripts/passing-tests-event-ticket-form.txt` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`; `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/Renderers/TypedFamilyBusinessObjectRenderer.razor` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` calls `ApplyCreateDefaults` from create payload flow | `bundle://proof/SB01/transcripts/failing-first-event-ticket-form.txt` |
