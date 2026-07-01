# Target Solution

## Identity Boundary

BusinessObject identity and display metadata are separate:

- `BusinessObjectId`: ledger lineage key used by create/update/transfer/close validation.
- Payload `name`: user-facing title, allowed to repeat.
- `OutPoint` / transaction hash: immutable chain position and best share/detail route once a transaction exists.

## Implementation Shape

- Add a small BO ID helper in the components layer.
- The helper creates IDs with the shape `bo:{type}:{slug}:{nonce}` for real creates.
- The nonce uses UTC ticks plus an atomic process-local sequence, encoded as safe lowercase hex. This avoids WASM cryptography APIs and stays deterministic enough for tests.
- The wizard uses the helper only for `BusinessObjectTransactionMode.Create`.
- Preview details keep the existing `bo:preview:{type}:{slug}` identity because it never goes to ledger submission.

## Non-Goals

- Do not allow overwriting an existing current BO.
- Do not change update/transfer/close object lookup semantics.
- Do not introduce NBitcoin, BouncyCastle, or new browser crypto dependencies.
