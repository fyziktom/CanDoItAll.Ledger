# ADR-0006: EF Core primary persistence

## Status

Accepted.

## Context

The current CanDoItAll repository uses EF Core as its primary persistence stack. The economy module should be compatible with CanDoItAll without introducing a second default ORM.

## Decision

Use EF Core for production persistence and PostgreSQL through `Npgsql.EntityFrameworkCore.PostgreSQL`.

## Consequences

- Migrations integrate better with the existing stack.
- Query/projection code remains familiar to CanDoItAll contributors.
- Performance-sensitive paths can use EF transactions plus provider-specific row locks.
- Direct SQL or Dapper can be considered only after a measured optimization cycle.
