# Normalized Requirements

## Requirements

| Requirement | Source notes | Statement | Acceptance evidence |
| --- | --- | --- | --- |
| R001 | N001 | The BO details Actions tab must not include a `Verify ownership` scanner action or open `BusinessObjectVerificationDialogContent`. | Component test and source assertion prove the action is absent from BO details. |
| R002 | N001 | The Home wallet-owner tool surface must still expose ownership verification scanning through `MessageSecurityPanel`. | Component/browser proof opens Home Security and verifies the ownership verifier content is available. |
| R003 | N001 | Owner-facing BO ownership QR generation must remain available so the owner can present a signed/time-bound ownership payload. | Component/browser proof shows BO details can still show ownership QR where owner presents the payload. |
| R004 | N002 | BO details Actions must include an `Edit` action for each loaded BO. | Component/browser proof shows an edit action in BO details. |
| R005 | N002 | The edit action must open a dialog that reuses `BusinessObjectCreationWizard` or the same form implementation used for minting, prefilled from the selected BO. | Source assertion and component test prove the edit dialog renders the wizard with selected BO state. |
| R006 | N002 | Staging from edit mode must produce a `BusinessObjectTransactionDraft` with the selected BO object id, selected object type/schema, current owner, updated state JSON, and `BusinessObjectTransactionMode.UpdateObject`. | Component test captures the draft and asserts update-mode semantics and changed state. |
| R007 | N003 | The final closure must run real validation: targeted tests, broader tests/builds, Docker rebuilds, and browser proof. | Command transcripts, Docker transcripts, Playwright screenshots, and execution-report rows. |

## Hard Constraints

- Do not move the scanner into Gallery, BO details, or BO Actions.
- Do not implement a second custom edit form when the minting wizard can be extended safely.
- Do not stage an edit as a new BO create transaction.
- Do not close without Docker rebuild attempts and recorded results.
