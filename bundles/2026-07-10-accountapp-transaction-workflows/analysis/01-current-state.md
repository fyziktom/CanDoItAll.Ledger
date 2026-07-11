# Current State

## Composer Inventory

`TransactionComposerShell.razor` is 441 lines and owns mode navigation, nine form variants, draft mutation helpers, previews, address-picker plumbing, parsing, and submit/assemble/refresh actions.

| Mode | Visible now | Adapter path | Current interaction | Main gap |
|---|---:|---:|---|---|
| Create | Yes | Yes | Raw id/type/schema/address/amount/JSON | No template/type guidance, preview, JSON validation, or field readiness |
| TransferObject | Yes | Yes | Raw object id + receiver + JSON | Does not list owned BOs or preload selected state |
| UpdateObject | Yes | Yes | Raw object id + JSON | Does not list owned BOs or show existing state/version |
| CloseObject | Yes | Yes | Raw object id + JSON | No object selection, final-state comparison, or destructive confirmation |
| CloneObject | Yes | Yes | Raw source/target ids + JSON | Missing origin choice, lookup, card selection, preload, and review wizard |
| MergeObjects | No | Yes | No composer form | Missing two-source selection, compatibility validation, target preview |
| SplitBusinessObjectPayment | No | Yes | No composer form | Missing BO selection, recipient rows, total validation, review |
| MainPayment | Yes | Yes | Receiver + amount + memo | No balance context, self/recipient hint, positive/balance validation |
| TokenTransfer | Yes | Yes | Token selector + amount + receiver + main amount | Empty token state is poor; no source/unique distinction or balance validation |
| CreateRuleToken | No | Separate dedicated adapter flow | Dedicated token creation dialog | Keep separate unless shared entry can delegate without duplicating domain logic |
| UtxoMerge | Yes | Yes | Eligible UTXO check list + output address | Strong seed; submit is still enabled with no selection |
| UtxoSplit | Yes | Yes | Eligible UTXO list + equal/specific outputs | Strong seed; invalid/empty plans still allow submit |

## Browser Baseline

- AccountApp Docker services are healthy on `localhost:5081-5083`; the node proxy responds at `/api/v1/health`.
- Opening through `127.0.0.1` causes an origin mismatch because generated app settings use `http://localhost:5081/`; browser proof must use `http://localhost:5081/`.
- A local test vault can be created and the Send Transaction dialog opens.
- All nine visible mode tabs render, but none exposes an error/validation message at initial invalid state.
- Screenshots: `bundle://evidence/baseline/send-*.png`.

## Existing Reusable Seeds

- `TransactionAddressInput`: address text, directory, and QR scan.
- `AvailableTokenSelector`: token option selection.
- `UtxoSelectionList`: eligible plain UTXO filtering and selection.
- `BusinessObjectGalleryCard` and `BusinessObjectRendererHost`: BO thumbnail/detail rendering.
- `ActionReviewPanel` and `ObjectActionReviewPanel`: review facts.
- BaseLib: `Steps`, `DialogScaffold`, `FormSection`, `FormField`, `CardGrid`, `SelectionListItem`, `EmptyState`, `Alert`.

## Missing Test Coverage

- No test asserts every declared mode is either composer-visible or explicitly delegated.
- No shared validation contract exists for submit readiness.
- No component tests cover BO lookup-origin choice, BO card selection, preload, merge compatibility, or split-payment totals.
- Existing browser proof covers Send/Merge/Split only, not all visible transaction flows.

