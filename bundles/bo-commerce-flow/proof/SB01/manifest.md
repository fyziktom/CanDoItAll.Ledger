# SB01 Proof Manifest

## Status

- Subbundle: `SB01 Long Description Persistence And Display`
- Closure decision: `Passed`
- Closed at: `2026-06-30`
- Owned requirements: `REQ-001`
- Owned raw notes: `RN-01`

## Production Changes

- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
  - Added a dedicated long-description section in the BO details overview with `data-testid="cda-bo-long-description"`.
  - Kept short description and long body separate so a populated short description no longer hides payload `text`.
- `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`
  - Added readable, wrapping detail styles for long body text.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectLongDescriptionComponentTests.cs`
  - Added component and payload round-trip tests for long body text with a non-empty short description.

## Evidence

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-long-description-test.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing-long-description-test.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Semantic invariants: `bundle://proof/SB01/semantic-invariants.md`

| Evidence | Path | Result |
| --- | --- | --- |
| Failing-first component assertion | `bundle://proof/SB01/transcripts/failing-first-long-description-test.txt` | Failed before fix because `[data-testid='cda-bo-long-description']` did not exist. |
| Passing targeted tests | `bundle://proof/SB01/transcripts/passing-long-description-test.txt` | `Passed!` 2 tests. |
| API raw-state assertion | `bundle://proof/SB01/transcripts/api-long-description-state.txt` | Reported Alice object exposes `stateJson.text`. |
| Browser DOM and raw-state proof | `bundle://proof/SB01/transcripts/playwright-long-description-browser-proof.txt` | Details overview and raw-state tab both include long body text. |
| Browser screenshots | `bundle://proof/SB01/browser/long-description-details.png`, `bundle://proof/SB01/browser/long-description-raw.png` | Captured at `1600x900`. |
| Review screenshot | `bundle://proof/SB01/browser/long-description-details-review.png` | Downscaled copy for visual inspection. |
| Source assertions | `bundle://proof/SB01/transcripts/source-assertions.txt` | Shows production/test selectors and long-description accessors. |
| Anti-stub audit | `bundle://proof/SB01/transcripts/anti-stub-audit.txt` | No hard-coded reported object id/hash or placeholder implementation found. |
| Changed-file hashes | `bundle://proof/SB01/transcripts/changed-file-hashes.txt` | Before/after SHA-256 recorded for changed files, including reported hash `2f9109c552bd0679b84e94a93fbbb9eb51e370436b97ad6c0b3ff840d70ddedb`. |
| Docker rebuild | `bundle://proof/SB01/transcripts/docker-accountapp-rebuild.txt` | AccountApp image rebuilt successfully. |
| App recreate and health | `bundle://proof/SB01/transcripts/docker-accountapp-recreate-health.txt` | Recreated AccountApp containers; HTTP health returned `200`. |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Payload `text` state | `bundle://proof/SB01/transcripts/passing-long-description-test.txt` | `bundle://proof/SB01/transcripts/api-long-description-state.txt` | `bundle://proof/SB01/transcripts/playwright-long-description-browser-proof.txt` | `bundle://proof/SB01/transcripts/failing-first-long-description-test.txt` |
| Details dialog long-description record | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` | `bundle://proof/SB01/browser/long-description-details.png` | `bundle://proof/SB01/transcripts/passing-long-description-test.txt` | `bundle://proof/SB01/transcripts/anti-stub-audit.txt` |
| Data-driven implementation | `bundle://proof/SB01/transcripts/source-assertions.txt` | `bundle://proof/SB01/transcripts/anti-stub-audit.txt` | `bundle://proof/SB01/transcripts/api-long-description-state.txt` | `bundle://proof/SB01/transcripts/anti-stub-audit.txt` |

## Closure Notes

- The reported Alice BO already contained `stateJson.text`; the defect was that the details overview did not render long body text separately from short description.
- Wizard capture and typed parser round-trip are covered by tests and did not require production changes in this subbundle.
- SB02 can depend on BO long body/raw-state visibility for commerce payment, purchase, and receipt records.
