# UI Proposal Manifest

All assets were generated with the built-in image generation tool in `ui-mockup` mode and copied into this workspace. They are planning references, not acceptance proof or pixel-exact specifications.

## Shared Direction Extracted

- Replace the equal-weight pill strip with a family/operation chooser.
- Use a persistent flow header/stepper, focused work area, review rail, and stable footer actions.
- Use compact selectable rows/cards only when the card is the interaction.
- Preload owned BOs, current state, versions, balances, token source/unique information, and UTXOs.
- Keep raw JSON under `Advanced JSON` after structured/preloaded fields.
- Show immediate validation and disable final action until ready.
- Collapse multi-column desktop layouts into a vertical sequence on narrow screens.

## Coverage

| Asset | Flow/step | Implementation decision |
|---|---|---|
| `00-chooser.png` | Operation chooser | Family rail + compact operation rows; Rule Token explicitly delegates |
| `01-payment.png` | Payment | Balance-aware form + review |
| `02-token.png` | Token Transfer | Token balance/source choice + review |
| `03-utxo-merge.png` | UTXO Merge | Checkable UTXOs + 3-to-1 review |
| `04-utxo-split.png` | UTXO Split | Equal/custom modes + balanced plan |
| `05-bo-create.png` | Create BO | Type guidance + structured fields + preview |
| `06-bo-transfer.png` | Transfer BO | Owned BO selection + receiver + current state |
| `07-bo-update.png` | Update BO | Preloaded current/next state + change review |
| `08-bo-close.png` | Close BO | Consequence confirmation + disabled submit |
| `09-clone-step-1-source.png` | Clone 1/3 | Mine/address/direct-id strategy |
| `10-clone-step-2-select.png` | Clone 2/3 | Compact source cards + selected preview |
| `11-clone-step-3-review.png` | Clone 3/3 | Preloaded target config + lineage review |
| `12-merge-objects-step-1-first.png` | BO Merge 1/3 | First source card selection |
| `13-merge-objects-step-2-second.png` | BO Merge 2/3 | Compatible, distinct second source only |
| `14-merge-objects-step-3-review.png` | BO Merge 3/3 | Target id, state conflicts, lineage |
| `15-split-payment-step-1-source.png` | BO Split Payment 1/3 | Source BO and allocatable context |
| `16-split-payment-step-2-allocate.png` | BO Split Payment 2/3 | Recipient rows and balanced totals |
| `17-split-payment-step-3-review.png` | BO Split Payment 3/3 | Outputs, lineage, final totals |
| `18-rule-token-delegated.png` | Delegated Rule Token | Dedicated specialized flow with back link |

## Inspection Result

- Accepted as planning references: all 19 depict the requested single flow/step, readable hierarchy, data preload, validation, and review.
- Generated app chrome and exact icons are illustrative only; implementation must use existing AccountApp/BaseLib theme and components.
- The implementation should preserve the current navy/purple/cyan language but avoid copying generated layout details that conflict with existing BaseLib behavior.

## Prompt Record

- Full reconstructable final prompts are in `bundle://inputs/ui-proposals/prompts.md`.

