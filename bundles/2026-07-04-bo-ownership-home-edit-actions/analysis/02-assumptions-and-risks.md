# Assumptions And Risks

## Assumptions

- "Home page" means the full AccountApp Home view rendered by `EconomyAccountWorkspace`, with the existing Message Security dialog as the wallet-owner tool surface.
- "BO Actions tab" means the Actions tab in `BusinessObjectLedgerDetailsDialogContent`.
- "Same form as minting" means reuse `BusinessObjectCreationWizard` and its field/data item inputs, with edit/update mode prefilled from the selected BO.
- "Stage" means invoke the existing `BusinessObjectTransactionDraft` submit path with `BusinessObjectTransactionMode.UpdateObject`.

## Critical Path Risks

- SB01 is a critical UI boundary because leaving any scanner action in BO details violates the first raw note and weakens final browser proof.
- SB02 is a critical behavior foundation because a create-mode draft or a duplicated edit form would satisfy only the button label, not the required BO UTXO update semantics.
- SB03 depends on SB01 and SB02; if browser proof shows ownership verification outside Home or edit staging not using update mode, reopen the owning subbundle.

## Validation Risks

- Browser proof depends on a runnable app target and Playwright availability.
- Docker proof depends on local Docker being available and able to build the AccountApp/Node images.
- bUnit proof can verify component structure and staged draft semantics, but cannot alone prove rendered browser workflow or Docker app viability.

## Reopen Triggers

- Reopen SB01 if any BO details action still opens the ownership verification scanner or if Home no longer opens the scanner flow.
- Reopen SB02 if the edit dialog does not prefill the selected BO, creates a new object id, stages `Create`, or fails to preserve the selected BO object id for update.
- Reopen SB03 if Docker build or Playwright proof fails for a shipped surface and the failure is caused by SB01 or SB02 behavior.
