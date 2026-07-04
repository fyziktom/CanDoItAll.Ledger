# SB02 Secure Message Attachments

## Scope

- Keep raw secure-message payload privacy unchanged.
- Render decrypted attachment bytes according to recovered encrypted metadata.
- Add download actions to decrypted attachments.
- Add mailbox status indicators for received/sent/replied.

## Proof

- Secure-message tests proving raw state still hides attachment metadata.
- Component tests proving decrypted image attachments render as images and expose download.
- Browser screenshot of decrypted message image attachment in Docker.
