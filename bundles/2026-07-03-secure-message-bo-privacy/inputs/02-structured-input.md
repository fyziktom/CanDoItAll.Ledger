# Structured Input

## Raw User Needs

- Secure-message transaction data must be privacy-minimized.
- Message subject, body, attachment filenames, descriptions, hashes, URIs, and other user content must only exist in encrypted payloads.
- BO IDs for messages must not be name/subject based.
- Raw tx view must be useful for inspection and should not obscure nested JSON behind escaped strings.
- Changes must be validated end to end in Docker WASM account apps with Alice and Bob.

## Non-Negotiable Invariants

- `INV-PRIV-001`: A new secure-message raw tx must not contain the message subject or body in `objectId`, `externalRef`, `data.inlineJson`, or business-object output refs.
- `INV-PRIV-002`: A new secure-message raw tx must not contain attachment filename, description, IPFS hash/CID, URI, or plaintext attachment bytes before decryption.
- `INV-PRIV-003`: Sender and receiver can decrypt message text and attachments using ECDH P-256 + AES-GCM; non-participants cannot open content.
- `INV-COMPAT-001`: Existing v1 secure-message transactions with public attachment metadata remain readable.
- `INV-RAW-001`: Raw tx dialog presents nested BO state as structured JSON when it can parse it.

## Large-Screen UX Notes

- No small/mobile tuning is required for this bundle.
- Raw tx and message details should remain dense, inspectable, and free of overlapping text.
