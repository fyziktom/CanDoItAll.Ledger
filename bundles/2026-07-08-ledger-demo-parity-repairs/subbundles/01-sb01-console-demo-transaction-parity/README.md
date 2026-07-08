# SB01 Console Demo Transaction Parity

## Status

- `Completed`

## Objective

- Update the console demo so it exposes and teaches the same UTXO split/merge and BO token-only output semantics that work in the WASM AccountApp.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `N004`
- `N005`
- `N006`

## Prerequisites

- Prepared-stage bundle validator passes.
- Existing user-owned changes remain untouched unless directly in scope.
- Shared transaction builder and account APIs still expose UTXO split/merge.

## Exact Source References

- repo://examples/CanDoItAll.Economy.ConsoleDemo/DemoAccountDriver.cs
- repo://examples/CanDoItAll.Economy.ConsoleDemo/LearningScenarios.cs
- repo://examples/CanDoItAll.Economy.ConsoleDemo/README.md
- repo://examples/CanDoItAll.Economy.ConsoleDemo/CanDoItAll.Economy.ConsoleDemo.csproj
- repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs
- repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs
- repo://tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj
- repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectAdvancedTransactionTests.cs

## Deliverables

- Console driver helpers for UTXO split and merge that delegate to `BusinessObjectAccount`.
- Lesson/source update that explains plain UTXO split/merge separately from BO split payment/merge.
- Test coverage or source assertions proving console code does not assemble raw transaction rules locally.
- Proof artifacts under `proof/SB01/`.

## Dependency Impact

- SB02 depends on SB01 because final closure must show both requested non-WASM app surfaces are covered.
- Weak SB01 proof invalidates raw-note closure for `N002`, `N004`, and `N005`.

## Validation Depth

- Critical foundation semantic proof.
- Failing-first test or source assertion showing missing console UTXO parity before implementation.
- Passing tests for helper/lesson behavior.
- Build proof for console demo project.

## C# Architecture Impact

- Local helper/facade update only; no project boundary changes.

## Boundary Ownership

- Console driver owns demo ergonomics.
- `BusinessObjectAccount` owns behavior.
- `BusinessObjectTransactionBuilder` owns transaction assembly.

## Dependency Direction

- Console demo may depend on business-object/core/sdk projects.
- Business-object/core projects must not depend on console demo.

## Pattern Decision

- Use thin facade/helper methods, documented in `architecture/03-csharp-pattern-selection-records.md`.

## Testability Contract

- Tests must verify behavior without a live node when possible.
- Tests must fail if UTXO split/merge are only old BO split/merge labels.

## Partial Class Policy

- No new partial classes.

## Architecture Proof Required

- Source assertion that no raw `LedgerTransaction` assembly was added to console demo for split/merge.
- Build proof for console project.

## Implementation Steps

- Add failing-first tests or source assertion for missing console UTXO split/merge helper/lesson parity.
- Add `DemoAccountDriver` UTXO split/merge helper methods that call `BusinessObjectAccount`.
- Update lesson 7 and README text to distinguish plain UTXO split/merge from BO split payment/merge.
- Run targeted tests and console project build.
- Capture hashes, source assertions, and anti-stub audit.

## Do Not Do

- Do not hand-roll raw transaction assembly in console demo.
- Do not remove advanced BO split payment or BO merge lessons.
- Do not change shared ledger policy unless proof shows the shared policy is wrong.

## Acceptance Checklist

- Console driver exposes UTXO split and merge helpers.
- Console lesson covers UTXO split and merge separately from BO split/merge.
- Tests or source assertions prove helper calls use `BusinessObjectAccount`.
- Console project builds.
- `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md` exist.

## Proof Required

- Failing-first transcript.
- Passing test transcript.
- Console build transcript.
- Source assertions transcript.
- Anti-stub audit transcript.
- Changed-file hashes transcript.
- `bundle://proof/SB01/manifest.md`.
- `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- N/A for console-only changes.
- Record `N/A` row in `reviews/01-execution-report.md`.

## Progression Gate

- SB02 may start. SB01 tests/build/source assertions passed and the SB01 manifest plus semantic invariant contract exist.

## Suggested Agent Prompt

- Execute SB01. Keep console changes as thin wrappers over shared account APIs. Prove UTXO split/merge behavior, BO token-only explanation, build success, source assertions, hashes, and anti-stub audit before moving to SB02.
