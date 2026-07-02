# SB03 Tag Occurrence Service And Shared Tag Rendering

## Status

- `Completed`

## Objective

Implement browser-persisted tag occurrence statistics, stable tag visual metadata, and shared BaseLib tag editor/badge rendering across BO entry and details surfaces.

## Success Criteria

- Tags have stable color metadata with readable text contrast after reload.
- General and unique occurrence counters behave differently and are tested.
- Settings allow choosing the source for frequent tag suggestions.
- BO creation/edit uses BaseLib `TagEditor`.
- Tag display uses BaseLib badge components instead of local spans where practical.

## Covered Inputs

- Unique/general tag occurrence storage request.
- Most-often tag suggestion request.
- Settings selection for "often" source request.
- Colored badge and persistent color request.
- Prepared `TagsEditor` and badge component reuse request.

## Prerequisites

- SB01 component boundary inventory.
- Existing browser cache/localStorage access from AccountApp.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectWorkspaceModel.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceCache.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/BrowserLocalStorageAccountWorkspaceCache.cs`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Forms\TagEditor.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Badges\Badge.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Badges\StatusBadge.razor`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Minting\SpaceTags.razor`

## Deliverables

- Tag occurrence model and store.
- Browser persistence implementation.
- Tag suggestion service.
- Tag visual metadata/color allocator with contrast guard.
- Settings model for frequent-tag source.
- BO wizard/details integration with BaseLib `TagEditor` and badges.
- Service tests for counters, color persistence, and suggestions.

## Dependency Impact

- SB04 uses tag editor/badges for address user tags.
- SB05 exports/imports tag stats and visual metadata.
- SB07 validates tag UI visually after reload.

## Validation Depth

- `Semantic service and UI component proof`
- Requires unit tests, storage proof, browser proof, and screenshot review.

## Implementation Steps

1. Define tag occurrence and visual metadata models.
2. Implement localStorage-backed store through the existing browser storage pattern.
3. Normalize tag text consistently and key unique counts by account plus BO identity.
4. Add general count increment for every relevant BO detail view open.
5. Add unique count increment only once per account/BO/tag tuple.
6. Add suggestion service with mode selection.
7. Integrate `TagEditor` in BO creation/edit surfaces.
8. Render tags with BaseLib badges and persisted color tokens.
9. Add settings surface hook for suggestion mode, with detailed UI finalized in SB05 if needed.
10. Add tests and browser proof.

## Scope Exceptions

- Global cross-device tag sync is out of scope.
- Address manager user tags consume this model in SB04.
- Full settings backup/export is SB05.

## Do Not Do

- Do not copy VEBlazor's global/static tag storage approach.
- Do not store tag colors without contrast validation.
- Do not rely on screenshot-only proof for counter semantics.

## Acceptance Checklist

- Opening the same BO repeatedly increments general count but not unique count after the first open.
- Opening a different BO with the same tag increments unique count.
- Suggestion ordering changes when switching unique/general mode.
- Tag color remains stable after reload/export/import.
- Badge text remains readable.
- BO wizard no longer uses the old plain textbox-only tag entry for primary entry.

## Proof Required

- Service test covering general count.
- Service test covering unique count.
- Service test covering color persistence and contrast.
- Browser screenshot of `TagEditor` suggestions and badge display.
- Execution report update for SB03.

## Browser Validation Logging

- Route: BO create/edit/details flow.
- Viewport: 1920x1080 or maximized large-screen browser.
- Actions: create/edit tags, open details multiple times, switch suggestion mode, reload, verify badges.
- Screenshots: `bundle://evidence/SB03/tag-editor-suggestions-large.png`, `bundle://evidence/SB03/tag-badges-after-reload-large.png`.
- Review questions: Are tags compact; are suggestions useful; are colors stable/readable; is dialog layout unclipped?

## Progression Gate

- SB04 and SB05 may consume tags only after storage semantics and color persistence tests pass.

## Suggested Agent Prompt

```text
Implement SB03 only. Build the tag occurrence store, integrate BaseLib TagEditor/badges, add semantic tests for unique/general counts and color persistence, capture browser screenshots, and update the execution report.
```

