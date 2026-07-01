# SB05 Semantic Invariants

## Alice Bob Commerce Validation

- Invariant ID: `bo:post:purchase-order-post`
- Source raw note: `RN-01` through `RN-07` required real Alice/Bob browser validation for long descriptions, sale setup, buy, dispatch, receipts, purchases, and gallery state.
- Expected behavior: Docker-hosted Alice and Bob AccountApps complete copy and original sale flows through browser-side vault unlock/signing, node API broadcast, seller refresh dispatch, and dashboard/detail/gallery verification.
- Disallowed shallow implementation: Seeding final database rows after the browser action, claiming success from unit tests alone, or skipping raw API corroboration for ownership and linked records.
- Failing-first test: `N/A process validation only; SB05 adds no production behavior and uses SB04 failing-first proof in bundle://proof/SB04/transcripts/dispatch-focused-tests-first.txt`.
- Passing test: `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt` and `bundle://proof/SB05/transcripts/playwright-alice-original-dashboard.txt` pass buyer and seller browser verification.
- Changed source files: SB05 is validation-only; implementation hashes are recorded in `bundle://proof/SB04/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB05/transcripts/api-original-sale-state.json` and `bundle://proof/SB04/transcripts/api-dispatch-state.json` corroborate browser screenshots with node API state.
- Red-team negative case: `bundle://proof/SB04/transcripts/anti-stub-audit.txt` confirms no hard-coded proof ids or VEFramework dependency in production/test scope.
- Downstream dependency check: `bundle://proof/SB05/transcripts/browser-result-summary.txt` and `bundle://proof/SB05/transcripts/browser-screenshot-inventory.txt` close the bundle-wide browser evidence set.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Browser-signed copy sale lifecycle | `bundle://proof/SB03/transcripts/playwright-bob-buy-bo-final-step.txt` | `bundle://proof/SB04/transcripts/playwright-alice-dispatch-refresh.txt` | `bundle://proof/SB04/transcripts/api-dispatch-state.json` | `bundle://proof/SB04/transcripts/anti-stub-audit.txt` |
| Browser-signed original sale lifecycle | `bundle://proof/SB05/transcripts/playwright-original-sale-e2e-rerun.txt` | `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt` | `bundle://proof/SB05/transcripts/api-original-sale-state.json` | `bundle://proof/SB04/transcripts/anti-stub-audit.txt` |
| Payment, receipt, purchase dashboard records | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` | `bundle://proof/SB05/transcripts/playwright-alice-original-dashboard.txt` | `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt` | `bundle://proof/SB05/transcripts/browser-screenshot-inventory.txt` |

## Final Review Answers

- Did Alice and Bob complete real browser-signed commerce flows? `Yes`.
- Did copy and original sale modes both dispatch? `Yes`.
- Are receipt and purchase records linked and visible? `Yes`.
- Are all raw notes closed with proof? `Yes`.
