# Target Solution

## UI Ownership

- Keep event/ticket minting inside `BusinessObjectCreationWizard.razor`.
- Use existing BaseLib layout components already in the wizard: `Stack`, `Grid`, `FormField`, `TextBox`, `TextArea`, `DropDown`, `Button`, and local wizard panels.
- Add event/ticket-specific field rendering helpers rather than creating a second wizard.

## Data Ownership

- Preserve `BusinessObjectFamilyCatalog` field keys so existing typed casting and renderers continue to work.
- Define option lists close to the wizard for now unless the same lists become needed outside the component.
- Populate create-flow defaults from `Workspace.Address` in the production payload path, not only in preview or tests.

## Validation Ownership

- Extend `BusinessObjectTicketMintingComponentTests` for wizard markup and staged payload behavior.
- Extend or preserve detail renderer tests to prove important facts remain visible.
- Run targeted tests before broader test/build proof.

## Component MCP Gap

- Retry `candoitall_components` during execution if available.
- If still unavailable, record the MCP failure in proof and rely on local BaseLib usages from current source.
