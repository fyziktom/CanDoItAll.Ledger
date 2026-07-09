# validation-docker-closure

## Status

- `Completed`

## Objective

- Prove the complete AccountApp account-entry repair in build/tests/real browser, rebuild dockerized apps, and close raw notes with artifact-backed evidence.

## Success Criteria

- Targeted tests pass.
- AccountApp build passes.
- Large-screen Playwright proof passes with screenshots.
- Docker compose rebuild passes.
- Completed-stage validator passes and raw-note closure is filled.

## Covered Inputs

- `N007`, `N008`, `N009`
- Requirements: `R007`, `R008`
- Closure proof for `R001` through `R006`

## Prerequisites

- `SB01` and `SB02` closure gates pass or any blocker is explicitly recorded.

## Exact Source References

- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css`
- `repo://docker-compose.yml`
- `repo://tests/CanDoItAll.Ledger.Tests`

## Deliverables

- Test/build/docker transcripts.
- Playwright transcript and screenshots.
- Final execution report rows, raw-note closure, proof manifests, semantic invariants, and red-team/final verifier artifact.

## Dependency Impact

- This is the final closure phase. Weak browser or docker proof blocks bundle completion.

## Validation Depth

- `End-to-end UI regression and closure`

## Implementation Steps

1. Run targeted tests.
2. Run AccountApp build.
3. Start required local/docker services for browser proof.
4. Use Playwright on large desktop viewport to capture locked/import/reset/unlocked screenshots and DOM assertions.
5. Run docker compose build/rebuild.
6. Update proof artifacts, manifests, execution report, raw-note closure, and final validator transcript.

## Scope Exceptions

- Small/medium viewport proof is intentionally not required.

## Do Not Do

- Do not mark browser proof passed from screenshots alone without DOM assertions and visual-review answers.
- Do not skip docker rebuild even if app build already passed.

## Acceptance Checklist

- Targeted tests pass.
- AccountApp build passes.
- Large-screen Playwright proof passes with screenshots.
- Docker compose build passes.
- Completed-stage validator passes.
- Every raw note is `Solved` or has an explicit blocker/follow-up.

## Proof Required

- `bundle://proof/SB03/transcripts/passing-tests.txt`
- `bundle://proof/SB03/transcripts/accountapp-build.txt`
- `bundle://proof/SB03/transcripts/playwright-large-screen.txt`
- `bundle://proof/SB03/transcripts/docker-compose-final-rebuild.txt`
- `bundle://proof/SB03/transcripts/docker-compose-ps.txt`
- `bundle://proof/SB03/transcripts/completed-stage-validator.txt`
- Browser screenshots under `bundle://proof/SB03/screenshots/`

## Browser Validation Logging

- Target route: AccountApp `/`.
- Viewport: large desktop, approximately `1600x1000`.
- Required actions/assertions: load locked setup; verify create/import/unlock/reset sections; navigate import steps; assert reset disabled/enabled rules; create/unlock a test account; verify no `.account-app__workspace-frame` when unlocked.
- Required screenshots: locked setup, import step 1, import step 2, reset guard, unlocked no-topbar.
- Review questions: text readable, no overlap/clipping, primary action hierarchy clear, destructive reset visually distinct, shared workspace is first unlocked surface.

## Progression Gate

- Bundle may close only when tests, build, Playwright proof, docker rebuild, manifests, raw-note closure, and completed-stage validator all pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
