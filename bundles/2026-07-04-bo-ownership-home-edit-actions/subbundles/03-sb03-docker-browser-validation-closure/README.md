# SB03 Docker Browser Validation Closure

## Status

- `Completed`

## Objective

- Prove the implemented behavior with real validation, Docker app rebuilds, browser proof, raw-note closure, and final bundle validation.

## Success Criteria

- Targeted tests pass.
- Broader build/test validation passes or any failure is unrelated and documented with evidence.
- Docker app images rebuild.
- Browser proof covers Home ownership verification and BO details edit/update staging.
- Critical proof manifests and semantic invariant contracts exist for completed critical subbundles.
- Raw note closure rows are no longer pending.

## Covered Inputs

- N003: "you must do real validations and rebuild apps in dockers to test it in them."
- R007: final validation and Docker/browser proof.
- Closure for N001 and N002 after integrated proof.

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.
- No open implementation blocker remains in the execution report.

## Exact Source References

- repo://docker/accountapp/Dockerfile
- repo://docker/node/Dockerfile
- repo://docker-compose.yml
- repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor
- repo://examples/CanDoItAll.Economy.AccountApp/Program.cs
- repo://src/CanDoItAll.Economy.Node/Dockerfile
- repo://tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj

## Deliverables

- Test/build transcripts under `proof/SB03/transcripts/`.
- Docker rebuild transcripts under `proof/SB03/transcripts/`.
- Playwright screenshots under `proof/SB03/browser/`.
- Final red-team proof artifact that re-reads critical manifests.
- Updated execution report, raw-note closure, root README validation summary, and completed-stage validator transcript.

## Dependency Impact

- This is the final closure phase. Any weak proof reopens SB01 or SB02 instead of being converted into residual risk.

## Validation Depth

- End-to-end regression and process-critical closure with Docker proof, browser proof, final verifier artifact, and completed-stage bundle validation.

## Implementation Steps

1. Run targeted tests for SB01/SB02.
2. Run project or solution build/test validation.
3. Rebuild Docker app images requested by the user.
4. Start the needed app target and run Playwright browser proof.
5. Create final verifier/red-team artifact checking proof manifests and fake-proof resistance.
6. Update execution report and raw-note closure.
7. Run completed-stage validator and repair any failure.

## Scope Exceptions

- None planned. If Docker is unavailable, record it as a blocker, not a passed closure.

## Do Not Do

- Do not replace Docker rebuilds with local builds.
- Do not replace browser proof with bUnit tests.
- Do not mark raw notes solved without concrete proof citations.
- Do not close if critical manifests or referenced transcript paths are missing.

## Acceptance Checklist

- Targeted tests pass.
- Build/test validation is recorded.
- Docker rebuild transcripts exist.
- Playwright screenshots and assertions exist.
- Proof manifests cite existing artifacts.
- Raw note closure is `Solved` or honestly blocked.
- `--stage completed` validator passes.

## Proof Required

- Transcript for targeted tests.
- Transcript for broader test/build validation.
- Transcript for Docker rebuild commands.
- Playwright transcript/screenshot evidence.
- Anti-stub audit transcript covering changed production files.
- Final verifier artifact under `proof/SB03/red-team-final.md`.
- Completed-stage bundle validator transcript.
- Final proof manifest: `bundle://proof/SB03/manifest.md`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Final red-team artifact: `bundle://proof/SB03/red-team-final.md`

## Browser Validation Logging

- Route: Docker-served AccountApp `/` where available, otherwise documented blocker with local app proof as supporting evidence only.
- Viewport: large desktop.
- Actions: open Home Security ownership verifier, open BO details, open Edit dialog, verify no BO details scanner, verify update staging UI.
- Screenshots: `bundle://proof/SB03/browser/docker-home-security.png` and `bundle://proof/SB03/browser/docker-bo-edit-dialog.png`.
- Review questions: Home-only verifier is clear, edit dialog is readable, overlays are not clipped, no harmful overflow, action hierarchy is understandable.

## Progression Gate

- Completed: Docker/browser proof, proof manifests, raw-note closure, and completed-stage validation were recorded.

## Suggested Agent Prompt

```text
Implement SB03 only after SB01 and SB02 pass. Run real tests, rebuild Docker app images, run browser proof, update proof manifests and execution report, audit raw notes, and run completed-stage validation. Reopen earlier subbundles if proof contradicts their claims.
```
