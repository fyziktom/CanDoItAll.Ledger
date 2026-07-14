# BusinessObject UTXO Identity: Current-State Inventory

> Baseline captured before implementation. The resolved design is documented in `01-target-solution.md`, and completed proof is recorded in `../proof/verification-summary.md`.

## Defect

`BusinessObjectId` currently has two incompatible meanings:

1. transaction builders accept a caller-created string such as `bo:post:name:nonce`;
2. projections also carry the real output position in `CurrentOutPoint`.

The arbitrary string is serialized inside `BusinessObjectRef` and included in the transaction hash. It therefore cannot be the ID of the output created by that transaction. Create, clone, and merge screens expose or predict this string even though the ledger already has the authoritative identity: `TxId:OutputIndex`.

## Responsibility map

| Concern | Current owner | Problem |
|---|---|---|
| Wire transition continuity | `BusinessObjectRef.ObjectId` and ledger validators | Named as public identity, but actually behaves as an internal stable lineage key. |
| Current object identity | `BusinessObjectRecord.ObjectId` plus `CurrentOutPoint` | Duplicated; API returns the stable key instead of the current outpoint. |
| Create/clone/merge target identity | request records, component drafts, transaction forms | Caller invents an ID that cannot be authoritative before confirmation. |
| Type/schema/state | transaction data and projection fallbacks | Create is structured; update/merge and some transfer paths emit raw state JSON and can lose type/schema. |
| Source lineage | stable key matching in validators | Clone/merge source references are not part of the new object payload. |
| Node/API identity | EF read-model mapping and SDK DTOs | Stored lineage key leaks through `BusinessObjectId`. |

## Constraints

- Transaction V1/V2 hashes and semantic replay must remain valid.
- A transaction cannot serialize its own finalized outpoint without circular hashing.
- Update/transfer validation still needs a stable internal correlation key for existing V1/V2 outputs.
- Public BO identity must be the current output outpoint and therefore changes whenever an update or transfer creates a new output.
- Display name and object type are data, never identity.

## Root cause

The previous identity-uniqueness work explicitly deferred an outpoint projection redesign and generated a unique metadata-derived string as a temporary stable key. The temporary key subsequently leaked into requests, persistence names, API responses, and UI labels as if it were the BO ID.
