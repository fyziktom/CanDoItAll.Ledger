# Normalized Requirements

| Id | Requirement | Acceptance criteria |
|---|---|---|
| RQ-01 | Preserve raw request and generated visual proposal. | Bundle contains raw request, image prompt, proposal image, and reference screenshots. |
| RQ-02 | Replace empty DataItems hero with adaptive first-tab layout. | For BOs with `DataItems.Count == 0`, the Overview tab does not render the selected-preview hero or DataItems rail. |
| RQ-03 | Preserve media/DataItems behavior. | For BOs with one or more DataItems, the Overview tab still shows the large preview and selectable data item list. |
| RQ-04 | Render typed BO facts prominently for no-DataItems objects. | Event, ticket, payment, purchase, and receipt BOs show key typed/commerce facts in the first tab without requiring a separate tab. |
| RQ-05 | Show related BO cards when resolvable. | Ticket detail resolves `eventId`; commerce detail resolves target/delivered BO ids. Resolved related BOs render with the shared gallery-card component. |
| RQ-06 | Reuse gallery card markup. | `BusinessObjectLedgerGallery` and related-object detail previews use the same component for BO cards. |
| RQ-07 | Improve default renderer coverage. | Default component renderer providers include typed-family and commerce-specific paths so event/ticket/commerce BOs no longer fall through only to the generic/media detail renderer. |
| RQ-08 | Avoid custom CSS. | No new CSS selectors/files are added for this work; layout uses BaseLib components, existing classes, and Tailwind utility classes where needed. |
| RQ-09 | Test and prove behavior. | Component tests cover empty DataItems layout, media layout preservation, related ticket-event card, related commerce card, and gallery-card reuse. |
| RQ-10 | Large-screen browser proof. | A large-screen screenshot captures the implemented dialog and is reviewed against `bundle://evidence/imagegen/bo-detail-layout-proposal.png`. |
| RQ-11 | Docker validation. | Dockered AccountApp/node images are rebuilt and a rebuilt AccountApp route is opened successfully with a screenshot. |

