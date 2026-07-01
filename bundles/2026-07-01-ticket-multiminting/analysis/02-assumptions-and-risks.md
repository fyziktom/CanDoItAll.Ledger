# Assumptions And Risks

## Working Assumptions

- A multimint batch means multiple create transactions, not one aggregate transaction.
- For ticket batches, the common event fields and uploaded data items are reused, while object id and ticket number are made unique per ticket.
- Existing `Workspace.SubAccounts` are valid targets when populated by SSR/development sessions.
- In the browser AccountApp, address-book/manual recipient targeting is the practical validation path until persisted subaccounts exist.

## Critical Path Risks

- Token-source UTXO exhaustion can reject a batch if the user selects a non-main creation asset without enough reusable sources.
- Some environments have no subaccounts; UI must hide/disable that mode instead of failing.
- Submitting too quickly can contend with UTXO changes; sequential submission and one refresh reduce this risk.
- Existing node data may not include a recent event; browser validation should create or reuse a real event first.

## Validation Risks

- Browser automation must not mint 100 tickets; proof batch size is capped to 5.
- AccountApp local state after restart may need import/session restore before proof.
- IPFS is not central to ticket batches unless files are uploaded, so proof can use metadata-only tickets.

## Reopen Triggers

- Any batch creates duplicate BO ids.
- Any accepted ticket is not visible to the intended owner address after refresh.
- SSR and WASM create ownership differ.
- Browser proof only validates rendering, not ledger ownership.
