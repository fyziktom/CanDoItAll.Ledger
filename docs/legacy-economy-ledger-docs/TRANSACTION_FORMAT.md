# Transaction format

## Logical shape

```text
tx
- version
- ledgerId
- scenarioId?
- kind
- signingMode
- timestamp
- inputs[]
    - previousTxId
    - previousOutputIndex
    - address
    - signaturePayloadHash?
    - signatureBundle
- outputs[]
    - address
    - mainAmount
    - tokenId?
    - tokenAmount?
    - businessObjectId?
    - metadataHash?
- data
    - itemType
    - contentType?
    - dataHash?
    - inlineJson?
    - externalRef?
```

## Transaction ID

`txId` is derived only from the transaction core:

```text
txId = SHA-256(CanonicalTxCore)
```

`CanonicalTxCore` excludes:

- input signatures;
- input signature payload hashes;
- client-supplied `txId`;
- client-supplied `txHash`;
- admin signature;
- status;
- database identity;
- confirmation timestamp;
- ledger sequence;
- projection state.

This avoids circular signing and keeps signatures stable.

Current `CanonicalTxCore` includes ledger id, scenario id, version, transaction kind, signing mode, timestamp as Unix milliseconds, inputs ordered by `InputIndex`, outputs ordered by `OutputIndex`, and `TransactionData` including `ItemType`. Changing core content changes `txId`; changing signatures, stale ids/hashes, status, or confirmation data does not.

## Transaction hash

`txHash` is the submitted-envelope hash. It includes canonical core bytes plus input signature bundles:

```text
txHash = SHA-256(CanonicalSubmittedTransaction)
```

The ledger entry signs and chains `txId` and `txHash`. Because signatures are included in `txHash`, two submissions with the same core transaction and different signatures keep the same `txId` but have different `txHash` values.

## Golden vectors

Canonical compatibility vectors live in `docs/golden-vectors/transaction-hashes-v1.json`. The test suite loads that file and asserts the current runtime output for:

- `TxId`;
- submitted-envelope `TxHash`;
- independent input payload hash;
- outpoint key;
- ledger entry hash.

Changing any of those values is a compatibility event. Update the vector only with an explicit canonical-format decision and matching documentation.

## Amounts

The current code uses the `Amount` value object over `decimal` and serializes amounts as invariant decimal strings. Do not use floating-point types.

Recommended representation:

```text
mainAmount: invariant decimal string, non-negative for outputs
tokenAmount: invariant decimal string, non-negative for outputs
```

Before external compatibility guarantees, decide whether to keep invariant decimal strings as the public canonical amount format or migrate to fixed-scale integer minor units with golden-vector coverage.

## Data

The `data` field is flexible but must be hashable. It always carries an `itemType` discriminator so loaders can sort and query payloads without parsing JSON. Current item types are `Generic`, `TokenMetadata`, `BusinessObject`, `AccountSettings`, `ScenarioMetadata`, `SnapshotMetadata`, `Permission`, and `Rule`.

For account settings BusinessObjects/NFT-style payloads, use `itemType = AccountSettings` and pair it with the output `businessObject` reference. For large BusinessObject payloads, store:

```text
businessObjectId
businessObjectVersion
businessObjectDataHash
businessObjectDataRef
```

Do not put large blobs in the transaction table unless there is a strong reason.

## Canonical serialization rules

The current canonical writer must:

1. write UTF-8 without BOM;
2. use stable property names;
3. write object properties in strict schema order;
4. sort inputs by `InputIndex` and outputs by `OutputIndex`;
5. sort submitted signature blocks by algorithm and key id;
6. encode byte arrays as standard base64 in submitted-envelope hashes;
7. encode amounts as invariant decimal strings;
8. encode timestamps as Unix milliseconds or ISO-8601 UTC with a fixed format;
9. reject NaN, Infinity, floats, duplicate property names, and ambiguous number formats;
10. include a domain separator in payload hashes.

## Signing modes

### Independent

Every input signer signs the same core payload:

```text
payload = SHA-256(
  "cdia.economy.input.independent.v1" ||
  txId ||
  inputIndex ||
  previousTxId ||
  previousOutputIndex ||
  address
)
```

### Ordered

The signer at input index `n` signs the transaction core plus signatures for inputs before `n`:

```text
payload[n] = SHA-256(
  "cdia.economy.input.ordered.v1" ||
  txId ||
  inputIndex ||
  previousTxId ||
  previousOutputIndex ||
  address ||
  CanonicalSignatureBlock(inputs[0..n-1])
)
```

This supports approval chains where every later signer explicitly sees earlier approvals.

## Admin confirmation

After validation, the node appends a ledger entry:

```text
entryHash = SHA-256(
  "cdia.economy.ledger-entry.v1" ||
  previousLedgerHash ||
  ledgerSequence ||
  txId ||
  txHash ||
  confirmedAt
)

adminSignature = Sign(adminKey, entryHash)
```
