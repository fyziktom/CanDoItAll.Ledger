# C# Dependency Direction

## Current Project References

- `examples/CanDoItAll.Economy.ConsoleDemo` references `CanDoItAll.Economy.BusinessObjects`, `Core`, `Cryptography`, `Ledger`, and `Sdk`.
- `examples/CanDoItAll.Economy.Components.Demo` references `CanDoItAll.Economy.BusinessObjects` and `CanDoItAll.Economy.Components`.
- `src/CanDoItAll.Economy.Components` references shared business-object and core services.
- `tests/CanDoItAll.Ledger.Tests` already references the production projects needed for adapter and domain proof.

## Target Project References

- No new project references are planned.
- Tests should use existing test project references.

## Forbidden References

- Production business-object/core projects must not reference demo projects.
- Shared transaction builder must not reference SSR, WASM, or console demo code.
- Components demo must not reference console demo.

## Cycle Risk

- Low if all changes stay inside existing demo projects, components services, and tests.
- Reopen the architecture gate if any new project reference is required.

## New Contract Projects Needed

- None.

## Build Test Proof Required

- Targeted tests for console/SSR parity.
- Build proof for `examples/CanDoItAll.Economy.ConsoleDemo`.
- Build proof for `examples/CanDoItAll.Economy.Components.Demo` or solution build if dependency warnings permit.

## CodeAnalytics Evidence

- Snapshot `snap-20260708181406-97cb2b5b-failed` failed before loading projects.
- Validation gap is accepted only because no project reference changes are planned and final closure must include direct source/build proof.
