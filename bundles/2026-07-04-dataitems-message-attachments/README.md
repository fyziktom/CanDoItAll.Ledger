# DataItem And Secure Message Attachment Repair

This bundle repairs the regression where normal, non-encrypted BO DataItems no longer render after structured BO envelope changes, and improves secure-message attachment UX after decryption.

## Mission

- Preserve public BO DataItems for image/post/music/etc. objects so gallery thumbnails and details can load normal IPFS attachments.
- Keep secure-message attachment locators and original file metadata encrypted until the message is opened.
- Render decrypted message attachments by recovered type instead of showing binary/base64 text.
- Add clear sent/received/replied indications in the messaging mailbox.
- Rebuild and restart Docker AccountApp instances, then capture screenshots proving gallery and message attachment visibility.

## Requirements

- `R1` Non-message BO payload parsing must accept both legacy string `stateJson` and structured object-valued `stateJson`.
- `R2` BO payload creation must guarantee exactly one main DataItem when any DataItems exist.
- `R3` Gallery/detail UI must use normal non-encrypted DataItems for thumbnails/previews, and every DataItem must offer a download action when a URI exists.
- `R4` Secure-message raw state must continue to hide original attachment file names, content types, and IPFS locators.
- `R5` After message decryption, image attachments must render as images; video/audio/text should render appropriately; every decrypted attachment must have a download action.
- `R6` Mailbox rows must visibly distinguish sent, received, and replied messages.
- `R7` Focused tests, Docker rebuild/restart, and browser screenshots must be captured before closure.

## Subbundles

- `SB01` Normal BO DataItems and gallery thumbnail repair.
- `SB02` Secure-message attachment rendering and mailbox state indicators.
- `SB03` Docker rebuild, browser validation, and screenshots.

## Validation Status

- Bundle preparation status: `Prepared`
- Execution status: `Completed`

## Proof Index

- `reviews/01-execution-report.md`
- `proof/SB01/manifest.md`
- `proof/SB02/manifest.md`
- `proof/SB03/manifest.md`
