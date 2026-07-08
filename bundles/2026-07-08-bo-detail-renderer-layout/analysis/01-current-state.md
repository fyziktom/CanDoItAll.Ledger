# Current State

## Source Files Inspected

- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectRendererHost.razor`
- `repo://src/CanDoItAll.Economy.Components/Rendering/*`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/Renderers/*`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCasting.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItems.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCommerceUiComponentTests.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs`

## Observed Behavior

- `BusinessObjectLedgerDetailsDialogContent` renders the Overview tab directly.
- The Overview tab always reserves a large left preview/hero slot for the selected DataItem.
- When `DataItems.Count == 0`, the left hero shows an icon and "No data items are attached" while the useful facts remain constrained to the right side.
- A DataItems rail is still shown for empty BOs.
- The gallery card markup is embedded in `BusinessObjectLedgerGallery.razor`, so related-object previews would currently require duplication.
- The renderer registry exists and supports default and custom renderer providers.
- Default renderers currently include generic, media, settings, and account-rule renderers.
- The family catalog already defines typed fields for `event`, `ticket`, `payment`, `purchase`, `receipt`, `invoice`, `product`, `profile`, `music`, `message`, `post`, and `image`.
- Commerce BOs are cast as `ICommerceBusinessObjectView` with payment, purchase, and receipt records plus merged display facts.
- Ticket event prefill stores the related event object id in the ticket `eventId` field and the event owner address in `eventAddress`.

## Component MCP Evidence

- `Grid`: recommended for page/section shells with explicit tracks and responsive structure.
- `Stack`: recommended for one-dimensional flows.
- `InspectorDialogLayout`: available for modal/inspector layouts but not necessary for this existing Radzen dialog content.
- `StatsGrid`: only appropriate for metric-first rows; this detail overview is fact/context-first, so use `Grid` and `Stack` instead.

## CodeAnalytics Evidence

- Snapshot attempt with absolute solution path: `snap-20260708151116-e22e1421-failed`.
- Result: blocked by SDK/global.json resolution in the MCP host.
- Diagnostic: the MCP resolved `C:\repositories\CanDoItAll\global.json` and required SDK `10.0.200`.
- Fallback: exact source inspection, component MCP, component tests, build, browser screenshots, and docker validation.

