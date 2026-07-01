# Current State

## Root Cause

The ledger validator rejects a `Create` only when the transaction attempts to create a currently existing `BusinessObjectId`. This invariant is correct for the current BO state machine because update, transfer, close, and clone operations track object lineage by `BusinessObjectId`.

The AccountApp wizard currently derives the create identity as:

```text
bo:{selectedObjectType}:{NormalizeName(payload.Name)}
```

For two image BOs named `New Image BO`, both drafts use `bo:image:new-image-bo`. The second create therefore collides with the first current BO even if the transaction hash, attachments, long text, and payload content are different.

## Reference Model

VEFramework NFTs keep user-facing data such as `Name`, `Description`, `Text`, `Image`, tags, and data items as metadata. Practical identity and tracking are based on transaction/UTXO properties such as `Utxo`, `UtxoIndex`, `SourceTxId`, and `NFTOriginTxId`.

The wiki reference describes NFTs as unique data records whose history is tracked through UTXO transactions. Minting creates a new output with one token; later updates/transfers move that UTXO lineage forward.

## Decision

Keep the ledger duplicate-create guard. Repair AccountApp create identity generation so interactive BO creates use a stable prefix plus unique non-metadata entropy. The display name stays in payload `name`, not in the persistent identity.
