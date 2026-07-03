# SB06 Docker Browser Closure

## Status

- `Completed`

## Objective

- Prove the finished bundle works in rebuilt Docker WASM apps and capture large-screen browser validation.

## Covered Inputs

- User explicitly required Docker rebuild/restart and Alice/Bob messaging with/without attachments.

## Prerequisites

- SB01 through SB05 complete.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://examples/CanDoItAll.Economy.AccountApp`
- `repo://src/CanDoItAll.Economy.Node`

## Deliverables

- Docker compose build and up for economy node plus account apps.
- API health proof.
- Alice/Bob message send/read proof with and without attachment.
- Raw tx screenshot showing no subject/attachment locator leak.

## Dependency Impact

- Final closure gate for the bundle.

## Validation Depth

- Docker build/runtime, API validation, browser screenshot.

## Implementation Steps

1. Run focused tests and AccountApp build.
2. Rebuild Docker images for node and account apps.
3. Restart containers and verify health.
4. Use browser/Playwright proof on large desktop viewport.
5. Update execution report.

## Do Not Do

- Do not claim closure from local tests alone.

## Acceptance Checklist

- [ ] Docker build succeeds.
- [ ] Containers restart and health endpoint reports ok.
- [ ] Alice/Bob secure messages work with and without attachments.
- [ ] Screenshot shows raw tx and layout is usable on large desktop.

## Proof Required

- Command transcripts, API/browser validation notes, screenshot path.

## Browser Validation Logging

- Large desktop viewport only; capture account workspace and raw tx dialog.

## Progression Gate

- Final closure can be marked complete only after all proof is recorded.

## Suggested Agent Prompt

- Rebuild Docker apps, validate Alice/Bob secure messaging end to end, capture raw tx screenshot, and close the execution report.
