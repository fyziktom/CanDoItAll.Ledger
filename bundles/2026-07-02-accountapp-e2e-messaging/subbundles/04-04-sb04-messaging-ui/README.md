# SB04 - Messaging UI

## Status

- Completed

## Objective

Replace the technical message-security workbench as the main messaging path with a large-screen mailbox/chat UI that hides BO mechanics, uses bookmark nicknames, and decrypts only selected messages and requested attachments.

## Covered Inputs

- N007
- N008
- N009
- N010
- N011

## Prerequisites

- SB03 closure gate passed.
- UI proposals stored under `bundle://inputs/ui-proposals/`.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/MessageSecurityPanel.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/AddressDirectoryDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://tests/CanDoItAll.Ledger.Tests`

## Deliverables

- Messages entry in AccountApp UI.
- Large-screen folder/list/detail layout.
- Compose flow with address-directory recipient selection and public-key readiness.
- Selected message decrypt-on-open behavior.
- Attachment chips with explicit decrypt/open controls.
- Security/transaction details available in secondary panel/tabs.

## Dependency Impact

- SB05 depends on this UI for Docker browser validation.

## Validation Depth

- Component tests, Playwright large-screen proof, screenshot visual review, source assertions, anti-stub audit.

## Implementation Steps

1. Add message mailbox view/component and route/entry point.
2. Add compose dialog/panel using address directory/bookmark display names.
3. Wire secure send/decrypt/attachment request APIs from SB03.
4. Render list rows without decrypted previews.
5. Add component tests for list/decrypt behavior.
6. Run Playwright large-screen visual proof and inspect screenshots for space use and overflow.

## Scope Exceptions

- Small/medium responsive tuning is out of scope unless large-screen changes break basic rendering.

## Do Not Do

- Do not expose raw BO terminology in primary message list/composer.
- Do not show decrypted body previews in unopened rows.
- Do not use generated proposal images as runtime proof.

## Acceptance Checklist

- [ ] Messages UI reads as email/chat, not BO editor.
- [ ] Bookmarked nicknames appear when present.
- [ ] Compose blocks secure send when recipient public key unavailable.
- [ ] Selected message decrypts on open.
- [ ] Attachments decrypt only on request.
- [ ] Large-screen screenshots show no overflow/overlap and good use of width.

## Proof Required

- `proof/SB04/transcripts/component-tests.txt`
- `proof/SB04/transcripts/playwright-messaging-ui.txt`
- `proof/SB04/screenshots/messages-list-large.png`
- `proof/SB04/screenshots/message-compose-large.png`
- `proof/SB04/screenshots/message-open-decrypted-large.png`
- `proof/SB04/screenshots/attachment-decrypt-request-large.png`
- `proof/SB04/transcripts/source-assertions.txt`
- `proof/SB04/transcripts/anti-stub-audit.txt`
- `proof/SB04/changed-file-hashes.json`
- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`

## Browser Validation Logging

- Record AccountApp route, viewport, actions, assertions, screenshots, visual review notes, and result in `reviews/01-execution-report.md`.

## Progression Gate

- SB05 may start only after large-screen browser proof validates mailbox list, compose, selected decrypt, attachment decrypt request, and no list bulk decryption.

## Suggested Agent Prompt

Execute SB04. Build the large-screen messaging UI over the proven secure domain, validate with Playwright/screenshots, and keep BO details secondary.
