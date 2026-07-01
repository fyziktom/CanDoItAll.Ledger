# SB04 Proof Manifest

## Status

- Subbundle: `SB04 Dispatch Automation`
- Closure decision: `Passed`
- Closed at: `2026-07-01`
- Owned requirements: `REQ-006`, `REQ-007`, `REQ-008`, `REQ-009`
- Owned raw notes: `RN-02`, `RN-05`, `RN-06`, `RN-07`

## Production Changes

- `repo://src/CanDoItAll.Economy.Components/Services/BusinessObjectCommerceDispatcher.cs`
  - Added shared seller-side dispatch orchestration that runs through the active workspace adapter, preserving WASM browser signing.
  - Handles pending payment discovery, seller/sale validation, deterministic clone ids, original transfer, max-clone rejection, sale counter updates, receipt/purchase creation, payment status updates, and retry/idempotency checks.
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
  - Invokes commerce dispatch after workspace load/refresh, reloads the workspace after accepted dispatch actions, and reports dispatch success/failure through notification popups only.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCommerceDispatcherTests.cs`
  - Added coverage for copy sale dispatch, original sale dispatch, duplicate no-op, and max-clone rejection.

## Evidence

- Failing-first transcript: `bundle://proof/SB04/transcripts/dispatch-focused-tests-first.txt`
- Passing transcript: `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Semantic invariants: `bundle://proof/SB04/semantic-invariants.md`

| Evidence | Path | Result |
| --- | --- | --- |
| Failing-first/compile guard | `bundle://proof/SB04/transcripts/dispatch-focused-tests-first.txt` | Failed before final fix on missing dispatcher test import. |
| Passing targeted tests | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` | `Passed!` 12 commerce tests including dispatcher tests. |
| AccountApp build | `bundle://proof/SB04/transcripts/accountapp-build.txt` | Build succeeded, 0 errors. |
| Docker rebuild/recreate | `bundle://proof/SB04/transcripts/docker-accountapp-build.txt`, `bundle://proof/SB04/transcripts/docker-accountapp-recreate.txt` | AccountApp image rebuilt and Alice/Bob/Lab containers recreated. |
| Copy-sale browser dispatch | `bundle://proof/SB04/transcripts/playwright-alice-dispatch-refresh.txt` | Pending Bob payment became dispatched, clone delivered to Bob, receipt/purchase found. |
| Copy-sale API state | `bundle://proof/SB04/transcripts/api-dispatch-state.json` | Original post sale has `soldCount=1`, payment is `Dispatched`, clone owner is Bob, receipt/purchase exist. |
| Browser screenshots | `bundle://proof/SB04/browser/seller-dispatch-refresh.png`, `bundle://proof/SB04/browser/seller-payment-dispatched-detail.png`, `bundle://proof/SB04/browser/seller-receipt-detail.png` | Seller dashboard and details captured at desktop width. |
| Source assertions | `bundle://proof/SB04/transcripts/source-assertions.txt` | Shows deterministic clone, original transfer, receipt/purchase/payment update, rejection, and host refresh hook. |
| Anti-stub audit | `bundle://proof/SB04/transcripts/anti-stub-audit.txt` | No TODO, VEFramework dependency, or hard-coded proof object/payment ids in SB04 production/test source. |
| Changed-file hashes | `bundle://proof/SB04/transcripts/changed-file-hashes.txt` | SHA-256 recorded for dispatcher, host, and dispatcher tests; dispatcher hash `92C31520B4A9E854D05D126CBD371FF07F2E4A602567716F86F82D4F4629BCD5`. |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Copy sale dispatch clone record | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` | `bundle://proof/SB04/transcripts/playwright-alice-dispatch-refresh.txt` | `bundle://proof/SB04/transcripts/api-dispatch-state.json` | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` |
| Original sale dispatch transfer state | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` | `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt` | `bundle://proof/SB05/transcripts/api-original-sale-state.json` | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` |
| Seller refresh adapter hook | `bundle://proof/SB04/transcripts/source-assertions.txt` | `bundle://proof/SB04/transcripts/playwright-alice-dispatch-refresh.txt` | `bundle://proof/SB04/browser/seller-dispatch-refresh.png` | `bundle://proof/SB04/transcripts/anti-stub-audit.txt` |

## Closure Notes

- Dispatcher is intentionally adapter-based, not node-session-based, so WASM AccountApp signs dispatch transactions with the seller's browser vault.
- Main-currency copy and original sale paths are implemented and proven. Token-priced purchase remains guarded by SB03 scope exception.
