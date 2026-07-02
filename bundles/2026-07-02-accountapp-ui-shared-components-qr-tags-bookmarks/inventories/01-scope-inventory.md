# Scope Inventory

## Existing AccountApp Surfaces

| Surface | Source | Current State | Target |
| --- | --- | --- | --- |
| Post-login shell | `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` | Stacked status/account/action cards. | Sticky compact top command strip. |
| Session status | `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` | Local page status message. | `NotificationService` for success, visible errors for failures. |
| Notification host | `repo://examples/CanDoItAll.Economy.AccountApp/Layout/MainLayout.razor` | Already present. | Use intentionally for unlock/refresh/background success. |
| Economy workspace | `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` | Dense workspace with top metrics/actions and bottom profile/backup. | Profile identity and reusable icon actions moved top. |

## Shared Component Candidates

| Need | Existing Component | Reuse Decision |
| --- | --- | --- |
| Toasts | `Notification` | Use for unlock and successful background operations. |
| Tooltips | `TooltipTarget` / `TooltipService` | Use for icon/minimal buttons. |
| Top command layout | `Toolbar`, `PageHeaderActionButton`, `PageScaffold` | Use for dense top strip and action groups. |
| Dialog shell | `DialogScaffold`, `PickerDialogShell`, `Dialog` | Use for QR, address manager, settings backup, and verification. |
| Tags | `TagEditor` | Use for BO create/edit and address user tags. |
| Badges | `Badge`, `StatusBadge` | Use for tag display, counts, source/unique badges, verification status. |

## QR Library Decision

| Candidate | Role | Finding | Decision |
| --- | --- | --- | --- |
| `Net.Codecrete.QrCodeGenerator` | QR generation | Pure C# library recommended by current Microsoft Blazor QR guidance; WASM PWA test builds and renders SVG. | Use for generation in `CanDoItAll.Components.QRCode`. |
| `AlexNek/Blazor.QrCode` | QR generation fallback | Possible fallback if generator fails later. | Keep as fallback only. |
| `densen2014/ZXingBlazor` | QR reading | Purpose-built Blazor QR/barcode reader. | Use for scanner components. |

## VEBlazor References

| Reference | Useful Principle | Implementation Caution |
| --- | --- | --- |
| `SpaceTags.razor` | Tag options and badge-like display are useful UX ideas. | Do not copy global/static tag patterns. |
| `AccountProfile.razor` | Profile identity should be visible near home/account top. | Rebuild with BaseLib components and AccountApp state model. |
| Home tab image | Account/avatar card improves orientation. | Avoid repeating accordion-heavy management layout. |

## Required Evidence Inventory

- QR WASM build and browser screenshot proof.
- Source diffs for each subbundle.
- `dotnet build` and targeted test proof.
- Large-screen Playwright screenshots for affected routes/dialogs.
- Visual review notes in `reviews/01-execution-report.md`.
- Workbook checklist updates.
