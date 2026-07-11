# SB05 AccountApp Browser Validation And Closure

## Status

- `Completed`

## Objective

Rebuild AccountApp, visually and functionally test every supported/delegated transaction flow, repair failures, and close the bundle with durable proof.

## Covered Inputs

- R014-R015 and final proof for R001-R013/R016.

## Prerequisites

- SB03 and SB04 completed; prior critical manifests valid.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `bundle://shared-prompts/qa-prompt.md`

## Deliverables

- Rebuilt service, all-flow browser matrix, responsive screenshots/geometry, console/error audit, repairs, final tests/build, raw-note closure, verifier artifact, and completed validator pass.

## Dependency Impact

- Final critical closure. Any failed mode reopens its owner and invalidates final completion.

## Validation Depth

- Real AccountApp WASM browser proof with desktop/tablet/mobile, open overlays, invalid/valid interaction checks, plus red-team proof-manifest audit.

## Implementation Steps

1. Build/test and rebuild AccountApp Docker service.
2. Open `http://localhost:5081/` in one persistent browser tab.
3. Test chooser, every direct flow, and delegated Rule Token entry.
4. Test every multi-step wizard step and invalid/valid transitions.
5. Repair and retest any failure.
6. Run final clean tests/build, proof audit, raw-note closure, and completed validator.

## Do Not Do

- Do not close from BUnit only, screenshots without DOM assertions, or a subset of forms.

## Acceptance Checklist

- [x] Every catalog entry has browser proof or an explicit unavoidable blocker.
- [x] Invalid and valid states agree with Submit enabled state.
- [x] No clipping/overflow/layering defects at three viewports.
- [x] Console/network audit has no new app errors.
- [x] Raw request closes note by note.
- [x] `proof/SB05/manifest.md`, invariants, and verifier artifact exist.

## Proof Required

- Docker/test/build transcripts, browser screenshots and DOM/geometry assertions, console audit, hashes, source/anti-stub audit, final verifier, completed validator transcript.

## Browser Validation Logging

- AccountApp home/Send Transaction; 1920x1080, 1024x768, 390x844; exact actions/assertions/screenshots per flow and step.

## Progression Gate

- Final closure only after every row passes and validators pass completed stage.

## Suggested Agent Prompt

```text
Execute SB05 only. Rebuild AccountApp, prove every direct/delegated transaction flow at all viewports, repair failures, and close with artifact-backed evidence.
```

## C# Architecture Impact

- Final architecture audit; source changes only for observed defects.

## Boundary Ownership

- Repairs return to the owning subbundle boundary.

## Dependency Direction

- Confirm unchanged project graph.

## Pattern Decision

- Verify catalog/shell/composition decisions survived implementation.

## Testability Contract

- Browser proof complements, not replaces, direct component tests.

## Partial Class Policy

- Final no-partial audit.

## Architecture Proof Required

- ACP-03, hashes, source audit, direct tests, final verifier, explicit CodeAnalytics gap.
