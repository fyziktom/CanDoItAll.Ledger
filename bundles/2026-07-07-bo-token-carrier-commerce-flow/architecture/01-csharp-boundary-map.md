# C# Boundary Map

The carrier-token rule belongs at two levels:

- Domain layer: `CloneBusinessObjectRequest` and `BusinessObjectTransactionBuilder` must be able to build a clone transaction that consumes the source BO plus a reusable token source and creates a separate token-backed clone output.
- Component/service layer: UI and automation drafts should select the default reusable source token through one policy helper instead of ad hoc selection in each page or dispatcher.

The domain layer will not force all creates to be token-backed because tests and low-level APIs still need explicit main-only behavior. The shared UI/account layer will apply the default when a reusable source is available.

