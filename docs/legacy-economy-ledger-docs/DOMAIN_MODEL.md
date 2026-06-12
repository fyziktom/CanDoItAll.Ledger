# Domain model

## Address

An address identifies a spend authority. In the current Gate A path it is derived from public key material and spend authority is proven by input signatures. The node can keep an address registry for metadata or future admission policy containing:

- address;
- key algorithm;
- public key or public key hash;
- display label;
- status;
- admin-signed registration state.

## Ledger account

A ledger account is an address plus projections:

- available UTXOs;
- available main balance;
- token balances;
- owned BusinessObjects;
- committed and planned views derived from BusinessObject state.

The account projection is a cache.

## Transaction

A transaction consumes zero or more previous outputs and creates one or more new outputs.

Transaction kinds:

- `Genesis`: creates initial supply; admin-signed; no inputs.
- `Mint`: creates main currency or token supply; admin-authorized.
- `Transfer`: normal UTXO spend.
- `BusinessObject`: creates, updates, transfers, or closes a BusinessObject.
- `Burn`: removes value or token units with explicit reason; admin-authorized or policy-authorized.

## Input

An input references a previous output:

```text
previousTxId
previousOutputIndex
address
signatureBundle
signaturePayloadHash
```

The address must match the previous output owner.

## Output

An output is spendable by its address and may contain:

- main currency amount;
- one token amount;
- one BusinessObject reference;
- metadata hash;
- optional lock window.

The MVP can use one token per output. The model can later evolve to multi-asset outputs.

## BusinessObject

A BusinessObject is a domain object attached to the ledger. It replaces the user-facing term NFT.

Examples:

- PurchaseRequest
- PurchaseApproval
- PurchaseOrder
- Invoice
- Asset
- BudgetAllocation
- RevenueForecast
- SalaryCommitment
- CloudCostCommitment
- WorkPackage
- RiskReserve

A BusinessObject is updated by spending the UTXO carrying its previous version and creating a new output carrying the next version.

This creates a natural single-writer rule:

```text
Only one confirmed transaction can spend the current BusinessObject outpoint.
```

## BusinessObject version

Each version stores:

- object id;
- object type;
- schema version;
- state;
- owner address;
- transaction id;
- output outpoint;
- data hash;
- optional JSON data;
- created timestamp.

## Token

A token represents internal budget units, resource units, department-specific allocation units, or other non-main assets. Token definitions are ledger-bound and admin-signed.

## Scenario

A scenario is a fork of ledger state from a snapshot or sequence. Scenario transactions must not affect the real ledger. This enables what-if simulations and project cash-flow planning.
