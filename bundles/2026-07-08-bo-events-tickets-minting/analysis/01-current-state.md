# Current State

## VEF Reference Summary

- `EventSpecificMintForm.razor` presents event class choices from `ClassOfNFTEvent`, a date-time `DatePicker`, location, location coordinates, and author link. It emits changed values through callbacks.
- `TicketSpecificMintForm.razor` presents date-time `DatePicker`, location, location coordinates, author link, ticket class from `ClassOfNFTTicket`, ticket duration from `DurationOfNFTTicket`, and an event NFT UTXO loader.
- `EventDetails.razor` renders location, coordinates, date time, author link, and class.
- `TicketDetails.razor` renders location, coordinates with map link, date time, author link, class, duration, mint author address, used state, and the use-ticket action.
- Neblio account code includes minting and use-ticket flows, including `UseNFTTicket` and subaccount minting helpers.

## Ledger Current State

- `BusinessObjectFamilyCatalog` already defines event and ticket field keys such as `eventDate`, `eventClass`, `location`, `locationCoordinates`, `authorLink`, `mintAuthorAddress`, `ticketClass`, `ticketDuration`, `used`, `usedAt`, and `status`.
- `BusinessObjectCreationWizard.razor` currently renders all typed fields with a generic loop. `DateTime`, `Status`, `Boolean`, `Address`, and `Url` kinds mostly become text boxes.
- The wizard already has ticket event selection and multimint panels. These must be preserved.
- `BusinessObjectTicketEventPrefill` already copies event identity, date, venue, coordinates, and author link into ticket fields.
- `TypedFamilyBusinessObjectRenderer.razor` renders typed facts generically. Details dialog tests already assert event and ticket facts appear and related event cards work.

## BaseLib/Shared Component Discovery

- Components MCP was attempted but failed with `Transport closed`.
- Local usages show `Grid`, `Stack`, `FormField`, `TextBox`, `TextArea`, `DropDown`, `Button`, `Alert`, `AvailableTokenSelector`, and other BaseLib components already used in the wizard and nearby components.
- No local BaseLib calendar component usage was found during preparation. Execution should prefer an available BaseLib date/time picker if discoverable; otherwise use native `datetime-local` inside `FormField` as a scoped fallback and document why.

## Initial Implementation Direction

- Add event/ticket-specific rendering in `BusinessObjectCreationWizard.razor` for selected object types.
- Convert date-time fields to a date-time input helper bound to ISO UTC-ish payload values.
- Convert category/status fields to dropdowns backed by fixed domain options.
- Autofill `mintAuthorAddress` from `Workspace.Address.Value` when creating event/ticket payloads.
- Keep typed renderer compatible by preserving field keys and values.
