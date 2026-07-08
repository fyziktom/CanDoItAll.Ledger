# C# Current State Inventory

## Source Files Inspected

See `bundle://analysis/01-current-state.md`.

## Large Classes And Partial Classes

- `BusinessObjectLedgerDetailsDialogContent.razor` is a large cohesive UI component for the detail dialog. It owns overview layout, action tab, ticket tab, ledger details tab, data item gallery behavior, sale panel, tag recording, and dialog actions.
- `BusinessObjectLedgerGallery.razor` embeds reusable card logic directly in the gallery.
- No partial class expansion is planned.

## Constructor Dependency Counts

- Razor components primarily use injected services:
  - `BusinessObjectLedgerDetailsDialogContent`: `DialogService`, `NavigationManager`, `NotificationService`, `ITagOccurrenceService`, `IBusinessObjectVerificationService`.
  - `BusinessObjectRendererHost`: `IServiceProvider` to resolve renderer registry, with fallback default registry.

## Direct Instantiation Points

- Renderer providers create `BusinessObjectRendererDescriptor` instances.
- The dialog opens itself from `BusinessObjectLedgerGallery`.
- Tests instantiate `BusinessObjectLedgerDetailsDialogContent` directly with bUnit.

## Responsibilities

- UI projection: `CanDoItAll.Economy.Components`.
- Domain payload/casting/commerce contracts: `CanDoItAll.Economy.BusinessObjects`.
- Composition: service extensions in `CanDoItAll.Economy.Components.Rendering` and app/node registration.

## Current Tests

- Commerce component behavior: `BusinessObjectCommerceUiComponentTests`.
- Long description detail rendering: `BusinessObjectLongDescriptionComponentTests`.
- Ticket event prefill and ticket wizard: `BusinessObjectTicketMintingComponentTests`.
- Renderer/casting extension seam: `BusinessObjectCastingTests`.

## Missing Tests

- No test proves the detail Overview hides empty DataItems hero.
- No test proves a related event/target BO card appears inside the detail Overview.
- No test proves gallery and related-card markup use the same component.
- No test proves typed/commerce renderer provider resolution.

## Risk Notes

- Because `MediaBusinessObjectRendererProvider` catches all `IMediaBusinessObjectView`, typed/commerce renderer providers must have higher priority.
- Moving too much logic into `BusinessObjectLedgerDetailsDialogContent` would worsen the large component. Extract reusable components instead.

