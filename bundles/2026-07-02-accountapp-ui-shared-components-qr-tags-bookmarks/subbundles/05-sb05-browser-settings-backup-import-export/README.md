# SB05 Browser Settings Backup Import Export

## Status

- `Completed`

## Objective

Add a dedicated dialog for exporting and importing AccountApp browser settings, tag metadata, address directory state, and related local preferences as a separate file or zip.

## Success Criteria

- Home/top toolbar exposes settings backup as a compact button only.
- Dialog shows export/import sections, preview, conflict strategy, and confirmation.
- Export includes browser-local settings, tag occurrence/visual metadata, address directory, favorites, user tags, and safe profile cache summaries.
- Import can restore selected sections without corrupting account/vault data.
- Tests prove round-trip export/import and conflict handling.

## Covered Inputs

- Backup/import/export all localStorage/IndexedDB settings request.
- Separate dialog request.
- Home page button-only entry request.
- Address/tag settings persistence dependencies.

## Prerequisites

- SB02 toolbar placement.
- SB03 tag store.
- SB04 address directory store.

## Exact Source References

- `repo://examples/CanDoItAll.Economy.AccountApp/Services/BrowserLocalStorageAccountWorkspaceCache.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceCache.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/AccountSettingsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Modals\DialogScaffold.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Navigation\Toolbar.razor`

## Deliverables

- Settings section registry.
- Export/import service with versioned manifest.
- Dialog for export, import preview, section selection, conflict handling, and confirmation.
- Toolbar/home entry button.
- Tests for round-trip, selected-section import, conflict handling, and invalid backup rejection.
- Browser proof of export/import dialog.

## Dependency Impact

- SB07 final closure validates backup/import alongside visual flow.
- Settings backup protects state created by SB03 and SB04.

## Validation Depth

- `Process-critical storage closure`
- Requires service tests, invalid input tests, and browser proof.

## Implementation Steps

1. Inventory browser-local settings and stores.
2. Define backup manifest version, sections, timestamps, source account, and checksum/hash metadata.
3. Implement export for UI settings, tag stats, address directory, profile cache summaries, and safe cache metadata.
4. Implement import preview with section selection.
5. Implement conflict strategy such as merge, replace section, or skip.
6. Add a dialog entry to the shell/top toolbar.
7. Add tests for round trip, selected import, invalid file, and conflict behavior.
8. Capture browser screenshots of export and import preview.

## Scope Exceptions

- Encrypted account vault backup remains the existing account backup flow unless explicitly integrated through a separate section.
- Cloud sync is out of scope.

## Do Not Do

- Do not overwrite account/vault data silently.
- Do not import without preview and confirmation.
- Do not expose the full import/export UI inline on the home page.

## Acceptance Checklist

- Export file/zip is created with versioned manifest.
- Import preview shows sections and conflicts.
- User can choose sections to import.
- Invalid backup fails with clear error and no partial write.
- Tag/address settings round trip successfully.
- Toolbar entry remains compact and tooltip-labeled.

## Proof Required

- Tests for export/import round trip.
- Test for invalid backup rejection.
- Test for conflict handling.
- Browser screenshot of export dialog.
- Browser screenshot of import preview.
- Execution report update for SB05.

## Browser Validation Logging

- Route: AccountApp home after unlock.
- Viewport: 1920x1080 or maximized large-screen browser.
- Actions: open backup dialog, export, import preview, toggle sections, cancel/confirm.
- Screenshots: `bundle://evidence/SB05/settings-backup-export-large.png`, `bundle://evidence/SB05/settings-backup-import-preview-large.png`.
- Review questions: Is backup hidden behind a dialog; is the toolbar compact; is preview clear; is no inline accordion added?

## Progression Gate

- SB07 final closure may start only after export/import tests pass and browser screenshots are logged.

## Suggested Agent Prompt

```text
Implement SB05 only. Add versioned settings export/import behind a dialog, protect account data with preview and conflict handling, prove round-trip and invalid import behavior with tests, capture screenshots, and update the execution report.
```

