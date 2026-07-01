# Structured Input

## User Goals

- Add multiminting to BO minting, especially event tickets.
- Support minting to own account, subaccount, another account, or a list of recipients.
- Use VEFramework UX/process as source material, not as implementation quality target.
- Validate with a small real run, preferably to a subaccount or equivalent target.

## Non-Goals

- Do not mint very large batches during validation.
- Do not implement persistent browser subaccount vault creation in this bundle.
- Do not create a separate Ledger transaction/signing path.

## Acceptance Signals

- Ticket wizard exposes a discoverable multimint section.
- Batch preview shows recipient and count before broadcast.
- Each ticket has unique BO id and ticket-specific payload data.
- WASM and SSR submission paths respect intended owner addresses.
- Browser proof shows the flow and ledger proof confirms the target account owns the created tickets.
