# Assumptions And Risks

## Working Assumptions

- Event and ticket BOs are represented by `BusinessObjectFamilyCatalog` typed families and payload `Fields`.
- The account address that owns the current `BusinessObjectWorkspaceModel` is the minting address for create-mode BO staging.
- The request is about the BO mint wizard and detail rendering, not Neblio transaction protocol compatibility.
- Ticket event prefill should continue to overwrite event-derived fields except existing ticket status.

## Critical Path Risks

- A date-time control could store a local-time string that breaks existing `eventDate` tests or renders unclearly.
- Autofill could be applied to edit/update flows, incorrectly changing historical mint author metadata.
- Replacing the generic field loop could accidentally hide fields needed by non-event/ticket families.
- Component MCP unavailability could leave a better BaseLib date-time picker undiscovered.

## Validation Risks

- bUnit can prove markup and staged payloads but not final browser calendar popover behavior.
- Browser validation may be blocked if the sample app route or data seeding is unavailable in the current environment.
- If native `datetime-local` fallback is used, visual proof must explicitly call out that no BaseLib calendar picker was found locally.

## Reopen Triggers

- Reopen SB01 if tests show event/ticket fields still render as plain generic text controls for date-time or category fields.
- Reopen SB01 if `mintAuthorAddress` is missing or not equal to `Workspace.Address.Value` in create drafts.
- Reopen SB02 if detail renderer facts drop VEF-relevant fields such as ticket duration, used state, or mint author address.
- Reopen the bundle if the components MCP becomes available and exposes a required BaseLib date-time picker that should replace a fallback control.
