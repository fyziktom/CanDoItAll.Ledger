# CanDoItAll.Ledger.Rules.NCalc

NCalc-backed implementation of the Ledger account-rule expression evaluator.

Expressions are parsed and evaluated with invariant culture, and NCalc-specific exceptions are translated into the Ledger rule-evaluation contract. Keep NCalc API details isolated in this adapter.

The test suite covers supported types, functions, error translation, culture invariance, and every expression in the current simulator scenario catalog.
