# Requirement Traceability

## Raw Note Coverage

| Raw note | Requirement IDs | Impacted surface | Planned proof | Owning subbundle | Exception status |
| --- | --- | --- | --- | --- | --- |
| `N001` | `R001`, `R007` | BO wizard and details | Targeted tests, source assertions, final closure report | `SB01`, `SB02` | None |
| `N002` | `R002`, `R003`, `R005`, `R006` | VEF-aligned event/ticket fields | Source analysis, bUnit tests, renderer tests | `SB01`, `SB02` | None |
| `N003` | `R001`, `R006` | Domain-specific minting and renderers | Component tests and details renderer tests | `SB01`, `SB02` | None |
| `N004` | `R002` | Date-time field controls | Markup/source tests plus browser validation or blocker | `SB01` | MCP unavailable during preparation; fallback must be recorded if still unavailable |
| `N005` | `R003` | Event/ticket category fields | Dropdown markup tests and payload tests | `SB01` | Categories limited to existing catalog/VEF fields |
| `N006` | `R004` | Create draft payload | Staged draft payload test for `mintAuthorAddress` | `SB01` | Edit mode must preserve existing payload |

## Bundle Destination Matrix

| Requirement | Bundle files | Code targets | Proof targets |
| --- | --- | --- | --- |
| `R001` | `requirements/01-normalized-requirements.md`, `subbundles/01-sb01-event-ticket-minting-form/README.md` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | `bundle://proof/SB01/manifest.md` |
| `R002` | `analysis/01-current-state.md`, `subbundles/01-sb01-event-ticket-minting-form/README.md` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | `bundle://proof/SB01/transcripts/passing-tests-event-ticket-form.txt` |
| `R003` | `requirements/01-normalized-requirements.md` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | `bundle://proof/SB01/transcripts/source-assertions-event-ticket-form.txt` |
| `R004` | `requirements/01-normalized-requirements.md` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | `bundle://proof/SB01/transcripts/passing-tests-event-ticket-form.txt` |
| `R005` | `analysis/01-current-state.md` | `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTicketEventPrefill.cs` | `bundle://proof/SB01/transcripts/passing-tests-event-ticket-form.txt` |
| `R006` | `subbundles/02-sb02-renderer-and-closure-proof/README.md` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/Renderers/TypedFamilyBusinessObjectRenderer.razor` | `bundle://proof/SB02/transcripts/passing-tests-renderer-closure.txt` |
| `R007` | `reviews/01-execution-report.md` | N/A | `bundle://proof/SB02/manifest.md` |
