# QA Prompt

Audit the implementation against the raw request literally.

Required checks:

- `Neblio` does not appear in production default tags.
- Browser/WASM ECDH path is used for AccountApp encryption.
- Transaction signatures include public key material and key mismatch is rejected.
- Public-key discovery accepts only signature public keys deriving to the queried address.
- Message BO state does not contain plaintext subject/body for secure messages.
- IPFS attachment bytes do not contain plaintext and can decrypt only on request.
- Inbox list does not trigger decryption of all messages.
- Opened message decrypts exactly the selected message.
- Docker validates Alice to Bob and Bob to Alice, each with and without attachments.
- Screenshots are reviewed for large-screen space use, no overflow, no confusing BO-first primary UX, and readable dialogs/panels.
