# SB02 Domain Renderers And Related Cards

## Status

Closed

## Objective

Add typed/commerce renderer coverage and related BO resolution/cards for event, ticket, payment, purchase, and receipt BOs.

## Covered Inputs

- Events, tickets, payments, receipts, and purchases need better renderer/display support.
- Related BOs should appear in the first tab when available.

## Prerequisites

- SB01 completed with shared gallery card and adaptive layout proof.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Rendering/ServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Economy.Components/Rendering/MediaBusinessObjectRendererProvider.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`

## Deliverables

- Default provider/renderer support for commerce BOs.
- Default provider/renderer support for typed family BOs.
- Related event/target/delivered BO lookup in detail overview.
- Tests for ticket-event and commerce-target related cards.

## Dependency Impact

Critical foundation for visual/browser proof in SB03.

## Validation Depth

Critical semantic gate with positive and negative tests.

## Implementation Steps

1. Add provider(s) with priorities above media fallback.
2. Add renderer components using BaseLib layout and existing fact components/classes.
3. Add related BO lookup for ticket and commerce records from `Workspace.BusinessObjects`.
4. Render related BO with shared gallery card.
5. Add tests for resolved and unresolved related BO cases.

## Scope Exceptions

Related BOs not present in the current workspace are not fetched over the network in this bundle.

## Do Not Do

- Do not add network/API calls to resolve related objects.
- Do not change commerce payload formats.
- Do not make payment/purchase/receipt visible in the main gallery.

## Acceptance Checklist

- [x] Event/ticket/payment/purchase/receipt facts are prominent.
- [x] Ticket detail shows related event card when event exists in workspace.
- [x] Payment/purchase/receipt detail shows target or delivered BO card when present.
- [x] Renderer registry resolves typed/commerce providers before media fallback.

## Proof Required

- Focused component tests.
- Provider-resolution test.
- Source assertion for relationship candidate resolution order.
- Anti-stub audit.

## Browser Validation Logging

Record in SB03.

## C# Architecture Impact

Uses existing provider strategy extension point.

## Boundary Ownership

Renderers and relation card projection stay in UI project. Domain records stay in BusinessObjects.

## Dependency Direction

No new references.

## Pattern Decision

Existing `IBusinessObjectRendererProvider` strategy.

## Testability Contract

Tests instantiate providers/render components without full app construction.

## Partial Class Policy

No partial classes.

## Architecture Proof Required

Provider priority/source assertions and direct tests.

## Progression Gate

Passed. Renderer/related-card tests and adjacent component tests passed.

## Closure Proof

- `bundle://proof/SB02/transcripts/adjacent-component-tests.txt`
- `bundle://proof/SB03/transcripts/source-assertions.txt`
- `bundle://evidence/playwright/ticket-related-no-data-detail-large.png`

## Suggested Agent Prompt

Implement SB02 only. Add default typed/commerce renderer coverage and related BO cards through the existing provider strategy and the shared gallery card.
