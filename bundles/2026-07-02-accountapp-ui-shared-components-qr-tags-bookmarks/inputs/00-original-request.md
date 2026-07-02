# Original Request

User asked to use `candoitall-bundle-workflow` for a large AccountApp UI and shared-component initiative.

## Raw Request Preservation

- Do detailed architecture design and whole plan.
- Use xlsx for detailed checklists and references.
- Review UX ideas from `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor`, but improve the implementation quality.
- Prefer dialogs over accordions for management details, including patterns visible on the VEBlazor home page.
- Main goal: tuning UI for the PWA WASM account app and isolating shared components for ledger-related apps.
- After login, the first screenshot shows card stacks moving the hero/workspace down. The login confirmation should show through `NotificationService`; other status/action surfaces should compress into one elegant top bar fixed to the top of the page frame.
- Buttons should be minimalistic icon buttons and use `TooltipService` labels.
- Use prepared `CanDoItAll.Components` in forms and dialogs, especially `TagsEditor`.
- Add a WASM tag occurrence service storing unique and general tag occurrence counts per BO in localStorage, with settings to choose the source for "often" suggestions.
- Tags must display through colored badges from `CanDoItAll.Components`; stored tags must remember color and guarantee readable text contrast.
- Improve profile placement: the profile button is currently near the bottom of the home page; it should become a card showing account name and icon/avatar, similar to `C:\Users\dell\OneDrive - TechnicInsider\Produkty\VEFramework\Media\Features\VENFT New MultiAccounts\Home-tab.png`.
- Analyze Microsoft QR guidance at `https://learn.microsoft.com/en-us/aspnet/core/blazor/security/qrcodes-for-authenticator-apps?view=aspnetcore-10.0`.
- Create a simple WASM PWA QR test at `C:\programovani\dotnet\qrcode-wasm-test`.
- If the Microsoft-recommended pure C# QR library works in WASM, add it to `CanDoItAll.Components` as new project `CanDoItAll.Components.QRCode`; otherwise use `AlexNek/Blazor.QrCode` or a better option.
- For QR reading, use `densen2014/ZXingBlazor`.
- Use the new QR library for address/subaddress dialogs, tx hash QR buttons, signed ownership hashes for BOs such as tickets, scan-address buttons in transaction and BO dialogs, and verification/ticket workflows.
- Design verification as a driver with a builder defining verification steps. Tickets add a Used-flag verification/update flow, but other BOs should be supported too.
- Improve bookmarks management through a reusable dialog showing contacts/bookmarked addresses as square cards with cached profile BO when available.
- Local storage or IndexedDB needs favorite flags and user tags for addresses.
- The address dialog must be generic so it can manage external bookmarks, own subaccounts, receiver selection in sends, minting recipient selection, gallery navigation, and subaccount selection.
- Settings stored in localStorage or IndexedDB must be exportable/importable through a separate file/zip backup dialog; home page exposes it as a button only.
- Do real app testing and capture visual screenshots. Analyze screenshots against the request for available-space use, minimized scrolling, no overlay/overflow, and visual correctness. Repair and retest until correct.
- Avoid custom CSS where prepared components are available.
- Focus now on large screens only; do not tune small or medium screens now.

## Supplied Artifacts

- Current AccountApp screenshot: `bundle://inputs/artifacts/accountapp-after-login-current.png`
- VENFT profile/home reference image: `bundle://inputs/artifacts/venft-profile-home-reference.png`
