# SB02 Opaque Secure Message Identity

## Status

- `Completed`

## Objective

- Stop secure-message BO ids and external refs from encoding subject or display-name text.

## Covered Inputs

- User object id example `bo:secure-message:hello-greetings:...`.

## Prerequisites

- SB01 privacy tests exist.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/BusinessObjectCreateIdFactory.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`

## Deliverables

- Opaque secure-message id factory path.
- Adapter uses opaque id for secure messages.

## Dependency Impact

- Raw tx and BO history show safe object ids for messages.

## Validation Depth

- Integration test inspects transaction vout BO object id and external ref.

## Implementation Steps

1. Add an opaque id factory method or secure-message-specific helper.
2. Replace subject-based id creation in `SendSecureMessageAsync`.
3. Assert id starts with `bo:secure-message:` and has no normalized subject segment.

## Do Not Do

- Do not change all existing public BO id behavior in this subbundle.

## Acceptance Checklist

- [ ] Subject is absent from message object id.
- [ ] Subject is absent from message external ref.
- [ ] Existing public BO id tests still pass.

## Proof Required

- Targeted `dotnet test` output.

## Browser Validation Logging

- Covered by SB06 raw tx screenshot.

## Progression Gate

- SB03 can proceed independently, but SB05 raw tx closure requires SB02 complete.

## Suggested Agent Prompt

- Implement opaque secure-message BO ids with narrow scope and verify external ref privacy.
