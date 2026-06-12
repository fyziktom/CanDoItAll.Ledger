# Codex implementation plan

Historical implementation checklist. Use [ROADMAP.md](ROADMAP.md) as the current execution plan.

## Step 1: compile the skeleton

Run:

```bash
./scripts/bootstrap-solution.sh
dotnet restore
dotnet build
```

Fix compile errors without changing architecture boundaries.

## Step 2: implement canonicalization

Implement a deterministic canonical writer. Do not rely on unordered dictionary enumeration.

Acceptance:

- same transaction produces the same bytes across test runs;
- changing input indexes or referenced outpoints changes txId;
- changing output indexes, owners, amounts, tokens, or metadata changes txId;
- reordering input/output arrays without changing their indexes does not change txId because the canonical writer orders by index;
- different signature does not change txId;
- stale client-supplied txId/txHash does not change txId;
- different data hash changes txId.

## Step 3: implement signatures

Implement:

- `ECDsaP256SignatureProvider`;
- `SignatureProviderRegistry`;
- `SignaturePolicyEvaluator`;
- optional `MLDsaSignatureProvider` when supported.

Acceptance:

- valid independent signature verifies;
- invalid signature fails;
- ordered mode signer 2 payload changes when signer 1 signature changes;
- independent mode signer 2 payload does not change when signer 1 signature changes.

## Step 4: implement validator

Acceptance:

- no negative output amounts;
- duplicate inputs rejected;
- unknown input rejected;
- spent input rejected;
- address mismatch rejected;
- asset imbalance rejected;
- invalid signature rejected;
- valid multi-input/multi-output transfer accepted.

## Step 5: implement in-memory executor

Acceptance:

- genesis creates UTXO;
- transfer consumes old UTXO and creates outputs;
- spent index is updated;
- address projections update;
- projection rebuild matches current projections.

## Step 6: implement PostgreSQL store

Acceptance:

- migration applies on empty database;
- transaction confirmation is atomic;
- concurrent double-spend attempts result in one success and one rejection;
- chain verification passes after valid transactions;
- projection rebuild matches stored projections.

## Step 7: implement CLI

Acceptance:

- every MVP operation can be run through CLI;
- JSON transaction drafts can be inspected before signing;
- chain verification command returns non-zero exit code on failure.

## Step 8: implement BusinessObjects

Acceptance:

- create BO;
- update BO by spending current BO outpoint;
- reject update with stale BO outpoint;
- transfer BO owner;
- list current BOs by owner.

## Step 9: implement snapshots and scenarios

Acceptance:

- create snapshot;
- verify snapshot hash;
- fork scenario from snapshot;
- scenario transaction does not mutate real ledger;
- simulation detects insufficient funds.
