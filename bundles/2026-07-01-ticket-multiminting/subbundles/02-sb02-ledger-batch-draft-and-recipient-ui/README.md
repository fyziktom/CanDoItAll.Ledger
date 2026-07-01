# SB02 Ledger Batch Draft And Recipient UI

## Status

- Completed

## Objective

Implement ticket multiminting in the AccountApp wizard and adapter host while preserving single-mint behavior and existing signing paths.

## Covered Inputs

- R2 through R6.
- SB01 architecture.

## Prerequisites

- SB01 complete.
- Existing tests pass before edits or failures are documented.

## Exact Source References

- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Components\BusinessObjects\BusinessObjectCreationWizard.razor`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Components\VenftSsrEconomyApp.razor`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Components\Account\EconomyAccountWorkspaceAdapterHost.razor`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Models\BusinessObjectTransactionDraft.cs`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Services\EconomyNodeWorkspaceSession.cs`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Services\EconomyDevelopmentWorkspaceSession.cs`

## Deliverables

- Batch request/result model.
- Wizard multimint UI for tickets.
- Recipient resolution for own account, subaccount, address book, and manual list.
- Sequential batch submission with one refresh and summary notification.
- SSR/create ownership alignment.
- Focused tests.

## Dependency Impact

- Enables real AccountApp proof in SB03.
- Touches shared components used by both WASM and SSR.

## Validation Depth

- Component tests must inspect staged batch drafts.
- Adapter host tests must prove single refresh/summary behavior for batch submission.
- Existing ticket event picker tests remain green.

## Implementation Steps

1. Add batch request/result models.
2. Add wizard state and UI.
3. Build `BusinessObjectTransactionDraft` collection with unique ids and ticket numbers.
4. Wire events through `VenftSsrEconomyApp` to adapter host.
5. Submit sequentially and summarize.
6. Align SSR create owner handling.
7. Add focused tests.

## Do Not Do

- Do not add a new cryptography or broadcast path.
- Do not make multimint available for non-mintable system BOs.
- Do not hide single-ticket behavior behind batch UI.

## Acceptance Checklist

- Single mint still stages one draft.
- Batch mint stages one draft per recipient/count.
- Draft owner address matches resolved recipient.
- BO ids are unique.
- Ticket payload has unique ticket numbers.
- SSR and WASM create owner semantics align.

## Proof Required

- Focused .NET test command transcript: `bundle://proof/SB02/transcripts/dotnet-test-passing.txt`.
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.

## Browser Validation Logging

- Deferred to SB03.

## Progression Gate

- SB03 may start only after tests pass.

## Suggested Agent Prompt

Implement the batch draft and UI changes with focused tests, preserving all existing AccountApp flows.
