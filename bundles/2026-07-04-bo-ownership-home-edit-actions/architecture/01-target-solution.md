# Target Solution

## UI Boundary

- `EconomyAccountWorkspace` remains the Home surface and opens `MessageSecurityPanel` for wallet-owner security tools.
- `MessageSecurityPanel` remains the scanner/verifier for BO ownership payloads.
- `BusinessObjectLedgerDetailsDialogContent` keeps owner presentation actions such as ownership QR and ticket signing, but no longer contains a scanner/verifier action.

## Edit/Update Flow

- Extend `BusinessObjectCreationWizardModel` with optional edit source data instead of introducing a second form model.
- Extend `BusinessObjectCreationWizard` with an edit mode that preloads object id, type, schema, state-derived fields, tags, text, and data items from the selected BO.
- In edit mode, the wizard stages `BusinessObjectTransactionMode.UpdateObject` for the original BO id and owner address.
- `BusinessObjectLedgerDetailsDialogContent` opens an edit dialog from its Actions tab and passes the staged update draft to the host.
- `BusinessObjectLedgerGallery`, `EconomyAccountWorkspace`, `VenftSsrEconomyApp`, and `EconomyAccountWorkspaceAdapterHost` pass the edit callback through to the existing adapter submit path.

## Validation Shape

- bUnit tests cover the UI boundary and update draft semantics.
- Docker builds cover the app targets that the user asked to rebuild.
- Playwright browser proof covers rendered Home security ownership verification and BO details edit dialog behavior.
