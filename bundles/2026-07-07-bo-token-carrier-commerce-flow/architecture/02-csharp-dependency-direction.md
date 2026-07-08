# C# Dependency Direction

- `BusinessObjects` remains independent of component/UI models.
- `Components.Services` may depend on `Components.Models` and `BusinessObjects`.
- `Components.Components.BusinessObjects` may use component model/service helpers.
- AccountApp pages may use component services/models, but not reach into transaction-builder internals.

No new project reference is planned. The token policy helper will live in `CanDoItAll.Economy.Components.Services` so adapters, dispatcher, and pages can share it without moving UI-only concerns into the core business object assembly.

