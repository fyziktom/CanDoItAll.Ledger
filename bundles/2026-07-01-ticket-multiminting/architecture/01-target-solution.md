# Target Solution

## Shape

Add a ticket-focused multimint panel inside `BusinessObjectCreationWizard.razor`:

- Toggle between single mint and multimint.
- Select recipient mode: own account, subaccount, address book, manual list.
- Choose a small count for generated tickets.
- Preview resolved recipients and generated ticket numbering.
- Stage one `BusinessObjectTransactionDraft` per ticket.

## Draft Rules

- `ObjectId`: generated via `BusinessObjectCreateIdFactory.CreateNewObjectId("ticket", payload.Name)` for every ticket.
- `OwnerAddress`: resolved recipient address.
- `ReceiverAddress` and `PaymentReceiverAddress`: default to the same recipient unless the transaction mode requires otherwise.
- `StateJson`: created from `BusinessObjectPayloadBuilder` with ticket payload fields normalized.
- `ticketNumber`: base ticket number plus a zero-padded suffix when count > 1.
- `ExternalRef`: `preview://ticket/{normalized-name}/batch/{index}` or equivalent stable local preview reference.

## Batch Submission

Add a batch event from wizard to app shell to adapter host:

- Wizard emits `BusinessObjectBatchTransactionRequest`.
- App shell switches to gallery after a successful batch request.
- Adapter host submits each draft sequentially through `Adapter.SubmitTransactionAsync`.
- Host refreshes once at the end and shows one summary notification.

## Adapter Alignment

- WASM already builds create requests with `draft.OwnerAddress`.
- Real-node/SSR session must also use `draft.OwnerAddress`.
- Development session must stop replacing create object ids and should preserve generated BO identity for UI-created objects.

## Deferred Architecture

- Persistent browser subaccounts need vault/key storage and import/export flows. This bundle only consumes existing `Workspace.SubAccounts`.
- Resume state for partially completed batches can later use the AccountApp cache provider; the first version keeps batches synchronous and small.
