# CanDoItAll.Ledger.ConsoleDemo

Guided console scenarios for learning and smoke-testing Ledger transactions,
accounts, keys, business objects, permissions, exports, and messaging.

Run from the repository root:

```powershell
dotnet run --project examples\CanDoItAll.Ledger.ConsoleDemo\CanDoItAll.Ledger.ConsoleDemo.csproj
```

Lesson 07 mirrors the AccountApp transaction semantics:

- UTXO split consumes explicitly selected plain main-currency UTXOs and creates
  requested outputs plus positive change.
- UTXO merge consumes explicitly selected plain main-currency UTXOs and creates
  one output for the selected receiver.
- BusinessObject outputs use a one-token carrier with zero main amount; any
  main currency is represented by separate plain outputs.
- Permission-holder minting creates token outputs without minting main
  currency.

Run the Ledger tests and a console smoke whenever scenario or command behavior
changes.
