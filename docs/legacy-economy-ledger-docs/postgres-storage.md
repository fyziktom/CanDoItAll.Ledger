# PostgreSQL storage

PostgreSQL is the first production database target, but it is accessed through EF Core.

Use:

```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

Do not implement direct PostgreSQL persistence before the EF confirmation path is complete.

## Migrations

EF migrations are the source of truth. SQL files under `sql/migrations` are review sketches only.

## Partitioning

Start without partitioning. Add partitioning after measured pressure appears. Keep `Economy_UnspentOutputs` hot and compact.
