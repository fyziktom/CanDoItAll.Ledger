# Image Generation Prompt Record

## Common Prompt Applied To Every Asset

```text
Use case: ui-mockup
Asset type: desktop web app transaction modal or wizard-step proposal
Scene/backdrop: blurred calm account workspace behind one centered wide modal
Style/medium: realistic shippable product UI, calm dense operational UI, Linear-style restraint, not concept art
Composition/framing: one 1600x900 desktop screen; one focused flow/step; persistent review when appropriate
Color palette: white, soft gray, slate navy primary, restrained cyan and purple accents; green for valid state; amber/red only for warnings
Constraints: one screen only; no collage; no logos; no watermark; no decorative gradients; practical accessible layout; readable text
```

## Per-Asset Final Prompt Delta

| Asset | Primary request and exact UI content |
|---|---|
| `00-chooser.png` | Send Transaction operation chooser with families `Money`, `Business objects`, `UTXO management`; rows for Payment, Token transfer, Create/Transfer/Update/Close/Clone/Merge objects, Split payment, Merge/Split UTXOs, and delegated Create rule token. |
| `01-payment.png` | Payment with `Available 1,250 MAIN`, Receiver, Amount, MAX, Memo, Ready to submit, Review, You send, Remaining, Back, Submit payment. |
| `02-token.png` | Token transfer with USD-CREDIT/SUPPLIER-SLOT, Source balance, Unique balance, Token amount, Receiver, Required MAIN amount, review, unique-balance warning. |
| `03-utxo-merge.png` | Merge UTXOs with checkable outpoints/age/amount, 3 selected, Select all/Clear, selected total, output address, `3 inputs -> 1 output`, ready state. |
| `04-utxo-split.png` | Split UTXOs with two selected inputs totaling 100 MAIN, Equal/Custom outputs, count 4, amount 25, My address, Selected/Planned/Remainder review. |
| `05-bo-create.png` | Create object with Product/Invoice/Post/Generic choice, generated id, schema, current owner, carrier asset, structured Name/SKU/Description, Advanced JSON, preview. |
| `06-bo-transfer.png` | Transfer object with searchable owned-object cards, selected object/current state, receiver/address book, advanced changes, From/To/Object/Version review. |
| `07-bo-update.png` | Update object with owned-object selection, Current v3 versus Next v4 structured editor, change markers, JSON valid, 2-change review. |
| `08-bo-close.png` | Close object with selected Invoice v4, final state, closing note, permanent consequence, `I understand this cannot be undone`, disabled submit. |
| `09-clone-step-1-source.png` | Clone step 1/3: Source/Select/Configure & review; source choices My objects, From address, Direct object id; preload explanation and availability. |
| `10-clone-step-2-select.png` | Clone step 2/3: search/type filters, compact BO thumbnail cards with type/version/id/owner, selected source/current-state preview. |
| `11-clone-step-3-review.png` | Clone step 3/3: source summary, generated target id, target owner, preloaded type/schema/state, Advanced JSON, source-to-new-object lineage. |
| `12-merge-objects-step-1-first.png` | Merge step 1/3: first source selection; compact cards; compatibility note that next candidates share type/schema. |
| `13-merge-objects-step-2-second.png` | Merge step 2/3: compatible-only candidates, distinct second selection, duplicate disabled as Already selected, pair review. |
| `14-merge-objects-step-3-review.png` | Merge step 3/3: two sources, generated target id, state field conflict choices Use first/Use second/Custom, no unresolved conflicts, lineage. |
| `15-split-payment-step-1-source.png` | Split payment step 1/3: owned invoice/order selection, selected Invoice v4, 1,000 MAIN available, lineage note. |
| `16-split-payment-step-2-allocate.png` | Split payment step 2/3: two recipient rows for 600/400 MAIN, address book, Add recipient, Available/Allocated/Remainder, balanced state. |
| `17-split-payment-step-3-review.png` | Split payment step 3/3: Invoice v4 source, two outputs, 600/400 MAIN, lineage, total 1,000, ready state. |
| `18-rule-token-delegated.png` | Dedicated Create rule token with specialized-flow notice, permission source/namespace/uses, token id, rule object, expression preview, amount, authority/result review, Back to transactions. |

