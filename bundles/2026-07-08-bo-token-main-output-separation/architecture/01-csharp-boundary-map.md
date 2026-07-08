# C# Boundary Map

## Target Boundary

- Node shape rule remains in `CanDoItAll.Economy.Ledger`.
- Transaction construction remains in `CanDoItAll.Economy.BusinessObjects`.
- UI draft and AccountApp adapter behavior remains in `CanDoItAll.Economy.Components`.

## Target Types

- `TransactionShapeValidator`: owns output-level asset-shape invariants.
- `BusinessObjectTransactionBuilder`: owns signed/unsigned account transaction output construction.
- `BusinessObjectCarrierTokenPolicy`: owns default carrier token selection for UI and commerce drafts.

## Responsibility Decisions

- Do not add a new project: this is a ledger invariant and builder repair inside existing project boundaries.
- Do not add partial classes.
- Do not use service-location or new runtime registration.
- Keep permission/rule authority behavior under existing token authority validators.

## Forbidden References

- Core and ledger projects must not reference components or UI.
- Components may depend on business-object/account APIs, not ledger internals.

