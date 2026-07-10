# SB08 — PostgreSQL migration and existing-schema adoption

Status: Complete — 2026-07-10

## Owned requirement

RQ-025 / R-001: ship a reviewed PostgreSQL migration baseline and a fail-closed adoption path for existing `EnsureCreated` databases.

## Prerequisites

- SB04 relational constraints and PostgreSQL concurrency proof remain trusted.
- Production still forbids `EnsureCreated`.

## Acceptance checklist

- provider migration assembly is selected by default;
- new empty PostgreSQL database migrates to the current model;
- an existing complete schema with no migration history is audited and baselined transactionally;
- missing tables or invalid output/UTXO rows block adoption before history is written;
- constraint and protocol-v2 upgrade DDL is idempotent for previously `EnsureCreated` schemas;
- Node `Migrate` mode and ledger reset use the same migrator seam.

## C# Architecture Impact

Persistence gets a narrow schema-migrator contract; the PostgreSQL provider owns adoption SQL. Node only selects schema mode.

## Boundary Ownership

`Persistence.EFCore` owns `IEconomySchemaMigrator`; `Persistence.PostgreSql` owns the PostgreSQL implementation and migrations; Node composes it.

## Dependency Direction

PostgreSQL -> EFCore/Core remains valid. Node -> PostgreSQL is existing composition-root direction. EFCore must not reference Npgsql.

## Pattern Decision

Adapter/strategy: normalize provider migration behind a small contract because adoption is provider-specific. Rejected: SQL in Node, silent `EnsureCreated`, or blindly inserting migration history.

## Testability Contract

Direct audit/adoption tests against disposable PostgreSQL databases plus a DI composition test. Invalid rows must leave the baseline unapplied.

## Partial Class Policy

No new partial class.

## Architecture Proof Required

Before/after project references, migration inventory, new/legacy database transcripts, anti-stub audit, and architecture review.

## Progression gate

SB09/SB10 may proceed after migration ownership and existing-schema fail-closed behavior are executable.

## Closure evidence

- Two provider migrations are present and an idempotent script is archived under `../../proof/second-wave/`.
- Four PostgreSQL tests cover empty, legacy, invalid legacy, and two-node concurrent startup paths.
- A session advisory lock spans audit, baseline adoption, EF migration, and final migration-set verification.
- The retained Docker database adopted both migrations and exposes the five output plus five UTXO checks.
