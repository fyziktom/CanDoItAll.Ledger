# Current State

## VEFramework Multimint Findings

`MultimintForm.razor` provides the core behavior:

- Accept receiver addresses from uploaded text or stored JSON.
- Validate and deduplicate addresses.
- Track per-recipient flags: address, use-as-receiver, done, transaction id.
- Persist the in-progress recipient state in browser local storage under `multiminting-{MintingPageName}`.
- Send the batch through account-level `MintMultiNFTLargeAmount(NFT, count, "", addresses)`.
- Listen to progress events and update a process console.
- Export the result state for later review.

Reusable UX semantics:

- A batch is not a new BO kind; it is a repeated mint of the prepared BO template.
- Each recipient should be independently tracked.
- The user needs resume/progress feedback for larger batches.
- Address-list minting is an advanced mode, not the default single-ticket path.

Implementation aspects not to copy:

- Direct `AppData.Account` calls couple UI and account driver.
- Progress is console-like and not integrated with current notification/service patterns.
- Address validation is Neblio-specific.
- Local storage is hard-coded instead of using the AccountApp cache/provider layer.

## Current Ledger AccountApp State

- `BusinessObjectCreationWizard.razor` creates one `BusinessObjectTransactionDraft`.
- Ticket event selection and prefill already exist.
- `EconomyAccountWorkspaceAdapterHost.razor` submits one draft and refreshes the workspace.
- WASM adapter create uses `draft.OwnerAddress`, so it can create BOs owned by another address.
- `EconomyNodeWorkspaceSession.cs` still forces create ownership to the signing account and must be aligned.
- Browser AccountApp subaccount creation is currently an intent-only dialog; persisted child vaults are not available.

## Fit

The right Ledger integration point is batch draft generation plus sequential adapter submission. This keeps all cryptography, UTXO selection, signing, broadcasting, and refresh behavior in the existing production paths.
