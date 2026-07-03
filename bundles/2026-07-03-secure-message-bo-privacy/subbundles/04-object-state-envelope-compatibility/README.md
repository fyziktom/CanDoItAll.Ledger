# SB04 Object State Envelope Compatibility

## Status

- `Completed`

## Objective

- Make BO envelope readers and raw display tolerant of parseable object state so new and old payload shapes remain usable.

## Covered Inputs

- User concern that `\u0022` escaped nested JSON may be real persisted data and bloats raw tx.

## Prerequisites

- SB03 defines the secure-message public state shape.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectPayload.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/InProcessLedgerGateway.cs`
- `repo://src/CanDoItAll.Economy.Node/EconomyLedgerReadModelService.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`

## Deliverables

- Shared/tolerant envelope reader handles `stateJson` string, `stateJson` object, and `state` object.
- Raw tx view expands parseable nested JSON.
- Existing projection contract continues returning compact state JSON strings.

## Dependency Impact

- BO history, gallery, rule executor, and node projections keep their string-state contract.

## Validation Depth

- Unit tests for legacy and object-valued envelope parsing.
- Component test for raw tx structured view.

## Implementation Steps

1. Add compatibility parsing helper.
2. Update in-memory and node read-model envelope parsing.
3. Update secure-message transaction parsing to use tolerant helper.
4. Update raw tx display expansion.

## Do Not Do

- Do not perform a destructive ledger migration.

## Acceptance Checklist

- [ ] Legacy string `stateJson` parses.
- [ ] Object-valued `stateJson` or `state` parses into existing string state model.
- [ ] Raw tx display expands nested JSON.

## Proof Required

- Targeted tests for both read paths and raw display.

## Browser Validation Logging

- SB06 screenshot covers rendered raw tx.

## Progression Gate

- SB05 starts after compatibility tests pass.

## Suggested Agent Prompt

- Implement tolerant BO envelope parsing and raw nested-state expansion without breaking existing projections.
