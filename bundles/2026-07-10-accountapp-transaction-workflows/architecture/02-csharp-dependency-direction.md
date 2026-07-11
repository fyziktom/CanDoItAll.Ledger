# C# Dependency Direction

## Current And Target

```text
AccountApp (composition)
  -> Economy.Components (UI + UI models)
     -> Economy.BusinessObjects / Economy.Core (domain contracts)
```

- No target project reference changes.
- New UI models and components remain in `CanDoItAll.Economy.Components`.
- Domain projects must not reference Components.
- Mode forms may depend on `IBusinessObjectView`, `LedgerSpendableOutput`, token/address models, and the draft record already exposed to Components.

## Forbidden Directions

- BusinessObjects/Core -> Components.
- Form components -> AccountApp concrete services.
- Validation helpers -> DialogService/NotificationService/network clients.
- Components -> service locator for transaction form discovery.

## Cycle Risk

Low if all work remains in the existing Components project. A new project reference is a bundle repair trigger.

## Proof

- `dotnet build CanDoItAll.Ledger.slnx` or container-equivalent build.
- Project file diff shows no new references.
- Source audit shows no `IServiceProvider` lookup in new transaction code.

## CodeAnalytics

Unavailable for reliable before-state proof; snapshot `snap-20260710162123-996d4827-failed` is recorded as a validation gap.

