# Current State

## Repo Observations

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` renders the Home account surface and opens `MessageSecurityPanel` through the Security action.
- `repo://src/CanDoItAll.Economy.Components/Components/Account/MessageSecurityPanel.razor` already contains ownership payload paste/scan verification, object lookup, ownership result facts, and details opening for verified BOs.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` currently places `Verify ownership` inside the BO details Actions tab and also shows BO ownership QR generation.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` is the shared minting wizard. It has the needed fields and staging callback, but it always creates a `BusinessObjectTransactionMode.Create` draft.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` already maps `BusinessObjectTransactionMode.UpdateObject` drafts to `BuildUnsignedUpdateBusinessObjectTransactionAsync`.
- `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor` wires Gallery BO details and Home surfaces into the full app shell.
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs` already has bUnit coverage around the full Account workspace shell and staged BO drafts.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs` already exercises `BusinessObjectCreationWizard` behavior and is the closest focused test pattern for wizard draft staging.

## Initial Implementation Signals

- The ownership scanner can remain in `MessageSecurityPanel`; the BO Actions tab should remove its scanner action. The owner-facing QR generator may remain because the user describes the owner opening a QR code with a signed BO transaction and time.
- The edit flow should add an update mode to the existing wizard model/component rather than creating a separate form.
- The BO details dialog needs a new callback for edit/update staging and the Gallery/Home host chain must pass it to the adapter host's `SubmitTransactionAsync`.
