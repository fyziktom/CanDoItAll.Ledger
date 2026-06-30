# SB03 Browser Proof And Closure

## Status

- Status: Completed

## Objective

Run final tests, rebuild/restart the Docker-hosted apps, capture Playwright proof for refresh, notification, and large-screen width behavior, then close raw notes.

## Covered Inputs

- RN-01
- RN-02
- RN-03
- RN-04

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj`
- `repo://examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj`
- `repo://src/CanDoItAll.Economy.Node/CanDoItAll.Economy.Node.csproj`

## Deliverables

- Passing test transcript.
- AccountApp and node builds/rebuilds.
- Docker stack running.
- Playwright screenshots and DOM assertions for:
  - full-width desktop shell;
  - no inline result/refresh alert after transaction send;
  - account refresh after new tx without browser page reload;
  - cache-supporting behavior through tests/source assertions.
- Final raw-note closure table.

## Dependency Impact

- Final closure depends on this subbundle.

## Validation Depth

- Critical final closure.
- Requires artifact-backed manifest, semantic invariants, browser screenshots, final tests, and raw-note audit.

## Implementation Steps

1. Run full test suite.
2. Build AccountApp and Node.
3. Rebuild/restart Docker apps if needed.
4. Use Playwright on `http://localhost:5081`.
5. Capture screenshots and DOM assertions.
6. Update execution report and raw-note closure.

## Do Not Do

- Do not claim browser proof from screenshots alone; include DOM/assertion details.
- Do not close a raw note if it is only partially implemented.

## Acceptance Checklist

- [x] Tests pass.
- [x] Docker stack is running.
- [x] Browser proof exists for all user-visible notes.
- [x] Final bundle status and closure matrix are synchronized.

## Proof Required

- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`
- Browser screenshots under `bundle://proof/SB03/`.
- Final validator transcript.

## Browser Validation Logging

- Required in `reviews/01-execution-report.md`: route, viewport, actions, DOM assertions, screenshot paths, and pass/fail result.
- Large-screen desktop viewport must be first because RN-04 is desktop-specific.

## Progression Gate

- The bundle may close only after this subbundle proves every raw note as solved or explicitly records a follow-up/blocker.

## Suggested Agent Prompt

Execute final proof for the whole bundle with Playwright and tests. Close raw notes only with artifact-backed evidence.
