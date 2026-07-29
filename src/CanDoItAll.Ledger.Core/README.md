# CanDoItAll.Ledger.Core

Core Ledger contracts for accounts, addresses, amounts, assets, transactions,
chain state, storage abstractions, and validation data.

This is the innermost product layer. It must remain independent from hosts,
user interfaces, provider-specific persistence, and experimental Simulator
implementations.

Public changes require compatibility review and focused transaction hash,
serialization, validation, and replay tests.
