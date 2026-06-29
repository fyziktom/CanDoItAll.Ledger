# SB07 Integration Browser Proof Hardening

## Status

- `Completed`

## Objective

- Perform final end-to-end validation, browser hardening, SSR regression checks, no-key-leak review, workbook reconciliation, and raw-note closure for the AccountApp initiative.

## Success Criteria

- AccountApp end-to-end browser flow passes from clean profile: create/import/unlock, load transactions/UTXOs/details, assemble/sign/broadcast.
- IndexedDB contains encrypted key data only.
- Network captures show no raw private key, password, or unsigned transaction draft sent to node.
- SSR node/component demo behavior still passes.
- Execution report closes every subbundle gate and raw note with proof.
- Planning workbook reflects final implemented status.

## Covered Inputs

- RQ29, RQ30, RQ31, RQ32, plus final validation for all earlier requirements.

## Prerequisites

- SB06 progression gate passed.
- All prior subbundle proof manifests exist.

## Exact Source References

- repo://CanDoItAll.Ledger.slnx
- repo://examples
- repo://src
- repo://tests
- bundle://reviews/01-execution-report.md
- bundle://plan/01-phase-plan.md
- bundle://plan/account-wasm-pwa-planning.xlsx

## Deliverables

- End-to-end browser test suite or scripted proof covering the complete AccountApp flow.
- No-key-leak audit artifact for DOM, console, network, IndexedDB, local storage, and logs.
- SSR regression proof.
- Updated `reviews/01-execution-report.md` with subbundle gates, browser analytics, analytics review, and raw note closure.
- Updated workbook with final statuses and risk closure.
- Final validator pass for completed bundle stage when implementation is done.

## Dependency Impact

- This subbundle controls final closure. Weak proof here invalidates the entire initiative.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Read all prior subbundle manifests and unresolved risks.
2. Run full .NET test suite or justified scoped suite plus all browser tests.
3. Run AccountApp from a clean browser profile against a test node.
4. Capture screenshots, network traces, IndexedDB/local storage inspection, and console review.
5. Run SSR node/component regression checks.
6. Update workbook and execution report.
7. Run bundle validator in completed mode and repair closure gaps.

## Scope Exceptions

- Do not implement new feature scope here unless required to repair a failed acceptance criterion from earlier subbundles.

## Do Not Do

- Do not mark raw notes closed without artifact-backed proof.
- Do not replace failing browser proof with manual claims.
- Do not skip mobile layout checks if AccountApp UI changed.

## Acceptance Checklist

- Clean-profile browser flow passes.
- Browser storage and network audit shows no key leakage.
- PWA assets and service worker behavior are validated.
- SSR regression proof is captured.
- All subbundle statuses are `Completed` or explicitly `Blocked` with accepted reason.
- Bundle completed-stage validator passes after implementation.

## Acceptance Result

- `Passed`: final proof reuses the AccountApp browser run for storage/network assertions, verifies PWA manifest and service worker, confirms SSR demo build, confirms scoped AccountApp build, confirms ledger test project passed 188/188 tests, and reconciles workbook/status documents.

## Proof Required

- `proof/SB07/manifest.md` with all command transcripts, screenshots, traces, hashes, and final audit summary.
- Browser validation analytics rows populated in `reviews/01-execution-report.md`.
- Raw note closure table populated with proof references.
- Workbook updated and saved.
- Completed-stage bundle validator output.

## Browser Validation Logging

- Target route: AccountApp and SSR demo/node console.
- Viewports: desktop 1440x1000, mobile 390x844, and any affected intermediate width.
- Actions: clean profile, create/import/unlock, load API data, sign/broadcast transaction, inspect storage/network, installability/PWA check, SSR regression route pass.
- Screenshots/evidence: `proof/SB06/screenshots/*.png`, `proof/SB06/accountapp-browser-proof-output.json`, `proof/SB07/no-key-leak-audit.md`, `proof/SB07/manifest.md`, and `proof/SB07/validator-result.md`.
- Review questions: Does the evidence prove the actual production/browser path rather than a stub, fixture-only path, or server-side shortcut?

## Progression Gate

- `Passed`: every raw note and critical subbundle gate is closed with artifact-backed proof and validator checks passed.

## Suggested Agent Prompt

```text
Implement SB07 only after all prior subbundles are complete. Validate the complete AccountApp browser path, no-key-leak posture, PWA behavior, SSR regressions, workbook status, and final bundle closure. Do not add new scope except to repair failed acceptance criteria.
```
