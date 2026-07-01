# SB04 Semantic Invariants

## Seller Refresh Dispatch

- Invariant ID: `BusinessObjectCommerceDispatcher`
- Source raw note: `RN-02`, `RN-05`, `RN-06`, and `RN-07` required seller refresh dispatch, clone/original delivery, receipt/purchase records, and no VEFramework copy.
- Expected behavior: Seller workspace refresh processes only the seller's pending payments, validates sale state, dispatches original or deterministic clone once, updates payment status, and creates linked purchase and receipt records.
- Disallowed shallow implementation: Dispatching from UI-only state, bypassing seller ownership checks, creating duplicate deliveries on refresh, or rendering raw notification text into workspace content.
- Failing-first test: `bundle://proof/SB04/transcripts/dispatch-focused-tests-first.txt` captures the compile guard before the dispatcher test fix.
- Passing test: `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` passes copy, original, duplicate, and max-clone dispatcher tests.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Services/BusinessObjectCommerceDispatcher.cs`, `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`, and `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCommerceDispatcherTests.cs`.
- Production assertions: `bundle://proof/SB04/transcripts/playwright-alice-dispatch-refresh.txt` and `bundle://proof/SB04/transcripts/api-dispatch-state.json` prove copy-sale dispatch from the hosted AccountApp.
- Red-team negative case: `bundle://proof/SB04/transcripts/anti-stub-audit.txt` confirms no TODO, VEFramework dependency, or hard-coded proof object/payment ids in SB04 scope.
- Downstream dependency check: `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt` proves SB05 can consume dispatcher output for original-sale ownership and dashboard state.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Copy dispatch clone record | `repo://src/CanDoItAll.Economy.Components/Services/BusinessObjectCommerceDispatcher.cs` | `bundle://proof/SB04/transcripts/playwright-alice-dispatch-refresh.txt` | `bundle://proof/SB04/transcripts/api-dispatch-state.json` | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` |
| Original dispatch transfer state | `repo://src/CanDoItAll.Economy.Components/Services/BusinessObjectCommerceDispatcher.cs` | `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt` | `bundle://proof/SB05/transcripts/api-original-sale-state.json` | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` |
| Linked receipt and purchase records | `repo://src/CanDoItAll.Economy.Components/Services/BusinessObjectCommerceDispatcher.cs` | `bundle://proof/SB04/browser/seller-receipt-detail.png` | `bundle://proof/SB05/transcripts/playwright-alice-original-dashboard.txt` | `bundle://proof/SB04/transcripts/anti-stub-audit.txt` |

## Visual Review Answers

- Does seller refresh show dispatched payment/receipt rows? `Yes`.
- Does the payment detail expose delivered BO and dispatch tx? `Yes`.
- Does the receipt detail open from the dashboard? `Yes`.
- Is there any raw notification text rendered into workspace content? `No`.
