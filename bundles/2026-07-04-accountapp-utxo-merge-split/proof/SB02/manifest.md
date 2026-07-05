# SB02 Proof Manifest

## Result

- Status: `Pass`
- Scope: reusable Send, UTXO Merge, and UTXO Split form components plus large desktop dialog layout support.

## Source Proof

- Source assertions: `bundle://proof/SB02/transcripts/02-source-assertions-final.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/03-anti-stub-audit-final.txt`
- Changed source/test hashes: `bundle://proof/changed-source-test-file-hashes-final.txt`
- SHA-256 sample: `47C8A6E5F05B3DE436D3A4FAF88B6C62AD09170B66F6E03AF62DFE96A493B792` for `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionSendForm.razor`.
- Failing-first proof: N/A process/non-production exemption; SB02 is a reusable UI refactor validated by component tests after SB01 failing-first domain semantics.

## Test Proof

- `dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --filter "FullyQualifiedName~TransactionUiComponentTests"`
- Final transcript: `bundle://proof/SB02/transcripts/05-passing-reusable-form-ui-tests-after-outpoint-polish.txt`
- Result: 6 passed, 0 failed.

## Browser/Layout Proof

- Send form screenshot: `bundle://proof/SB03/screenshots/00-send-dialog.png`
- Split form screenshot: `bundle://proof/SB03/screenshots/01-split-dialog.png`
- Merge form screenshot: `bundle://proof/SB03/screenshots/03-merge-dialog.png`
- These screenshots were captured after the final Docker rebuild at 1920x1080 and show the reusable payment fields, UTXO selector, selected totals, equal split output controls, output plan, and merge output summary.

## Source Coverage

- Reusable payment form: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionSendForm.razor`
- Reusable merge form: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionUtxoMergeForm.razor`
- Reusable split form: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionUtxoSplitForm.razor`
- Shared address input: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionAddressInput.razor`
- Shared UTXO selector: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/UtxoSelectionList.razor`
- Dialog wiring: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor`
- Details-card layout guard: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TxInputCard.razor`, `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TxOutputCard.razor`, and `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`
