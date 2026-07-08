# Assumptions And Risks

## Assumptions

- The first-tab change should apply to the existing AccountApp BO details dialog, not only the separate inspector dialog.
- "Same card as BO gallery page" means shared markup/component behavior, not a visually similar duplicate.
- Large-screen proof is sufficient for this request. Small and medium responsive layout work is explicitly out of scope.
- Related BOs can only be shown when the current `BusinessObjectWorkspaceModel` contains the related object.
- If a related object cannot be resolved from the workspace, the detail view should still show the related id/facts and avoid a broken card.

## Critical Path Risks

- Reusing the gallery card could accidentally change gallery ordering, filtering, hidden commerce records, or card text.
- A naive no-DataItems layout could hide fields already available through `TypedFacts`.
- Commerce records may have several possible related ids (`TargetObjectId`, `DeliveredObjectId`, `PaymentObjectId`, `ReceiptObjectId`); resolution order must be explicit.
- Ticket `eventId` is a BO id, not an outpoint, in the current wizard/test model.

## Validation Risks

- ImageGen is a planning aid only. The implementation must be judged against actual rendered browser screenshots.
- Browser proof requires seeded or created BOs of the relevant types; component tests alone do not prove the visual dialog.
- Docker rebuild can be slow and may be affected by sibling checkout availability for `../CanDoItAll.Components` and `../CanDoItAll/ExternalPackages`.

## Reopen Triggers

- The gallery page loses visible cards or hidden commerce artifacts become visible in the main gallery.
- The media BO path loses its DataItems hero or preview strip.
- A no-DataItems BO still renders the empty preview hero or empty DataItems rail.
- Related BO cards use duplicated markup instead of the shared gallery card.
- Dockered AccountApp fails to load the rebuilt detail dialog.

