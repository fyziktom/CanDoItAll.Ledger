# Target Solution

## Target Shape

- Introduce explicit UTXO merge/split request models in the business-object account layer, for example `UtxoMergeRequest`, `UtxoSplitRequest`, and `UtxoSplitOutput`.
- Add transaction-builder methods that load spendable outputs, resolve selected `OutPoint`s, reject any selected output with `BusinessObject` or `Token`, preserve the selected input order, and produce plain `TransactionKind.Transfer` transactions.
- Add account methods and adapter/session switch cases for the new modes so SSR and WASM paths use the same domain logic.
- Extend `BusinessObjectTransactionDraft` or add a focused companion model with selected UTXO keys and split output rows. Prefer a shape that keeps Send, Merge, and Split forms reusable.
- Replace the old dialog Merge/Split tabs with UTXO-specific form components. BO merge/split can remain available in BO-specific surfaces if needed, but must not masquerade as home send dialog UTXO operations.
- Tighten transaction details scroll/list styling only if real large-row proof shows layout defects.

## Boundaries

- Do not implement token UTXO merge/split in this bundle.
- Do not spend BusinessObject outputs, permission outputs, or rule outputs.
- Do not remove the underlying BusinessObject merge/split builder APIs if other flows or tests still use them.
- Do not redesign the whole AccountApp home page; keep work scoped to transaction assembly, dialog forms, and transaction details overflow behavior.

## Expected Vin/Vout Semantics

- Merge: inputs are exactly the selected eligible UTXOs; outputs contain exactly one main-currency output with amount equal to the selected total and address equal to self or the external merge address.
- Split: inputs are exactly the selected eligible UTXOs; outputs contain user-defined main-currency outputs. Any unallocated positive remainder should be returned as a change output to self unless the UI/output plan consumes the full total.
