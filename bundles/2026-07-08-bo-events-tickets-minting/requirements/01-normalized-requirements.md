# Normalized Requirements

| Requirement | Raw notes | Acceptance criteria | Owning subbundle |
| --- | --- | --- | --- |
| `R001` | `N001`, `N003` | Event and ticket BO wizard fields use domain-specific rendering instead of only the generic text-loop experience. | `SB01` |
| `R002` | `N002`, `N004` | Event/ticket date-time fields use a date-time picker/input pattern through BaseLib/shared form structure, and payload values remain parseable. | `SB01` |
| `R003` | `N002`, `N005` | Event class, ticket class, ticket duration, and status-like fields use predefined dropdown choices. | `SB01` |
| `R004` | `N006` | Create-mode event and ticket drafts write `mintAuthorAddress` from `Workspace.Address.Value`; edit mode does not silently replace it. | `SB01` |
| `R005` | `N002`, `N003` | Ticket event selection and multimint continue to work with the improved ticket form. | `SB01` |
| `R006` | `N002`, `N003` | Detail renderer output continues to show VEF-relevant event/ticket facts, including ticket duration, used state, and mint author address when present. | `SB02` |
| `R007` | `N001`-`N006` | Final closure records tests, source assertions, anti-stub audit, browser validation or explicit blocker, and raw-note closure. | `SB02` |

## Literal Scope Notes

- `must be autofilled` is preserved as a required create-flow behavior for event and ticket minting.
- `some predefined categories` is narrowed to fields already present in the current typed family catalog and VEF forms: `eventClass`, `ticketClass`, `ticketDuration`, `status`, and `used`.
- `calendar picker` is planned through BaseLib/shared component discovery first. If unavailable locally and the MCP remains unavailable, execution must document the fallback and proof.
