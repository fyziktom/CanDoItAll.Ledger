# Normalized Requirements

| ID | Requirement | Acceptance Criteria | Primary Subbundle |
| --- | --- | --- | --- |
| R01 | Replace post-login card stack with notification-backed unlock state and one compact sticky top frame bar. | Successful unlock/create/import shows a notification, not a page card; top frame keeps account, endpoint, signing, and primary actions in one compact row; screenshot shows workspace starts materially higher. | SB02 |
| R02 | Use icon/minimal action buttons with tooltip labels. | Top shell and reusable dialog actions use BaseLib button/tooltip patterns; text labels appear in tooltips or compact accessible labels; no major action lacks tooltip text. | SB02 |
| R03 | Prefer prepared CanDoItAll components for forms/dialogs. | New or touched forms use BaseLib dialog, toolbar, button, tag editor, badge, picker, and notification components unless a documented gap requires local markup. | SB01, SB02, SB03, SB04, SB05 |
| R04 | Move account/profile identity to a top profile card/identity region. | The account name, address, avatar/icon, profile edit entry, and copy/QR actions are visible near the top identity surface; old bottom-only profile entry is removed or secondary. | SB02 |
| R05 | Add shared QR generation components. | `CanDoItAll.Components.QRCode` provides QR button/dialog/rendering based on the WASM-proven generator; AccountApp consumes it through shared components, not local QR markup. | SB01 |
| R06 | Add shared QR scanner components. | QR scan button/dialog use ZXingBlazor-backed reader abstractions and expose scanned payloads to transaction, BO send, and verification flows. | SB01, SB06 |
| R07 | Use QR generation for address/subaddress, tx hash, and signed ownership hash. | Address and tx hash surfaces open QR dialogs; ticket/BO ownership dialogs show real QR codes instead of mocks/placeholders. | SB06 |
| R08 | Build verification as a reusable driver/builder. | Verification steps are defined through a builder; BO ownership, ticket ownership, and ticket Used-state checks are composable; tests cover positive and negative outcomes. | SB06 |
| R09 | Implement browser tag occurrence service. | localStorage-backed service tracks general and unique BO tag occurrence by account and BO identity; settings choose suggestion source. | SB03 |
| R10 | Use shared tag editor and colored badges. | BusinessObject creation/edit/details use BaseLib `TagEditor` and badge components; tag color persists; text contrast is readable. | SB03 |
| R11 | Add generic address manager dialog. | One reusable dialog supports external bookmarks, own subaccounts, search, select/manage modes, favorites, user tags, and cached profile BO display as square cards. | SB04 |
| R12 | Reuse address manager in multiple flows. | Home management, send transaction recipient selection, BO send/mint recipient selection, gallery navigation target selection, and subaccount selection can use the same component contract. | SB04 |
| R13 | Add settings backup/import/export dialog. | Browser-local AccountApp settings, tag stats, address directory, and UI preferences can export as file/zip and import with preview and conflict handling. | SB05 |
| R14 | Avoid accordion-heavy management surfaces. | Settings, bookmarks/address manager, backup, and verification detail flows open dialogs; no new large inline accordion management panels are added. | SB02, SB04, SB05, SB06 |
| R15 | Prove visual quality with large-screen browser validation. | Each UI subbundle captures large-screen screenshots, logs viewport/routes/actions, reviews available space, scroll reduction, overflow, tooltip/dialog behavior, and repairs failures before closure. | SB07 |
| R16 | Preserve existing ledger behavior. | Existing build/tests pass; account unlock, refresh, send, BO details, ticket verification, and backup flows remain functional after UI changes. | SB07 |
