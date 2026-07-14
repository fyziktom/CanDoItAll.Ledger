# Pattern Selection Record: Legacy Lineage Compatibility Adapter

## Context

Historic V1/V2 transactions serialize a stable value named `ObjectId`, while the corrected public identity is the finalized output outpoint. Existing hashes must replay unchanged without allowing the legacy value to remain a public ID.

## Forces

- extension growth: a future V3 may remove the legacy field;
- multiple implementations: V1/V2 compatibility and canonical public projection;
- construction complexity: low;
- external SDK isolation: Node contracts must not leak storage semantics;
- runtime selection: transaction version selects canonical wire semantics;
- testability: conversion and generation must be independently testable;
- dependency direction: primitives remain in Core and adapters point inward.

## Selected pattern

Adapter, implemented as narrow conversion functions at transaction/persistence/transport boundaries. `BusinessObjectLineageKey` adapts the legacy wire value; `BusinessObjectId.FromOutPoint` is the only public identity constructor used by projections.

An injected key factory is used only where a new opaque lineage is required. It contains no transaction behavior.

## Rejected alternatives

- simple rename only: would keep arbitrary values exposed as BO IDs;
- serialize the target outpoint: creates a transaction-hash circular dependency;
- replace V1/V2 immediately: breaks historical replay and stored transactions;
- service locator: hides the key-generation dependency;
- facade over all BO behavior: would create a new monolith;
- partial-class split: moves lines without changing ownership.

## New types and projects

| Type | Project | Responsibility |
|---|---|---|
| `BusinessObjectLineageKey` | Core | Stable internal V1/V2 correlation value. |
| `BusinessObjectId.FromOutPoint/TryGetOutPoint` | Core | Canonical public identity conversion and validation. |
| `IBusinessObjectLineageKeyFactory` | BusinessObjects | Test seam for opaque root-lineage generation. |
| payload reference records/reader | BusinessObjects | Typed source relationships in BO data. |
| `LedgerBusinessObjectOutputDetails` | BusinessObjects | Public summary metadata with an outpoint-derived ID and no implicit lineage. |
| `BusinessObjectTransactionHistory` | BusinessObjects | Select versions through canonical reference chains with a trusted in-process compatibility fallback. |
| `EconomyNodeLedgerTransactionSummary` | SDK/Node | Lineage-free public transaction transport whose BO IDs are derived from transaction output positions. |
| `EconomyNodeSpendable*` DTOs | SDK/Node | Keep canonical identity and V1/V2 correlation in separately named fields and validate their binding. |

## Test plan

| Test | Behavior proven |
|---|---|
| canonical identity round trip | Only `txid:index` maps to/from an outpoint. |
| V1/V2 canonicalization characterization | Renaming the domain concept does not alter historical core bytes/hash. |
| create confirmation | Returned/current BO ID equals the created output outpoint. |
| update/transfer confirmation | Current ID advances and previous ID remains a source. |
| clone/merge payload | Source canonical IDs are present with the correct relationship. |
| node query/DTO | Transaction reads omit lineage; spendables preserve required correlation only under `lineageKey`; every `objectId` remains canonical and bound to its output. |
| history selection | Canonical `previous-version` chains work without exposing lineage; trusted legacy/in-process fallback remains characterized. |

## Proof that this is not fake separation

Builders stop accepting target IDs, projections stop returning stored correlation values, and UI/API code stops generating them. Tests fail if the old `bo:type:name:*` value is returned as a BO ID.
