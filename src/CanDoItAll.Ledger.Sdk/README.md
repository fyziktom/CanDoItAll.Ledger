# CanDoItAll.Ledger.Sdk

Client-facing facade for Ledger node and transaction operations.

The SDK provides stable application integration over Ledger APIs and contracts.
New behavior must document public API, transport, authentication, and
compatibility effects.

The SDK may expose confirmed Ledger facts and commands. It must not hide
validation failures or reinterpret Simulator projections as accepted
transactions.
