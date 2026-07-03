# Current State

## Secure Message Payload

- `SecureLedgerMessages.CreateAsync` encrypts subject/body into `Content`, but it creates and stores a public `SecureLedgerMessageAttachment` list beside the encrypted content.
- Public attachment records include `BusinessObjectDataItem` with IPFS hash/URI, generated encrypted file name, storage type, content type, size, and description.
- `DecryptAttachmentAsync` depends on the public attachment list, so moving metadata into encrypted content requires a compatibility fallback for legacy messages.

## Secure Message Object Id

- `WasmAccountWorkspaceAdapter.SendSecureMessageAsync` calls `BusinessObjectCreateIdFactory.CreateNewObjectId("secure-message", request.Subject)`.
- The factory intentionally normalizes display names into IDs for general BOs. That is useful for inspectable public objects but wrong for encrypted messages.
- `ExternalRef` mirrors the object id as `secure-message://{objectId}`, so the current subject leak appears in both `vout[].businessObject.objectId` and `data.externalRef`.

## BO Envelope And Raw Display

- `BusinessObjectPayload.CreateData(CreateBusinessObjectRequest)` serializes an envelope with `stateJson` as a string.
- In-memory and node read models both expect `stateJson` to be a string.
- `TransactionDetailsDialogContent` parses the outer inline JSON but does not expand nested `stateJson`, so raw output displays escaped nested JSON.
- Existing BO parsers and history surfaces read `LedgerBusinessObjectHolding.StateJson`; a hard writer-only migration from `stateJson` string to object state would break older projection paths unless readers become tolerant.

## Test Gaps

- Existing secure-message tests check that subject/body/plain attachment bytes are not in `StateJson`.
- They do not check that subject is absent from BO ids/external refs.
- They do not check that attachment CID/URI/file metadata is absent from public transaction data.
- Docker validation already exercises Alice/Bob messaging, but the privacy red-team assertions need to run against the actual transaction JSON.
