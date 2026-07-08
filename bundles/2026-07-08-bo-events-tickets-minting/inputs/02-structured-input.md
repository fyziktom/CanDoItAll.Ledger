# Structured Input

## Raw Notes

| Raw note | Exact wording | Normalized target |
| --- | --- | --- |
| `N001` | `improve BO events and tickets` | Improve the BO event/ticket minting and detail experience, not unrelated BO families. |
| `N002` | `analyze how events and tickets works in ... VEDriversLite\Neblio and ... VEBlazor\Components\NFTs\EventsAndTickets` | Use the VEF Neblio and Blazor implementations as source references for required fields, date-time behavior, categories, details, and ticket usage semantics. |
| `N003` | `they have specific minting form and renderers of details. our minting forms are not well done` | Replace the generic event/ticket field experience with domain-specific controls while keeping renderer/details compatibility. |
| `N004` | `use more from our CanDoItAll.Components.BaseLib as calendar picker for setup of date and time` | Use existing BaseLib/shared controls and local patterns for date-time inputs instead of plain text. |
| `N005` | `then some predefined categories of tickets or events as dropdowns` | Use predefined dropdown options for event class, ticket class, ticket duration, and ticket status-like fields. |
| `N006` | `mint author address must be autofilled based on from what address is it minting` | Populate `mintAuthorAddress` from the current workspace/minting address for event and ticket payloads before staging create drafts. |

## Hard Constraints

- Preserve existing ticket event picker and multimint behavior.
- Preserve typed family payload keys unless the bundle is repaired with a migration plan.
- Do not create a separate one-off event/ticket wizard outside `BusinessObjectCreationWizard`.
- Prefer BaseLib components and existing local shared CSS/classes over new page-local structural CSS.

## Validation Expectations

- bUnit tests prove control rendering and staged payload fields.
- Tests prove `mintAuthorAddress` is taken from `Workspace.Address` for create flow.
- Source assertions prove VEF-inspired fields are represented in production code.
- UI proof uses browser validation if a runnable app route is available in this turn; otherwise record an explicit validation gap.
