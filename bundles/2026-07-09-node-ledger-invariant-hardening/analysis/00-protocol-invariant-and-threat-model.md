# Protocol invariant and threat model

## Trust boundaries

1. **Untrusted transaction envelope**: HTTP clients, SDK callers, simulation code, and direct in-process callers can construct record values that bypass UI/builder checks.
2. **Validation boundary**: `LedgerExecutor` is the final application-level authority before confirmation.
3. **Confirmation store boundary**: the store must evaluate against the same UTXO/ledger state it commits.
4. **Database boundary**: direct corruption is outside normal business operations, but schema constraints and verification reduce blast radius and improve detection.
5. **Admin/treasury keys**: treasury authorizes issuance; ledger admin signs confirmed state for tamper evidence. They are separate roles even when development defaults make them convenient.
6. **Derived read models**: address balances, token registry, BO current/version rows, and snapshots are not spend authority.

## Asset model

For an output `o`:

- `o.main > 0` implies `o.token == null`;
- `o.token != null` implies `o.main == 0` and `o.token.amount > 0`;
- a normal BO output implies `o.token.amount == 1`;
- a plain output must carry positive main or positive token value;
- permission/rule BOs are authority objects and may use protocol-specific token quantities.

For a transfer/BO transaction and every asset `a`:

`sum(inputs[a]) == sum(outputs[a])`

For burn:

`sum(outputs[a]) <= sum(inputs[a])`

For mint:

- existing normal assets are conserved;
- permission token output cannot exceed permission token input;
- main output cannot be lower than main input and any positive delta needs treasury authorization;
- new normal token IDs may have a positive initial supply only with a valid treasury-origin permission;
- rule/permission deltas follow their dedicated issuance policy.

## UTXO and concurrency invariants

- Every input references exactly one current UTXO in the same ledger/scenario.
- A referenced outpoint can be consumed once.
- Validation state, sequence/hash assignment, spent insertion, unspent deletion/creation, registry changes, BO projections, address projections, and ledger state advancement commit atomically.
- Concurrency conflict is a rejected result, not partial persistence or an unhandled exception.

## BusinessObject invariants

- Create starts at version 1 and does not overwrite a current object.
- Update/transfer consumes the current outpoint and increments exactly once.
- Update retains owner; transfer changes owner.
- Update/transfer preserves carrier token identity.
- Close consumes the current BO and emits no next version for the same object.
- Non-burn close/merge conserves the carrier token; burn may destroy it explicitly.

## Threat scenarios

| Threat | Required control |
| --- | --- |
| Oversized transaction hashed before validation | Cheap preflight before canonicalization; HTTP stream/body limit. |
| Reuse of a spent output | Transactional UTXO/spent lookup plus unique constraints/locks. |
| Two concurrent spends | Per-ledger/input serialization and deterministic conflict mapping. |
| Mint duplicates unrelated existing token | Issuance-aware conservation, not blanket mint exemption. |
| Raw signed account Mint increases main currency | Fail-closed main-currency issuance policy requiring configured treasury and a consumed plain treasury authority UTXO. |
| Permission splitting inflates authority | Aggregate permission conservation and delegation policy. |
| BO changes carrier type during update | Transition-level carrier identity check. |
| BO Update silently transfers ownership | Operation/owner semantic check. |
| Decimal sum overflow | Checked aggregation mapped to a validation error. |
| Valid hash chain contains semantically invalid history | Versioned semantic replay verifier proposal. |
| Authenticated JWT has unintended full power | Scope/role-aware endpoint authorization proposal. |
| Direct DB insert violates output shape | Relational check constraints plus verification. |
