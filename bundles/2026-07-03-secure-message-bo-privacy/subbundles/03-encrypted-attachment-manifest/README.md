# SB03 Encrypted Attachment Manifest

## Status

- `Completed`

## Objective

- Move secure-message attachment metadata and storage locators from public payload into encrypted content while preserving v1 fallback.

## Covered Inputs

- User raw tx shows `encryptedDataItem.hash`, `uri`, and attachment metadata in public transaction data.

## Prerequisites

- SB01 privacy tests.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs`
- `repo://src/CanDoItAll.Economy.Core/Messages.cs`
- `repo://src/CanDoItAll.Economy.Cryptography/EcdhP256MessageCipher.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`

## Deliverables

- Encrypted content attachment descriptor includes encrypted DataItem, nonce, tag, and ciphertext hash.
- New public payload writes empty attachment list.
- Decrypt fallback reads v1 public attachment records when encrypted descriptors are absent.

## Dependency Impact

- Message list still shows generic message rows until opened.
- Attachment opening now requires decrypted content descriptor.

## Validation Depth

- Unit tests for new payload privacy.
- Integration tests for Alice/Bob message with attachment.
- Legacy payload parse/open test.

## Implementation Steps

1. Add locator fields to decrypted attachment descriptor.
2. Upload encrypted attachment before final content encryption so locator can be encrypted.
3. Change `DecryptAttachmentAsync` to prefer descriptor locator and fallback to public v1 locator.
4. Add ciphertext hash verification when opening attachment.

## Do Not Do

- Do not expose attachment CID/URI/file name in public payload.

## Acceptance Checklist

- [ ] Public `payload.Attachments` is empty for new messages.
- [ ] Raw tx does not contain CID, URI, original file name, description, content type, or size.
- [ ] Sender and receiver open attachments successfully.
- [ ] Legacy v1 attachment messages remain openable.

## Proof Required

- Targeted secure-message unit and adapter tests.

## Browser Validation Logging

- SB06 validates in Docker WASM.

## Progression Gate

- SB04 starts after new payload contract compiles and tests pass.

## Suggested Agent Prompt

- Refactor secure-message attachments so metadata and locator are encrypted, with v1 fallback and red-team assertions.
