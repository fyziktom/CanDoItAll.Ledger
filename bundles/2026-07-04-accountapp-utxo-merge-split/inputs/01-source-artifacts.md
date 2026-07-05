# Source Artifacts

- `bundle://inputs/00-original-request.md`
- Repo inspection commands:
  - `rg -n "Send Transaction|Split pay|Merge|TransactionComposer|UtxoExplorer|Utxo" src examples tests -g '*.razor' -g '*.cs'`
  - `rg -n "BuildUnsignedTransactionAsync|SubmitTransactionAsync\\(|BusinessObjectTransactionMode\\." src/CanDoItAll.Economy.Components/Services -g '*.cs'`
  - `rg -n "record LedgerSpendableOutput|record PaymentRequest|SpendableOutputsOrEmpty" src tests -g '*.cs' -g '*.razor'`
