# Requirement Traceability

| Raw Note | Requirement IDs | Subbundle | Source Proof | Closure Proof |
| --- | --- | --- | --- | --- |
| Screenshot card stack moves workspace down. | R01, R02, R15 | SB02, SB07 | `bundle://inputs/artifacts/accountapp-after-login-current.png`, `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` | Large-screen before/after screenshots and visual review. |
| Unlock confirmation should be notification. | R01 | SB02 | `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`, `repo://examples/CanDoItAll.Economy.AccountApp/Layout/MainLayout.razor` | Notification proof and no success card in DOM/screenshot. |
| Buttons should be minimal with icons and tooltips. | R02, R03 | SB02 | BaseLib tooltip/button sources and workspace action source. | Tooltip hover/click proof and screenshot review. |
| Use prepared components, especially forms/dialogs and tags. | R03, R10 | SB01, SB03 | BaseLib TagEditor/Badge/Dialog references and BO wizard source. | Source diff and component tests/browser proof. |
| Tags need unique/general occurrence counters and settings. | R09, R10, R13 | SB03, SB05 | BO wizard/details sources and browser cache source. | Service tests, settings UI proof, storage export proof. |
| Tag badge colors should persist and maintain contrast. | R10 | SB03 | BaseLib badge sources. | Reload test and contrast/color assertions. |
| Profile button belongs in account/name/avatar card. | R04 | SB02 | Workspace source and VENFT reference image. | Screenshot showing top identity region. |
| QR code library must be tested in WASM and added as shared project if successful. | R05 | SB01 | `bundle://evidence/qr-wasm-test/Home.razor`, Microsoft QR guidance. | Build proof and shared project compile proof. |
| QR reader should use ZXingBlazor. | R06 | SB01, SB06 | ZXingBlazor repository reference. | Scanner component compile proof and mocked/manual scan proof. |
| QR should cover addresses, tx hashes, ownership hashes, scans, and verification. | R07, R08 | SB06 | BO details, tx details, transaction composer, ticket verification sources. | Positive/negative verification tests and browser proof. |
| Bookmarks need generic address management dialog. | R11, R12 | SB04 | AddressBookmarkManager, AddressBookPicker, workspace adapter sources. | Dialog screenshot, selection/manage tests, flow integration proof. |
| Settings in localStorage/IndexedDB need backup/import/export. | R13 | SB05 | Browser cache/session/settings dialog sources. | Export/import tests and dialog screenshot. |
| Prefer dialogs over accordions. | R14 | SB02, SB04, SB05, SB06 | VEBlazor references and current AccountApp sources. | Screenshot proof showing management surfaces in dialogs. |
| Real app testing and visual validation required. | R15, R16 | SB07 | All implementation subbundle proof. | Execution report, screenshots, build/tests, workbook closure. |
