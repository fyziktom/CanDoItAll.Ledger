# CanDoItAll.Ledger.Core

Protocol contracts and value types for auditable CanDoItAll ledgers.

This is the dependency-light foundation for accounts, addresses, amounts,
tokens, UTXOs, transactions, signatures, validation results, chain state, and
storage abstractions. Choose it when implementing a driver, store, validator,
or integration that needs the Ledger wire and domain contracts without a host
or persistence provider.

## Requirements

- .NET 10

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Core
```

No dependency-injection registration is required.

## Validate External Values

```csharp
using CanDoItAll.Ledger.Core;

static Amount ValidateExpense(decimal value)
{
    Amount amount = Amount.FromDecimal(value);
    amount.EnsureNonNegative(nameof(value));
    return amount;
}

static Address ParseAddress(string text) =>
    Address.TryParse(text, out Address address)
        ? address
        : throw new FormatException("Invalid CanDoItAll Ledger address.");
```

Value-object constructors are intentionally lightweight and don't universally
validate protocol input. Parse or explicitly validate values received from
users, files, databases, or networks.

## Main Contracts

- `LedgerTransaction`, `TransactionInput`, and `TransactionOutput`
- `LedgerId`, `Address`, `TxId`, `OutPoint`, `Amount`, and `TokenId`
- `ILedgerStore` and `ILedgerConfirmationStore`
- hashing, canonicalization, signature, clock, and admin-signer abstractions
- `ValidationResult`, `ValidationError`, and transaction submission results

## Compatibility Notes

- Public contract changes can affect JSON payloads, transaction hashes,
  signatures, persistence, and node interoperability.
- A public `BusinessObjectId` is derived from the confirmed output outpoint.
  `BusinessObjectLineageKey` is internal protocol correlation state; the two
  identities are not interchangeable.
- Use the canonicalizer and hasher from
  `CanDoItAll.Ledger.Cryptography` for transaction IDs. Do not hash arbitrary
  JSON serialization.
- Storage implementations must preserve ledger, scenario, outpoint, and
  confirmation semantics defined by these contracts.

See the
[runtime architecture](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/architecture/ledger-runtime.md)
and [full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT-derived license with website-link requirement.
