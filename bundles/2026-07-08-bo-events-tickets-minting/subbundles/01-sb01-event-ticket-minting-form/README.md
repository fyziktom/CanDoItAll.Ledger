# SB01 Event Ticket Minting Form

## Status

- `Completed`

## Objective

- Replace the generic event/ticket typed-field minting experience with VEF-aligned date-time controls, category dropdowns, ticket event preservation, and create-flow mint author autofill.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `N004`
- `N005`
- `N006`

## Prerequisites

- Prepared-stage bundle validator passed.
- Current wizard still builds event/ticket fields from `BusinessObjectFamilyCatalog`.
- Existing ticket event picker and multimint tests are understood before edits.

## Exact Source References

- repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor
- repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs
- repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTicketEventPrefill.cs
- repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs
- C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\EventsAndTickets\EventSpecificMintForm.razor
- C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\EventsAndTickets\TicketSpecificMintForm.razor

## Deliverables

- Event/ticket field rendering branch in the wizard.
- Date-time field helper for `eventDate`, `startsAt`, `endsAt`, and `usedAt`.
- Dropdown options for event class, ticket class, ticket duration, status, and used fields.
- Create-flow defaulting for `mintAuthorAddress` from `Workspace.Address.Value`.
- Targeted component tests for markup and staged payload semantics.

## Dependency Impact

- SB02 depends on this form because renderer and browser proof must inspect the final payload and fields.
- If this subbundle misses mint author autofill, SB02 final proof is untrustworthy.

## Validation Depth

- Critical foundation semantic proof.
- Targeted bUnit tests for wizard controls and staged drafts.
- Source assertions for option lists and mint-author production path.
- Anti-stub audit for changed production and test files.

## Implementation Steps

- Retry or record CanDoItAll components MCP availability.
- Add/adjust helper methods in `BusinessObjectCreationWizard.razor` for event/ticket field rendering.
- Add dropdown option lists for VEF-style categories.
- Add date-time value formatting/parsing helpers.
- Autofill `mintAuthorAddress` only for create-mode event/ticket payloads.
- Add targeted tests in `BusinessObjectTicketMintingComponentTests.cs`.
- Capture failing-first and passing transcripts.

## Do Not Do

- Do not remove ticket event selection or multimint.
- Do not change unrelated BO families from the generic field loop.
- Do not overwrite existing edit-mode mint author values.
- Do not create a second standalone event/ticket wizard.

## Acceptance Checklist

- Event wizard renders date-time and category controls.
- Ticket wizard renders date-time, class, duration, used/status controls, and event picker/multimint panels.
- Staged event/ticket create drafts contain `mintAuthorAddress` equal to `Workspace.Address.Value`.
- Existing event prefill and multimint tests still pass.
- SB01 proof manifest and semantic invariant contract exist.

## Proof Required

- Failing-first transcript for missing domain controls or missing mint author autofill.
- Passing transcript for targeted tests.
- Source assertions transcript for production code paths.
- Anti-stub audit transcript.
- `bundle://proof/SB01/manifest.md`.
- `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- Route: AccountApp or demo app page that hosts `BusinessObjectCreationWizard`.
- Viewport: large desktop first, then narrower width if layout changes are visible.
- Evidence: screenshot of event/ticket form controls or explicit blocker if app route cannot be run.
- Record row in `reviews/01-execution-report.md`.

## Progression Gate

- SB02 may start only after SB01 targeted tests pass, source assertions prove production behavior, and `proof/SB01/manifest.md` plus `proof/SB01/semantic-invariants.md` exist.

## Suggested Agent Prompt

- Implement SB01 from this bundle. Keep changes scoped to the BO wizard and focused tests. Prove date-time/category controls and mint author autofill with artifact-backed transcripts before moving to SB02.
