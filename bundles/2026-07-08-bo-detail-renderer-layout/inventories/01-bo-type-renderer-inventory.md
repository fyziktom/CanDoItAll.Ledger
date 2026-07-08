# BO Type And Renderer Inventory

## Current Default Component Renderers

| Renderer/provider | Current target | Notes |
|---|---|---|
| `RuleBusinessObjectRendererProvider` | Account-rule BO payloads | Priority 150; custom rule display. |
| `SettingsBusinessObjectRendererProvider` | Account settings BOs | Priority 100; settings-specific display. |
| `MediaBusinessObjectRendererProvider` | Any `IMediaBusinessObjectView` | Priority 90; currently catches typed family BOs including events/tickets/commerce if they implement media view contracts. |
| `GenericBusinessObjectRendererProvider` | Fallback | Priority minimum. |

## Typed BO Families

| Object type | Important fields | Desired first-tab behavior |
|---|---|---|
| `event` | `startsAt`, `endsAt`, `eventId`, `eventDate`, `eventClass`, `location`, `locationCoordinates`, `organizer`, `authorLink`, `videoLink`, `musicInLink`, `mintAuthorAddress`, `status` | No-DataItems overview should prioritize schedule, venue, organizer, status, and links. |
| `ticket` | `ticketNumber`, `eventId`, `eventAddress`, `eventDate`, `location`, `locationCoordinates`, `authorLink`, `ticketClass`, `ticketDuration`, `holder`, `seat`, `used`, `usedAt`, `status` | No-DataItems overview should prioritize access status and show related event card when found. |
| `payment` | `amount`, `currency`, `payer`, `payee`, `reference`, `status`, plus commerce payment record fields | No-DataItems overview should prioritize amount/status/parties and show target BO card when found. |
| `purchase` | `vendor`, `buyer`, `amount`, `currency`, `orderNumber`, `status`, plus commerce purchase record fields | No-DataItems overview should prioritize buyer/vendor/status and show target/delivered BO card when found. |
| `receipt` | `receiptNumber`, `amount`, `currency`, `paidBy`, `receivedBy`, `issuedAt`, plus commerce receipt record fields | No-DataItems overview should prioritize proof fields and show delivered/target BO card when found. |
| `post`, `image`, `music`, `message`, `product`, `invoice`, `profile` | Family-defined facts and optional DataItems | Use the same adaptive overview: gallery path when DataItems exist, fact-first path when they do not. |

