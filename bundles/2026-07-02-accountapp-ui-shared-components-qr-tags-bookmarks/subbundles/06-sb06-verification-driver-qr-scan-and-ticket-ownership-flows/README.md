# SB06 Verification Driver QR Scan And Ticket Ownership Flows

## Status

- `Completed`

## Objective

Use shared QR generation/scanning to implement reusable BO ownership and ticket verification flows through a composable verification driver/builder.

## Success Criteria

- Address/subaddress, transaction hash, and BO ownership hash surfaces open real QR dialogs.
- QR scan/manual entry can feed address selection and verification flows.
- Verification builder composes reusable steps for BO ownership, ticket ownership, and ticket Used-state validation.
- Ticket verification can check and optionally update Used state through explicit confirmation.
- Positive and negative tests prove verification semantics.

## Covered Inputs

- QR generation for addresses/subaddresses, tx hashes, signed BO ownership hashes, and tickets.
- Scan address button in create transaction, send BO, and related dialogs.
- Verification process for ownership and ticket verification.
- Driver/builder verification architecture request.
- Ticket Used-flag additional process request.

## Prerequisites

- SB01 shared QR generation/scanner components.
- SB04 address manager/select model where receiver/address selection is involved.
- Existing ticket verification service behavior.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTicketVerification.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/AddressBookPicker.razor`
- `bundle://evidence/qr-wasm-test/qrcode-wasm-cli-render.png`

## Deliverables

- Verification driver and builder APIs.
- Verification step result model and UI dialog.
- QR generation wired into address, tx hash, and ownership hash surfaces.
- QR scan/manual entry wired into address and verification flows.
- Ticket-specific Used-state verification/update flow.
- Tests for positive ownership, invalid signature/hash, wrong BO/ticket, already-used ticket, and Used-state update path.
- Browser proof for QR and verification dialogs.

## Dependency Impact

- SB07 final validation depends on these flows for QR and verification closure.
- Address selection consumes SB04 when scan result maps to a receiver address.

## Validation Depth

- `Process-critical closure`
- Requires semantic tests, negative tests, browser QR proof, and verification dialog screenshots.

## Implementation Steps

1. Define verification flow builder, step contracts, and result model.
2. Wrap existing `BusinessObjectTicketVerification` behavior as driver steps.
3. Add generic BO ownership verification steps.
4. Add ticket Used-state check and optional mark-used step with confirmation.
5. Replace QR placeholders/mocks with shared QR dialogs.
6. Add QR buttons for tx hash and address/subaddress surfaces.
7. Add QR scan/manual entry buttons for receiver and verification inputs.
8. Add tests for positive and negative verification paths.
9. Capture browser proof of QR and verification flows.

## Scope Exceptions

- Real camera automation may use mocked scan payloads if Playwright cannot access a physical camera; manual camera evidence can be logged in SB07.
- Network-level ticket settlement beyond existing adapter capabilities is out of scope.

## Do Not Do

- Do not leave QR mock placeholders for ticket or BO ownership hashes.
- Do not make ticket verification a one-off dialog that cannot verify other BO types.
- Do not mark a ticket as Used without explicit user confirmation and proof of target ticket identity.

## Acceptance Checklist

- Tx hash QR button opens a real QR dialog.
- Address/subaddress QR button opens a real QR dialog.
- Ticket/BO ownership QR displays signed ownership hash payload.
- Scan/manual payload can populate receiver or verifier input.
- Verification result shows step-by-step success/failure.
- Tests cover invalid hash/signature and already-used ticket.

## Proof Required

- Tests for verification driver positive and negative cases.
- Tests for ticket Used-state logic.
- Browser screenshot of tx hash QR dialog.
- Browser screenshot of ownership verification dialog.
- Browser screenshot or logged proof of scan/manual entry flow.
- Execution report update for SB06.

## Browser Validation Logging

- Route: BO details, transaction details, send/create transaction, and verification dialogs.
- Viewport: 1920x1080 or maximized large-screen browser.
- Actions: open tx QR, open BO ownership QR, scan/paste verification payload, run verification, confirm Used-state flow.
- Screenshots: `bundle://evidence/SB06/tx-hash-qr-large.png`, `bundle://evidence/SB06/ownership-verification-large.png`, `bundle://evidence/SB06/ticket-used-confirm-large.png`.
- Review questions: Are QR dialogs real not mock; are verification steps understandable; is Used-state confirmation explicit; is layout unclipped?

## Progression Gate

- SB07 cannot close until QR placeholder replacement and verification-driver semantic tests pass.

## Suggested Agent Prompt

```text
Implement SB06 only. Replace QR placeholders with shared QR dialogs, add scanner/manual entry integration, build a reusable verification driver, prove positive and negative ticket/BO cases with tests, capture screenshots, and update the execution report.
```

