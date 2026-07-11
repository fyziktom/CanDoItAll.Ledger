# SB05 AccountApp WASM Visual Validation Manifest

## Runtime

- Rebuilt `candoitall-ledger-accountapp:dev` and recreated `accountapp-alice`, `accountapp-bob`, and `accountapp-lab`.
- Final image/container digest: `sha256:831deffdb1646cb772d81124bf693b5c06892e0fb12f8b1fbedc6f4be66bc906` for all three AccountApps.
- Final desktop inspection used AccountApp WASM at `http://localhost:5083/` with a browser-local encrypted test vault populated from the Alice demo account.
- Populated state during validation: `100005` main, `1` token type, `3` business objects, `6` UTXOs, and `13` transactions.
- Node health and readiness both returned HTTP `200`; readiness reported ledger sequence `20`, `valid_semantic_payload`, and a `valid` projection.

## Visual Findings and Repairs

| Finding | Repair | Revalidation |
|---|---|---|
| Transaction cards and the selected workflow competed vertically, forcing the user to scroll back to the chosen operation. | The transaction selector is now explicit Step 1. Selecting a mode replaces it with that mode's guided workflow and a compact mode header. | Rebuilt WASM selector and every inspected workflow occupied a single 1792 x 846 desktop dialog. |
| Wizard content used only a narrow left strip of the available dialog. | Added a reusable desktop transaction workbench with a flexible editor and a compact context/action rail. | Create, payment, clone, UTXO split, and review screens used the full working width. |
| Long BO and UTXO collections expanded inside the parent transaction dialog. | BO and UTXO selection now opens dedicated searchable picker dialogs with internal collection scrolling and sticky actions. | Populated BO picker rendered six gallery columns; UTXO picker exposed searchable, accessible selections without growing the parent. |
| Valid details could be submitted without visiting Review. | Guided flows now publish step readiness to the shell; Submit remains disabled until the Review step. | Split and create flows showed disabled Submit on Details and enabled Submit only on Review. |
| Switching modes could preserve hidden asset, payment, BO source, or UTXO allocation state. | Mode changes rebuild from the current account draft seed, preserve intentional per-mode defaults, and clear edits owned by the previous mode. Genuine account/draft replacement resets the workflow and becomes the new seed. | Regression tests cover child-update echoes, account replacement, token defaults, receiver/amount reset, BO references, and UTXO plans. |
| Split outputs stacked vertically and compact button typography exposed Material Icon ligature words. | Split outputs use a two-column desktop collection; transaction icon ligatures explicitly preserve case. | Final screenshot shows two 660 px output cards on one row and rendered glyphs instead of `CHECKLIST`/`ADD` text. |
| Token Review omitted optional attached main currency; the BO merge picker left extra compatible cards enabled at its selection limit. | Token Review and the context rail disclose attached main amount. Picker cards now disable at capacity with an ARIA-linked explanation, and picker/tag controls use coherent toggle-button semantics. | Focused review and picker component suites pass. |

## Flow Coverage

- Selector families: Payments, Tokens, UTXOs, and Business objects.
- Payment details and Review gating.
- Token transfer rendering plus delegated rule-token creator handoff.
- Rule-token creator: Authority, Definition, Review rail; measured 1312 px wide with no body overflow.
- UTXO Merge compatibility behavior and searchable picker.
- UTXO Split picker, two-input selection, equal-output plan, compact output cards, Review gating.
- BO Create template, details, and review.
- BO Transfer, Update, Close, Clone, Merge, and Split Payment render paths.
- Clone Mine / Another address / Direct reference source strategies and preloaded source details.
- Populated BO picker search, type and tag filters, thumbnails, compatibility disabling, and sticky selection action.

## Automated Verification

```text
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Transaction|FullyQualifiedName~BusinessObjectSelectionGridTests"
Passed: 115, Failed: 0

dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~TransactionComposerLayoutContractTests"
Passed: 2, Failed: 0
```

The populated in-app browser console inspection returned no warnings or errors. After the last edge-case repairs, the exact-source image rebuilt successfully and all HTTP/readiness checks passed; a second delegated visual attachment could not obtain an in-app browser backend, so those final behavior-only repairs are closed by focused rendered-component and layout tests. Detailed measurements and screenshot inventory are in `desktop-workbench-followup.md`.
