# QA Prompt

Validate multiminting with a small real batch.

Checks:

- Bundle validator passes for prepared and closure stages.
- Focused .NET tests pass.
- AccountApp can create or select an event, enable ticket multimint, choose a real recipient target, mint 5 tickets, and show a single batch success notification.
- Ledger/API state shows 5 accepted ticket BOs with unique object ids and the intended owner address.
- Gallery/order behavior is not regressed.

Do not mint 100 tickets for proof.
