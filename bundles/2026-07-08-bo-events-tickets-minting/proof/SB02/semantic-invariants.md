# SB02 Semantic Invariants

## Invariant 1

- Invariant ID: `SB02-DETAIL-RENDERER-PRESERVES-EVENT-TICKET-FACTS`
- Source raw note: `N001`, `N002`, and `N006` require improved event/ticket behavior while preserving specific minting data and detail rendering.
- Expected behavior: Ticket detail rendering keeps related event facts and also renders ticket facts such as `Mint author address` and `FreeToUse` when those fields are present.
- Disallowed shallow implementation: Adding fields only to the minting form while details drop the author/status facts or only show unrelated event-card summaries.
- Failing-first test: N/A - no production renderer behavior changed in SB02; this invariant is process proof over SB01 production behavior.
- Passing test: `bundle://proof/SB02/transcripts/passing-tests-renderer-closure.txt`
- Changed source files: `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectDetailRendererLayoutTests.cs`; `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-renderer-closure.txt`
- Red-team negative case: A renderer that only shows the related event card would fail the detail test assertions for `Mint author address` and `FreeToUse`.
- Downstream dependency check: Details remain compatible with the SB01 minting output and the generic typed-family fact renderer.

## Invariant 2

- Invariant ID: `SB02-FINAL-CLOSURE-PROOF`
- Source raw note: `N003`, `N004`, `N005`, and `N006` require the final UI to demonstrate date-time controls, predefined dropdowns, and author autofill in the live app surface.
- Expected behavior: The demo BO creation route renders event and ticket forms with `datetime-local` date inputs, predefined class/duration/status dropdowns, preserved ticket event/multimint panels, and a read-only author address from the active minting workspace.
- Disallowed shallow implementation: Passing bUnit tests while the browser surface still renders generic text inputs, hides the ticket event picker, overflows on mobile, or leaves the author address editable/empty.
- Failing-first test: N/A - no production behavior changed in SB02; browser validation is final closure proof for SB01 production behavior.
- Passing test: `bundle://proof/SB02/transcripts/playwright-demo-ui.txt`
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`; `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs`
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-renderer-closure.txt`; `bundle://proof/SB02/transcripts/playwright-demo-ui.txt`
- Red-team negative case: A form that only changes labels would fail the live Playwright checks for `datetime-local`, dropdown text, and read-only `cdia1` author values.
- Downstream dependency check: The completed bundle cites `bundle://proof/SB02/manifest.md`, this invariant contract, browser screenshots, and the final validator transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `mintAuthorAddress` ticket fact | `bundle://proof/SB01/transcripts/passing-tests-event-ticket-form.txt`; `bundle://proof/SB02/transcripts/playwright-demo-ui.txt` | `bundle://proof/SB02/transcripts/passing-tests-renderer-closure.txt`; `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectDetailRendererLayoutTests.cs` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`; `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs` | `bundle://proof/SB01/transcripts/failing-first-event-ticket-form.txt` |
| Event/ticket domain controls | `bundle://proof/SB02/transcripts/source-assertions-renderer-closure.txt`; `bundle://proof/SB02/transcripts/playwright-demo-ui.txt` | `bundle://proof/SB02/transcripts/passing-tests-renderer-closure.txt`; `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | `bundle://proof/SB01/transcripts/failing-first-event-ticket-form.txt` |
