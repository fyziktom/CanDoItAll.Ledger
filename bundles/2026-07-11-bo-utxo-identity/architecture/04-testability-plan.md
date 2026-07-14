# Testability Contract

## Characterization

- Preserve V1/V2 transaction canonical bytes and hashes for an existing `BusinessObjectRef` fixture.
- Preserve transition-validator behavior when a stable internal lineage consumes and emits the next version.

## Isolated unit tests

- `BusinessObjectId` converts to and from `OutPoint` and rejects malformed/non-outpoint strings.
- The lineage-key factory produces valid opaque values without type/name input.
- The payload writer/reader round-trips type, schema, JSON state, and source references.
- Projection mappers derive ID from `TxId` and output index, never the stored legacy lineage value.

No filesystem, database, network, or real clock is required for these tests. A deterministic fake lineage-key factory is injected into transaction-builder tests.

## Negative tests

- Clone and merge cannot proceed when selected IDs are malformed or not owned current outpoints.
- Payload references reject missing/invalid canonical source IDs.
- Update/transfer rejects a canonical `previous-version` reference whose consumed BO has a different internal lineage from the produced BO.
- A structured transaction rejects any unrelated consumed BO that is neither a required canonical source nor re-emitted under the same lineage.
- Node BO-ID query rejects `bo:post:name` and accepts `txid:index`.
- SDK transaction-summary mapping rejects a BO `objectId` claimed for any outpoint other than the containing transaction/output position.
- Create/clone/merge reviews contain no editable target-ID field.
- Clone review names the canonical protocol source as `Clone-source reference` and keeps the optional `External reference` distinct.

## Integration tests

- In-process create -> update -> clone -> merge proves current IDs and lineage references across confirmations.
- EF/Node read models expose current and historical outpoint IDs while replay still uses legacy stored keys.
- All three public transaction-summary routes omit lineage and derive BO IDs from `txid:outputIndex`; spendables expose correlation only as separately named `lineageKey`, and SDK mappings fail closed unless `objectId == outPoint` for both transaction summaries and spendables.
- History tests cover canonical `previous-version` chains, server-filtered legacy/data-hash binding, and trusted in-process fallback.
- Automated AccountApp contracts cover create/clone review and confirmed canonical identity. Manual in-app WASM interaction validates the live clone picker, canonical source card, non-editable pending identity, review state, and browser console.

## Composition smoke tests

- Existing DI resolves `BusinessObjectTransactionBuilder` with the default opaque lineage-key factory.
- Node and AccountApp build without new project references or cycles.

## Completion evidence

- focused test transcript;
- full solution or affected-project build transcript;
- Docker rebuild/restart and health checks;
- AccountApp browser screenshots for clone details and review, including canonical source identity and the pending post-confirmation target identity.
