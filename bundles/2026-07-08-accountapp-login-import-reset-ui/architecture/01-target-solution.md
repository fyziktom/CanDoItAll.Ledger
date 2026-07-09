# Target Solution

## UI Structure

- Keep implementation in `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` and `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css` unless the markup becomes too large to maintain.
- Replace the four-card locked setup grid with a large-screen account-entry shell using BaseLib `Grid`, `Stack`, `FormSection`, and `Steps`.
- Use `Steps`/`StepsItem` for import:
  - Step 1: choose import source and provide backup JSON or pasted public/private keys.
  - Step 2: provide existing backup password when needed plus new vault password/confirmation.
  - Step 3: review selected mode and submit import.
- Remove the unlocked `account-app__workspace-frame` entirely and leave `EconomyAccountWorkspaceAdapterHost` as the first visible unlocked surface.

## Reset Flow

- Add session state fields for reset acknowledgement and typed confirmation.
- Add `ResetLocalVaultAsync` on `AccountAppSession` using `IAccountVaultStore.DeleteAsync(Options.VaultId)`.
- Reset clears `HasStoredVault`, `UnlockedKey`, `Account`, `Workspace`, backup output, selected transaction, error, passwords, import fields, and confirmation fields.
- UI enables the reset action only when the user checks an acknowledgement and types `RESET LOCAL VAULT`.

## Boundaries

- Do not change ledger account cryptography, address derivation, transaction signing, or node API behavior.
- Do not add VEF/Neblio backup compatibility; the VEF code is a UX reference only.
- Do not spend implementation time on small or medium screen-specific layout tuning.

## Shared Components Chosen

- `Steps` and `StepsItem`: import is an ordered flow where sequence matters.
- `Grid`: large-screen account-entry tracks and field group composition.
- `Stack`: one-dimensional form and action grouping.
- `FormSection`: reusable form grouping with title/description semantics.

Custom CSS remains acceptable for AccountApp-specific panel presentation and reset danger affordance because the existing AccountApp shell is CSS-owned locally and the shared components do not define this exact browser-vault reset surface.
