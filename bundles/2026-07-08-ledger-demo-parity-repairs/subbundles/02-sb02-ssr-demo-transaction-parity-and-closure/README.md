# SB02 SSR Demo Transaction Parity And Closure

## Status

- `Completed`

## Objective

- Update and prove the Blazor SSR demo matches the WASM AccountApp transaction behavior for UTXO split/merge and one-token BO carrier defaults, then close the bundle.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `N004`
- `N005`
- `N006`

## Prerequisites

- SB01 is completed with artifact-backed proof.
- Prepared-stage validator passed.
- SSR development and real-node session source references still exist.

## Exact Source References

- repo://examples/CanDoItAll.Economy.Components.Demo/Program.cs
- repo://examples/CanDoItAll.Economy.Components.Demo/Components/Pages/Home.razor
- repo://examples/CanDoItAll.Economy.Components.Demo/Components/Pages/AdapterWorkspaceSamples.razor
- repo://examples/CanDoItAll.Economy.Components.Demo/CanDoItAll.Economy.Components.Demo.csproj
- repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs
- repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs
- repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs
- repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs
- repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs

## Deliverables

- SSR development workspace one-token BO carrier default.
- SSR development/session tests for UTXO split/merge vin/vout behavior.
- Sample adapter or component proof that `UtxoMerge` and `UtxoSplit` modes are accepted distinctly.
- Final proof artifacts, execution report, raw-note closure, and completed-stage validator transcript.

## Dependency Impact

- This subbundle closes the whole bundle.
- If SSR proof is weak, final raw-note closure for `N005` and `N006` remains open.

## Validation Depth

- Critical semantic proof.
- Failing-first and passing tests for SSR one-token default and UTXO split/merge.
- Build proof for SSR demo project or solution.
- Browser proof only if rendered UI text/markup changes; otherwise record explicit `N/A` with source/test rationale.

## C# Architecture Impact

- Local session/default/test updates only; no project references or partial classes.

## Boundary Ownership

- SSR sessions own host-specific orchestration and defaults.
- Shared account/builder code owns transaction assembly.
- Components demo page owns sample presentation only.

## Dependency Direction

- SSR demo and components services can depend on shared business-object/core services.
- Shared business-object/core services must not depend on SSR demo.

## Pattern Decision

- Preserve existing adapter/session facade; do not introduce new architecture pattern.

## Testability Contract

- SSR development session test must exercise behavior without a live node.
- Source assertions must prove real-node session already routes UTXO split/merge through `BusinessObjectAccount`.

## Partial Class Policy

- No new partial classes.

## Architecture Proof Required

- Source assertion that no new project references or partial classes were added.
- Test proof that SSR default/token behavior matches AccountApp.

## Implementation Steps

- Add failing-first SSR test for development workspace one-token BO create draft and/or split/merge behavior.
- Correct development workspace draft defaults if needed.
- Add or update sample adapter tests/source assertions for distinct UTXO modes.
- Run targeted tests and build.
- Capture source assertions, anti-stub audit, hashes, final validator, manifests, semantic invariants, and red-team/final verifier artifact.

## Do Not Do

- Do not add SSR-specific transaction assembly.
- Do not weaken real-node session behavior.
- Do not rely on browser screenshots when service-level tests are the actual behavior proof.

## Acceptance Checklist

- SSR development workspace create draft uses carrier token amount `1`.
- SSR development/session UTXO split/merge creates the same selected vin/vout shapes as WASM.
- Real-node SSR route remains through `BusinessObjectAccount`.
- Tests and build pass or an honest blocker is recorded.
- Final raw-note closure is complete.

## Proof Required

- Failing-first transcript.
- Passing test transcript.
- SSR demo build transcript.
- Source assertions transcript.
- Anti-stub audit transcript.
- Changed-file hashes transcript.
- Final verifier or red-team artifact.
- Completed-stage validator transcript.
- `bundle://proof/SB02/manifest.md`.
- `bundle://proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- Route: `examples/CanDoItAll.Economy.Components.Demo` home or adapter samples if visible markup changes.
- Viewport: large desktop first when browser proof is needed.
- Evidence: Playwright transcript and screenshot, or explicit `N/A` row if only service/test defaults changed.
- Record row in `reviews/01-execution-report.md`.

## Progression Gate

- Bundle may close. SB02 proof artifacts exist, raw notes are closed in the execution report, and the architecture gate has final pass evidence.

## Suggested Agent Prompt

- Execute SB02. Prove SSR one-token BO carrier defaults and UTXO split/merge parity through development/session tests and source assertions. Update final proof, raw-note closure, architecture gate, and completed-stage validator before closing.
