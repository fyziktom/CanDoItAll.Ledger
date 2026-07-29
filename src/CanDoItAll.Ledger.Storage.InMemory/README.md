# CanDoItAll.Ledger.Storage.InMemory

Thread-safe in-memory Ledger storage for tests and disposable single-process
development.

The store implements `ILedgerConfirmationStore` and serializes confirmation
inside one instance. Use it when a fast, isolated ledger is more important than
durability or multi-process coordination.

## Requirements

- .NET 10

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Storage.InMemory
```

## Use Directly

```csharp
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Storage.InMemory;

ILedgerStore store = new InMemoryLedgerStore();

LedgerState? state = await store.GetLedgerStateAsync(
    new LedgerId("agent-expenses"),
    scenarioId: null,
    CancellationToken.None);
```

## Register

```csharp
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Storage.InMemory;

builder.Services.AddSingleton<InMemoryLedgerStore>();
builder.Services.AddSingleton<ILedgerStore>(
    services => services.GetRequiredService<InMemoryLedgerStore>());
builder.Services.AddSingleton<ILedgerConfirmationStore>(
    services => services.GetRequiredService<InMemoryLedgerStore>());
```

Use a singleton when one process should share one disposable ledger.
A scoped or transient registration creates separate stores and therefore
separate ledger state.

## Limitations

- All data is lost when the owning process or service instance ends.
- The store has no backup, restore, migration, or cross-process coordination
  path.
- Its confirmation lock protects one store instance only.
- It is appropriate for unit tests, examples, and disposable local tools, not
  authoritative agent or project expense records.
- Use
  `CanDoItAll.Ledger.Persistence.PostgreSql` for a durable node.

See the
[Engine package](https://www.nuget.org/packages/CanDoItAll.Ledger.Engine),
[PostgreSQL provider](https://www.nuget.org/packages/CanDoItAll.Ledger.Persistence.PostgreSql),
and [full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT-derived license with website-link requirement.
