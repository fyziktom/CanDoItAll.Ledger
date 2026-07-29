# CanDoItAll.Ledger.Persistence.PostgreSql

PostgreSQL persistence, migrations, schema adoption, and resilient
dependency-injection setup for CanDoItAll Ledger.

This is the supported durable relational provider for Ledger state. It
registers the EF Core context factory and stores, supplies the migration set,
and replaces the provider-neutral migrator with PostgreSQL-aware migration and
schema-audit behavior.

## Requirements

- .NET 10
- A supported PostgreSQL server
- A protected connection string with permission to use the Ledger schema

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Persistence.PostgreSql
```

## Register

```csharp
using CanDoItAll.Ledger.Persistence.PostgreSql;

builder.Services.AddLedgerPostgreSqlPersistence(
    builder.Configuration.GetConnectionString("Ledger")
        ?? throw new InvalidOperationException(
            "Ledger connection string is missing."),
    configure: options =>
    {
        options.EnableRetryOnFailure = true;
        options.MaxRetryCount = 5;
        options.CommandTimeoutSeconds = 30;
    });
```

Configuration binding is also supported:

```csharp
builder.Services.AddLedgerPostgreSqlPersistence(
    builder.Configuration,
    "Ledger:PostgreSql");
```

The bound section maps to `LedgerPostgreSqlOptions`, including
`ConnectionString`, `MigrationsAssembly`, `CommandTimeoutSeconds`,
`EnableRetryOnFailure`, `MaxRetryCount`, and `MaxRetryDelay`.

## Apply Migrations

The host owns the startup migration policy:

```csharp
using CanDoItAll.Ledger.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;

await using var scope = app.Services.CreateAsyncScope();
CancellationToken cancellationToken = app.Lifetime.ApplicationStopping;
var factory =
    scope.ServiceProvider.GetRequiredService<IDbContextFactory<LedgerDbContext>>();
var migrator =
    scope.ServiceProvider.GetRequiredService<ILedgerSchemaMigrator>();

await using LedgerDbContext db =
    await factory.CreateDbContextAsync(cancellationToken);

LedgerSchemaMigrationResult result =
    await migrator.MigrateAsync(db, cancellationToken);
```

## Operational Notes

- Registration does not automatically apply migrations.
- Back up authoritative data and test a restore before schema changes.
- The migrator uses a PostgreSQL advisory lock to serialize concurrent
  migration attempts.
- Existing schema adoption is refused when required tables are missing or
  stored data violates current Ledger invariants.
- Retry-on-failure is disabled unless the host enables it.
- Keep connection strings out of committed configuration and package output.
- Validate health, a representative read, and a controlled write after
  migration.

See the
[container quickstart](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/getting-started/node-and-account-app.md),
[backup and restore guide](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/operations/backup-and-restore.md),
and [full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT License.
