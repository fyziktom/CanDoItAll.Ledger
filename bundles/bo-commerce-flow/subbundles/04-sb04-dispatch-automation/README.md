# SB04 Dispatch Automation

## Status

- `Completed`

## Objective

Process pending commerce payments during seller workspace refresh, dispatch the original BO or deterministic clone, enforce max clone limits, and create linked receipt/purchase/payment status records idempotently.

## Covered Inputs

- `RN-02`, `RN-05`, `RN-06`, `RN-07`
- `REQ-006`, `REQ-007`, `REQ-008`, `REQ-009`

## Prerequisites

- `SB02` contracts pass.
- `SB03` can create pending payment BOs from production UI.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/LedgerContracts.cs`
- `repo://tests/CanDoItAll.Ledger.Tests`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\Neblio\NeblioAccountBase.cs`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\NFTHelpers.cs`

## Deliverables

- Commerce dispatcher service or adapter method invoked after seller workspace refresh.
- Pending payment discovery and target sale validation.
- Original sale transfer path.
- Copy sale clone path with deterministic clone object id and max clone enforcement.
- Payment status update and receipt/purchase record creation.
- Idempotency guards for repeated refresh and partial transaction chains.

## Dependency Impact

- Final browser validation cannot close without this phase.
- Weak idempotency can create duplicate ownership transfers or clones.

## Validation Depth

- `Critical foundation`
- Requires Semantic Adequacy Gate proof and Production Behavior Artifact Matrix.

## Implementation Steps

1. Add dispatcher contract/service in shared layer or business-object layer.
2. Discover pending payment BOs from workspace/node query.
3. Validate sale terms and ownership before dispatch.
4. Implement original transfer and copy clone paths with deterministic ids.
5. Mark payment outcome and create receipt/purchase records.
6. Add duplicate refresh, max clone, already-sold, insufficient sale-state, and happy-path tests.
7. Wire dispatcher into refresh without noisy page-content notifications.

## Scope Exceptions

- Does not implement off-chain escrow or chain reorg handling.

## Do Not Do

- Do not dispatch a payment without validating seller ownership and sale terms.
- Do not create duplicate clones for the same payment id.
- Do not rely on browser UI state as the source of truth.

## Acceptance Checklist

- [x] Seller refresh dispatches original sale once.
- [x] Seller refresh dispatches copy sale until max clone count is reached.
- [x] Duplicate refresh after success does not create additional delivery.
- [x] Payment/receipt states link to target and delivered BO.
- [x] Errors surface as notifications or status facts without rendering raw notification text in workspace content.

## Proof Required

- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/semantic-invariants.md`
- Failing-first duplicate-dispatch/max-clone tests.
- Passing targeted tests and build transcript.
- Browser seller-refresh proof if UI-visible.
- Source assertions for deterministic id and no hard-coded Alice/Bob branches.
- Production Behavior Artifact Matrix for payment status, receipt, purchase, and dispatch event/state.

## Browser Validation Logging

- Route: seller AccountApp `/`.
- Viewport: `1600x900`.
- Actions: Bob payment exists, Alice refresh/auto-refresh runs, observe dispatch status, open receipt/detail.
- Screenshots: `bundle://proof/SB04/browser/seller-dispatch-refresh.png`, `seller-receipt-detail.png`.
- Review questions: no duplicated notification content in page body, dashboard status is clear, detail dialogs link correctly.

## Progression Gate

- `SB05` may start only after unit tests prove idempotent original and copy dispatch and browser smoke proves refresh-triggered dispatch.

## Suggested Agent Prompt

```text
Implement SB04 only. Add seller refresh dispatch using production payments from SB03 and prove idempotent original/copy behavior.
```
