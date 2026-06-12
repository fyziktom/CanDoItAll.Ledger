# Ledger validation

## Confirmation algorithm

EF-backed transaction confirmation runs inside one storage transaction through `ILedgerConfirmationStore`. In-memory/test stores can use the simpler `ILedgerStore` path, but production storage should use the transaction-scoped confirmation contract.

```text
1. Canonicalize transaction core, calculate `TxId`, calculate submitted-envelope `TxHash`, and validate transaction shape.
2. Begin the EF confirmation transaction.
3. Lock the ledger state row for the ledger/scenario where the provider supports it.
4. Reject duplicate `TxId` in the same ledger/scenario.
5. Validate genesis sequence and input uniqueness within the transaction.
6. For every input:
   a. calculate outpoint key;
   b. load matching unspent output using a write lock;
   c. if not found, check spent index;
   d. reject if spent or unknown;
   e. verify referenced output address matches input address;
   f. verify referenced transaction is confirmed;
   g. keep the locked read in the confirmation context.
7. Validate BusinessObject create/update/transfer/close transition shape from consumed and produced BO-bearing UTXOs.
8. Validate token creation policy for mint and permission outputs.
9. Verify input signature bundles.
10. Validate asset conservation per asset.
11. Insert transaction header.
12. Insert transaction inputs.
13. Insert transaction outputs.
14. Move consumed outpoints from unspent set into spent index.
15. Insert created outputs into unspent set.
16. Insert new token-type registry rows for newly minted token ids.
17. Update BusinessObject current/version projections from BO-bearing UTXO outputs.
18. Update address projections.
19. Append ledger entry hash.
20. Sign ledger entry, spent rows, and projection state hashes with admin key.
21. Commit.
```

## Double spend rule

If an input outpoint exists in `spent_outputs`, the transaction is invalid. No further signature analysis is needed.

## Input reference rule

If an input outpoint does not exist in `unspent_outputs` and does not exist in `spent_outputs`, the transaction is invalid because the node cannot prove that the referenced spendable value exists.

## Asset conservation

For `Transfer` transactions:

```text
sum(input.main) == sum(output.main)
sum(input.token[tokenId]) == sum(output.token[tokenId]) for every token
```

For `Genesis`, `Mint`, and `Burn`, use explicit policy checks and admin authorization.

## BusinessObject rules

BusinessObjects are represented by ledger outputs. Confirmed transaction rows plus BO-bearing UTXO movements are canonical. `BusinessObjectRecord` and `BusinessObjectVersionRecord` are derived projections maintained during append and rebuilt by `EfLedgerProjectionRebuilder`.

`BusinessObjectTransitionValidator` rejects malformed BO transitions before append:

- a create output must start at version 1 and must not consume an existing output with the same object id;
- update and transfer outputs must consume the current output for the same object id and must increment version by exactly one;
- close is represented by consuming the current BO output and emitting no next BO output with the same object id;
- a transaction cannot consume multiple current outputs or emit multiple next outputs for the same object id.

Higher-level semantics such as allowed state-machine transitions remain application-level policy. Ledger validation only enforces ownership/version shape.

## Address registration rule

Gate A enforces derived-address validity and signature ownership. Registered-address rows are metadata/reserved for a future explicit admission policy; they are not a required output-address allowlist today.

## Concurrency rule

Use one of these safe strategies:

1. lock the ledger state row and process confirmations serially per ledger;
2. lock all input UTXO rows and use unique constraints on spent output rows.

The MVP should use the first strategy for simplicity and deterministic ledger sequence hashes.

## Rebuild rule

A full rebuild from genesis must recreate:

- current UTXO set;
- spent output index;
- address balances;
- BusinessObject current states;
- ledger entry chain.

A rebuild from a trusted snapshot must recreate everything after the snapshot sequence.

`EfLedgerProjectionRebuilder` rebuilds ledger state, UTXO rows, spent rows, token-type rows, address projections, and BusinessObject current/version projections from confirmed transaction/input/output rows. It intentionally does not modify canonical transaction history.

## Chain verification foundation

`LedgerChainVerifier` accepts confirmed transactions and independently recomputes the canonical transaction id, submitted transaction hash, ledger entry hash, previous-hash continuity, ledger sequence continuity, input signature payload hashes, and admin-signature payload hashes.

Current verified checks:

- all confirmed transactions belong to one ledger/scenario;
- ledger sequences are contiguous from sequence 1;
- `PreviousLedgerHash` matches the previous recomputed ledger entry hash;
- stored `TxId` matches canonical transaction core content;
- stored `TxHash` matches canonical submitted-envelope content;
- stored `LedgerEntryHash` matches the recomputed ledger entry payload hash;
- input `SignaturePayloadHash` and signature-envelope payload hash match the configured signing mode;
- admin signature bundles are present and target the ledger entry hash;
- optional strict trusted-admin-key mode verifies key id/algorithm/public-key hash and cryptographic admin signatures.

Node readiness labels payload-only verification as `valid_payload_only` only outside production trust requirements. A production-profile node requires configured trusted admin verification and should report readiness as degraded if trusted verification is missing or invalid.

Persisted EF projection verification is handled by `EfLedgerProjectionVerifier`. It calculates deterministic hashes for the UTXO set, spent set, address state, token-type state, BusinessObject current rows, BusinessObject version rows, and full projection state, and it detects projection corruption such as address balance/hash mismatches.

`EfLedgerSnapshotService` creates signed snapshots only when projection verification is valid and verifies snapshot hashes/signatures against current persisted state.

Deferred verification checks:

- archive verification and restore-from-archive drills;
- broader production migration/backup/restore drills with PostgreSQL infrastructure.
