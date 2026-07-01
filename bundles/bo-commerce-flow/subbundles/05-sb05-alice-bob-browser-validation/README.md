# SB05 Alice Bob Browser Validation

## Status

- `Completed`

## Objective

Run real end-to-end validation with Alice and Bob AccountApps against the local node/IPFS/docker environment and close every raw note with artifact-backed evidence.

## Covered Inputs

- `RN-01` through `RN-07`
- `REQ-001` through `REQ-009`

## Prerequisites

- `SB01` through `SB04` closure gates pass.
- Node, IPFS, and AccountApp containers or dev servers are running.
- Alice and Bob import files are available or regenerated.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://examples/CanDoItAll.Economy.AccountApp`
- `repo://bundles/bo-commerce-flow/reviews/01-execution-report.md`
- `repo://tests/CanDoItAll.Ledger.Tests`

## Deliverables

- Playwright script(s) or transcripts covering Alice/Bob flow.
- Screenshots for long description, sale setup, buy page, payment, seller dispatch, buyer received BO, payment/receipt dashboard, and related details.
- Final execution report with raw-note closure.
- Completed proof manifests and semantic invariants for all critical subbundles.

## Dependency Impact

- This is the final closure phase.
- Failure reopens the relevant implementation subbundle.
- Expected local app endpoints for proof: Alice `http://127.0.0.1:5081`, Bob `http://127.0.0.1:5082`, Lab `http://127.0.0.1:5083`, node `http://127.0.0.1:8080`, IPFS `http://127.0.0.1:5001`.

## Validation Depth

- `End-to-end regression and closure`
- Requires browser proof and final Semantic Adequacy Gate audit.

## Implementation Steps

1. Build and start required services/apps.
2. Import/unlock Alice and Bob.
3. Validate long post description display/raw state.
4. Alice lists a BO for original sale and Bob buys it.
5. Alice refresh dispatches original, Bob sees ownership, receipts/payments open details.
6. Alice lists a BO for copy sale with max clones and Bob buys a copy.
7. Repeat refresh to prove no duplicate dispatch.
8. Update execution report, manifests, traceability, and root README closure status.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not seed final positive results directly in the database.
- Do not skip screenshots for dialogs/dashboard details.
- Do not mark raw notes solved from unit tests alone.

## Acceptance Checklist

- [x] Alice and Bob real AccountApps complete the flow using browser signing.
- [x] Long description is visible and raw-state proven.
- [x] Original sale changes ownership once.
- [x] Copy sale creates deterministic clone and respects max clone count.
- [x] Dashboard payments/receipts click through to correct details.
- [x] Final validators pass.

## Proof Required

- `bundle://proof/SB05/manifest.md`
- `bundle://proof/SB05/semantic-invariants.md`
- Playwright transcript and screenshots.
- Build/test transcript.
- Final raw-note closure table.
- Completed-stage bundle validator transcript.

## Browser Validation Logging

- Routes: Alice `/`, Bob `/`, Bob Buy BO link, Lab `/` if needed.
- Viewport: `1600x900`.
- Actions: import/unlock, create/list sale, open link, buy, seller refresh, open dashboards/details, duplicate refresh.
- Screenshots: `bundle://proof/SB05/browser/*.png`.
- Review questions: all dialogs readable, no clipping, full desktop width used, related links point to correct BO/payment/receipt.

## Progression Gate

- Workflow closes only when final raw-note closure says `Solved` for all raw notes or a concrete follow-up subbundle is created for any exception.

## Suggested Agent Prompt

```text
Implement SB05 only after SB01-SB04 are closed. Run the real Alice/Bob browser validation and update final closure artifacts.
```
