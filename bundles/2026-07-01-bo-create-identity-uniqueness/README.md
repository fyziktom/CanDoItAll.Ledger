# BO Create Identity Uniqueness

This bundle fixes the AccountApp BusinessObject create collision where two user-created BOs with the same type and name reused the same ledger `BusinessObjectId`.

## Profile

- `defect`
- `initiative`

## Mission

Make every new interactive BO creation mint a unique ledger object identity independent of title, content, description, attachment, or other metadata. Keep the ledger invariant that a `Create` operation must not overwrite an already-current object with the same object id.

## Outcome Contract

- Requested outcome: repeated creation of an image/post/etc. with the same display name must succeed as a distinct BO.
- Hard constraints: BO uniqueness must align with the UTXO/transaction identity model; display metadata must not be used as the persistent identity; AccountApp signing/assembly remains in WASM through the existing node API flow.
- Evidence required before closure: root-cause notes, VEFramework/wiki reference mapping, code fix, focused .NET tests, AccountApp build, and browser proof that duplicate-name creation is accepted.
- Scope exception: this bundle does not redesign the whole projection storage model from `ObjectId` to outpoint. It repairs the create-time identity source so the existing versioned BO state machine remains valid.

## Recommended Execution Order

1. `subbundles/01-sb01-root-cause-and-reference-model`
2. `subbundles/02-sb02-create-id-repair`
3. `subbundles/03-sb03-validation-and-browser-proof`

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Complete`
- Final closure gate: `Passed`
