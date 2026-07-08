# C# Testability Plan

## Characterization Tests

- Existing gallery tests remain in `BusinessObjectCommerceUiComponentTests`.
- Existing long-description and ticket prefill tests remain as regression coverage.

## Isolated Unit/Component Tests

- Add `BusinessObjectDetailRendererLayoutTests`.
- Render `BusinessObjectLedgerDetailsDialogContent` with:
  - event BO without DataItems
  - ticket BO with related event in workspace
  - payment/purchase/receipt BO with related sold/delivered BO in workspace
  - media BO with DataItems
- Render or resolve renderer providers directly if needed.

## Negative Tests

- Empty no-DataItems BO must not render `cda-bo-selected-preview` or empty DataItems rail.
- Media BO with DataItems must still render the selected preview.
- Unresolved related BO id must not render a broken related card.

## Integration/Composition Smoke

- Build AccountApp.
- Browser-open AccountApp large-screen route and capture screenshots for gallery and detail dialog.
- Docker rebuild and open rebuilt AccountApp route.

## Fake-Separation Checks

- No new partial class.
- No new project reference.
- No duplicated full gallery-card markup in detail dialog.
- Provider resolution stays outside `BusinessObjectRendererHost`.

