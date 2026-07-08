# CanDoItAll.Economy.ConsoleDemo

Ledger-owned console demo for transaction and node flows.

The compatibility name remains until the CLI and SDK namespace migration plan is executed. Keep the demo focused on Ledger workflows.

Lesson 07 mirrors the working WASM AccountApp transaction semantics:

- UTXO split consumes explicitly selected plain main-currency UTXOs and creates requested plain vouts plus positive change.
- UTXO merge consumes explicitly selected plain main-currency UTXOs and creates exactly one plain vout to self or the selected receiver.
- BusinessObject outputs are token-carried: one-token carrier on the BO output with zero main amount, while any main currency is represented by separate plain outputs.
- Permission-holder token minting creates token outputs without minting main currency.

Validate with build, tests, and a console smoke when command behavior changes.
