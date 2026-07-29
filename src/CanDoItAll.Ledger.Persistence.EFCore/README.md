# CanDoItAll.Ledger.Persistence.EFCore

Provider-neutral Entity Framework Core stores, history readers, projections,
snapshots, and schema contracts for CanDoItAll Ledger.

Use this package when implementing an EF Core-backed Ledger host or a custom
relational provider. For a supported durable deployment, use
`CanDoItAll.Ledger.Persistence.PostgreSql`, which supplies the provider and
migrations.

## Requirements

- .NET 10
- An EF Core provider when using `AddLedgerEfCoreStores()`

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Persistence.EFCore
```

## Disposable Test Store

```csharp
using CanDoItAll.Ledger.Persistence.EFCore;

builder.Services.AddLedgerInMemoryPersistence("ledger-tests");
```

`AddLedgerInMemoryPersistence` uses `Microsoft.EntityFrameworkCore.InMemory`.
It is not SQLite, relational, durable, or suitable for authoritative expense
records. Use it for isolated tests and disposable development only.

## Custom Provider Composition

```csharp
using CanDoItAll.Ledger.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContextFactory<LedgerDbContext>(options =>
{
    // Configure the relational provider selected and owned by this host.
});

builder.Services.AddLedgerEfCoreStores();
```

The host must supply `IDbContextFactory<LedgerDbContext>`.
`AddLedgerEfCoreStores()` registers:

- `ILedgerStore` through `EfLedgerStore`;
- history and transaction readers;
- projection verification and rebuilding;
- snapshots;
- the provider-neutral schema migrator.

Concrete relational migrations belong to the provider package. The PostgreSQL
package replaces the base migrator with its provider-aware implementation.

## Persistence Notes

- Treat the Ledger database as authoritative durable data and use
  database-native backups.
- Run projection verification after migration or repair work; projections are
  derived from confirmed transaction history.
- Coordinate startup migration policy in the host. Registration alone does not
  migrate a database.
- Stable `Economy_*` relational names are persisted compatibility identifiers;
  renaming them requires an explicit data migration and compatibility review.
- Do not expose EF entities as public API contracts.

See the
[PostgreSQL provider](https://www.nuget.org/packages/CanDoItAll.Ledger.Persistence.PostgreSql),
[persistence operations](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/docs/operations),
and [full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT-derived license with website-link requirement.
