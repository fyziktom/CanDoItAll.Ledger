# SB04 Generic Address Manager Dialog Bookmarks And Subaccounts

## Status

- `Completed`

## Objective

Replace narrow bookmark management with a reusable address directory dialog that supports external contacts/bookmarks and own subaccounts across manage and selection flows.

## Success Criteria

- One generic address directory model represents external and own addresses.
- Dialog shows square cards with display name, address, kind, favorite state, tags, and cached profile BO when present.
- The same component supports home management, send recipient selection, BO send/mint recipient selection, gallery owner selection, and subaccount selection.
- Favorite flags and user tags persist in browser storage.
- Dialog uses BaseLib dialog, picker, badge, tag, and toolbar patterns where practical.

## Covered Inputs

- Improve bookmarks management request.
- Cached profile BO card request.
- Favorite flag and user tags request.
- Reusable dialog for home, send, minting, gallery, and subaccounts request.
- External versus own address distinction.

## Prerequisites

- SB03 tag store and badge model.
- Existing address bookmark and subaccount data loaded by AccountApp.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Account/AddressBookmarkManager.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/AddressBookPicker.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectWorkspaceModel.cs`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Modals\PickerDialogShell.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Forms\TagEditor.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Badges\Badge.razor`

## Deliverables

- Address directory models and local persistence service.
- Profile summary cache model for address cards.
- Generic address directory dialog.
- Manage and select modes.
- Flow integrations for home management, send tx, BO send/mint, gallery target, and subaccount selection.
- Tests for persistence, own/external filtering, favorite/tags, and selection result.

## Dependency Impact

- SB05 exports/imports address directory state.
- SB06 can use the address manager for verifier/recipient selection.
- SB07 validates dialog visual density and no overflow.

## Validation Depth

- `UI, component-test, and browser-proof`
- Requires semantic tests plus screenshot proof of manage and select modes.

## Implementation Steps

1. Define `AddressDirectoryEntry` and related profile/tag/favorite models.
2. Implement store seeded from existing bookmarks, transactions, and own subaccounts.
3. Replace or wrap `AddressBookmarkManager` with the new dialog entry.
4. Update `AddressBookPicker` or create a successor selection component using the same dialog.
5. Integrate recipient selection in transaction composer and BO send/mint surfaces.
6. Add favorite and user tag editing through dialog actions.
7. Display profile BO summary/avatar when cached.
8. Add tests and browser screenshots for manage/select flows.

## Scope Exceptions

- Network profile fetching beyond existing cached profile data can be staged if not already available.
- Bulk remote synchronization is out of scope.

## Do Not Do

- Do not create separate bookmark and subaccount dialogs with duplicated UI.
- Do not hardcode the component for only external contacts.
- Do not move long management lists into accordions.

## Acceptance Checklist

- External and own addresses are visually distinguishable.
- Favorites and tags survive reload.
- Selection mode returns the chosen address to send/BO flows.
- Manage mode can add/edit/remove user metadata.
- Profile cache appears when available and degrades gracefully when absent.
- Dialog remains compact and unclipped on large screen.

## Proof Required

- Tests for store persistence and selection.
- Browser screenshot of manage mode.
- Browser screenshot of recipient selection mode.
- Browser screenshot showing tags/favorite/profile cache.
- Execution report update for SB04.

## Browser Validation Logging

- Route: AccountApp home and send transaction dialog.
- Viewport: 1920x1080 or maximized large-screen browser.
- Actions: open address manager, favorite an address, tag it, switch own/external, select receiver, confirm populated receiver.
- Screenshots: `bundle://evidence/SB04/address-manager-large.png`, `bundle://evidence/SB04/address-select-send-large.png`.
- Review questions: Are cards square and scannable; are tags readable; does the dialog avoid accordion sprawl; is selection clear?

## Progression Gate

- SB05 and SB06 may depend on address directory only after manage/select flows and persistence tests pass.

## Suggested Agent Prompt

```text
Implement SB04 only. Build one generic address directory dialog for external and own addresses, integrate it into the listed flows, prove persistence and selection with tests, capture screenshots, and update the execution report.
```

