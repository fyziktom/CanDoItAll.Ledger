# C# Boundary Map

## Target Projects

- `repo://src/CanDoItAll.Economy.Components/CanDoItAll.Economy.Components.csproj`
- `repo://tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj`

## Target Types And Components

| Type/component | Responsibility |
|---|---|
| `BusinessObjectGalleryCard.razor` | Shared gallery-card projection for gallery deck and related BO previews. |
| `BusinessObjectLedgerDetailsDialogContent.razor` | Dialog shell, tabs, actions, and overview composition handoff. |
| New typed/commerce renderer providers | Resolve event/ticket/commerce family renderers before generic media renderer. |
| New typed/commerce renderer components | Focused detail renderers for family and commerce facts. |

## Contracts Vs Implementations

- Domain contracts stay in `CanDoItAll.Economy.BusinessObjects`.
- UI renderer providers and components stay in `CanDoItAll.Economy.Components`.
- No new public domain contract is required for related BO resolution; use `IBusinessObjectView`, `ITypedBusinessObjectView`, and `ICommerceBusinessObjectView`.

## Composition Root Responsibilities

- `AddBusinessObjectComponentRenderers` registers default renderer providers.
- External apps may still add custom `IBusinessObjectRendererProvider` implementations.

## Old Responsibilities To Remove Or Reduce

- Remove duplicated gallery card markup from `BusinessObjectLedgerGallery.razor`.
- Reduce detail dialog first-tab conditional markup by moving card reuse and helper behavior into components/methods.

## Temporary Bridges

- Existing CSS classes remain in use but no new custom CSS is added.
- Existing `cda-vef-panel` and `cda-economy-fact` classes are allowed as existing design-system classes.

