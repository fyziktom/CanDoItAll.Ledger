# Structured Input

## Primary Objective

Tune the AccountApp WASM PWA large-screen user interface and extract reusable ledger components/services so related ledger apps can share QR, tag, address, settings-backup, and verification capabilities.

## Non-Negotiable Constraints

- Large-screen desktop quality is the current target.
- Prepared `CanDoItAll.Components` should be used before local CSS or hand-built controls.
- Routine login/unlock confirmations must move to `NotificationService`.
- Top-level post-login status/action cards must be compressed into a sticky top workspace bar.
- Details and management flows should open dialogs instead of expanding accordions.
- Icon-only or icon-forward action buttons need `TooltipService` labels.
- QR generation must be proven in Blazor WASM before implementation.
- QR reader must be based on `ZXingBlazor` unless execution finds a stronger, documented reason to change.
- Settings stored in browser storage need import/export.
- Every browser-visible change needs screenshot proof and visual review before closure.

## Required Functional Areas

1. AccountApp shell and top command surface.
2. Shared QR generation and scanning components.
3. Tag occurrence service, tag suggestions, color persistence, and badge rendering.
4. Generic address manager dialog for external bookmarks and own subaccounts.
5. Browser settings backup/import/export dialog.
6. Verification driver/builder for BO ownership, ticket ownership, and ticket Used-state checks.
7. Full large-screen browser validation and closure report.

## Current QR Decision

The Microsoft-recommended `Net.Codecrete.QrCodeGenerator` library builds and renders SVG successfully in a Blazor WASM PWA test at `C:\programovani\dotnet\qrcode-wasm-test`. This bundle therefore plans a new `CanDoItAll.Components.QRCode` project using that package for generation and `ZXingBlazor` for scanning.

## Out Of Scope For This Bundle

- Small and medium responsive tuning.
- Rewriting AccountApp navigation architecture from scratch.
- Replacing working account menu surfaces that are not part of the requested UI tuning.
- Copying VEBlazor implementation details wholesale.
