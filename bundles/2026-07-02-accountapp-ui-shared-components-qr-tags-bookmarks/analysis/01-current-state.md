# Current State Analysis

## AccountApp Shell

- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` renders the post-login workspace as a page topbar, unlocked-vault status strip, optional alert, account bar, and action row before the actual economy workspace. The supplied screenshot confirms these stacked cards push the useful workspace down.
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` records status messages locally instead of routing successful unlock/login confirmations through `NotificationService`.
- `repo://examples/CanDoItAll.Economy.AccountApp/Layout/MainLayout.razor` already hosts `DialogHost` and `Notification`, so the UI foundation for dialogs and notifications exists.
- `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css` contains local card and button styling for the current shell. Some local CSS will remain necessary for page framing, but execution must first try BaseLib layout, toolbar, button, tooltip, dialog, and notification components.

## Economy Workspace

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` already has a dense in-app workspace, account metrics, send/token/security/settings/refresh/subaccount actions, balances, token sources, and recent transactions.
- Profile editing is exposed from a lower action strip. The requested profile/identity treatment belongs near the top identity surface with name, avatar/icon, address copy, and related account actions.
- QR affordances are currently placeholders or absent. `BusinessObjectLedgerDetailsDialogContent.razor` includes QR mock UI for tickets and placeholder QR buttons. `TransactionDetailsDialogContent.razor` shows transaction hashes but has no QR dialog action.

## Tags

- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` uses a simple textbox and manual `#tag` rendering.
- BaseLib already provides `TagEditor`, `Badge`, and `StatusBadge` components. The tag implementation should replace custom entry/rendering where practical and should persist tag metadata in browser storage for consistent color and suggestions.
- VEBlazor's `SpaceTags.razor` shows useful behavior-level ideas around tag options and badge-like display, but it relies on older/global patterns that should not be copied directly.

## Bookmarks And Addresses

- `AddressBookmarkManager.razor` is a simple inline manager and `AddressBookPicker.razor` is a picker wrapper. The requested reusable square-card dialog requires a richer address directory model with profile cache, favorites, user tags, own/external classification, search, and selection mode.
- `EconomyAccountWorkspaceAdapters.cs` currently derives address bookmarks from transactions. That can seed the new directory store, but it is not sufficient for favorites, tags, profile BO cache, or subaccount management reuse.

## Settings Backup

- `BrowserLocalStorageAccountWorkspaceCache.cs` provides string cache storage in localStorage.
- `AccountAppSession.cs` can export encrypted account backup JSON, but settings backup/import/export is not a separate browser-settings backup dialog and does not cover settings/address/tag directory state.

## QR Feasibility

- A Blazor WASM PWA test was created at `C:\programovani\dotnet\qrcode-wasm-test`.
- `Net.Codecrete.QrCodeGenerator` version 3.0.0 builds under `net10.0` and renders SVG in the browser.
- Proof artifacts were copied into `bundle://evidence/qr-wasm-test/`.

## Visual Findings From Supplied Screenshots

- The AccountApp screenshot shows too much vertical ceremony before the workspace: topbar, vault confirmation, account info, and four text-heavy buttons.
- The desired large-screen direction is a fixed top workspace strip with account identity, endpoint/account state, compact actions, and notifications for transient success.
- The VENFT profile reference supports moving account/profile identity into a top card-like identity region, but the new implementation should avoid duplicating VEBlazor accordions and older layout density problems.
