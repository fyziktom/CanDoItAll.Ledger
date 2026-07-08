# SB01 Overview Layout Foundation

## Status

Closed

## Objective

Create the shared card and adaptive first-tab layout foundation: no-DataItems BOs should not show the empty DataItems hero, while media BOs with DataItems keep the current preview experience.

## Covered Inputs

- Hide empty DataItems gallery hero.
- Use same gallery card for related previews.
- Preserve media DataItems path.
- Avoid custom CSS.

## Prerequisites

- Root bundle prepared.
- ImageGen proposal and reference screenshots saved.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectDataItemPreview.razor`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCommerceUiComponentTests.cs`

## Deliverables

- New shared gallery-card component.
- Gallery page uses that component.
- Detail Overview branches by `DataItems.Count`.
- Empty DataItems detail renders fact/summary layout without DataItems hero.

## Dependency Impact

Critical foundation for SB02 related BO cards and SB03 screenshot proof.

## Validation Depth

Critical semantic gate with component tests and source assertions.

## Implementation Steps

1. Extract card component from gallery.
2. Replace gallery loop markup with the shared component.
3. Refactor detail Overview into DataItems and no-DataItems branches.
4. Add tests for empty and media branches.

## Scope Exceptions

Small/medium responsive behavior is out of scope by user request.

## Do Not Do

- Do not add custom CSS.
- Do not alter gallery filtering semantics.
- Do not change domain payload formats.

## Acceptance Checklist

- [x] Empty no-DataItems BO hides selected preview and empty DataItems rail.
- [x] Media BO with DataItems still shows selected preview.
- [x] Gallery cards still render and existing tests pass.
- [x] No custom CSS added.

## Proof Required

- Focused failing-first or characterization component test.
- Passing focused component tests.
- Source assertion showing shared component use.
- Anti-stub audit for no TODO/NotImplemented in touched production flow.

## Browser Validation Logging

Record in `bundle://reviews/01-execution-report.md` after SB03 screenshot capture.

## C# Architecture Impact

Local UI extraction inside existing project.

## Boundary Ownership

`CanDoItAll.Economy.Components` owns shared card and dialog layout.

## Dependency Direction

No project reference changes.

## Pattern Decision

Simple extracted component.

## Testability Contract

Tests render extracted/shared behavior through bUnit without a full app.

## Partial Class Policy

No partial classes.

## Architecture Proof Required

Source assertion that gallery and detail use `BusinessObjectGalleryCard`.

## Progression Gate

Passed. Focused tests prove empty/media branch behavior and shared card reuse.

## Closure Proof

- `bundle://proof/SB01/transcripts/focused-layout-tests.txt`
- `bundle://proof/SB03/transcripts/source-assertions.txt`
- `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Suggested Agent Prompt

Implement SB01 only. Do not add new custom CSS. Extract the gallery card and make the first tab adaptive by DataItems presence.
