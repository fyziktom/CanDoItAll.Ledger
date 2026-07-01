# Assumptions And Risks

## Assumptions

- Existing BO update, transfer, sale, and commerce dispatch flows depend on stable `BusinessObjectId` lineage and should not be converted to outpoint-only lookup in this bundle.
- Browser WASM must avoid new cryptography dependencies for identity creation.
- A creation-time nonce is acceptable until the project performs a broader projection redesign that can derive identity directly from finalized transaction outpoints.

## Risks

- Using only a title slug is unsafe and already reproduced.
- Using only a timestamp can collide under fast repeated creation, so the helper must include a process-local counter.
- The helper must stay within `LedgerValidationLimits.MaxBusinessObjectIdLength` and only use valid identifier characters.
- Preview object IDs must remain clearly non-ledger preview values.
