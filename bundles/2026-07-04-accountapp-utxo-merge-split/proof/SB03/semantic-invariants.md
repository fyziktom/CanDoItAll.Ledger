# SB03 Semantic Invariants

## Invariant ID: UTXO-BROWSER-001

- Invariant ID: UTXO-BROWSER-001
- Source raw note: UI tests must cover Send, Merge, and Split after Docker rebuild/restart.
- Expected behavior: final browser proof submits real Send, Split, and Merge transactions through the rebuilt AccountApp stack.
- Disallowed shallow implementation: relying on component tests only, stale containers, seeded fake details, or screenshots without transaction IDs.
- Failing-first test: process/non-production exemption; SB03 is a validation subbundle and records failed proof attempts before the final passing proof in transcripts `05` through `13`.
- Passing test: `bundle://proof/SB03/transcripts/16-playwright-accountapp-send-split-merge-final.txt`.
- Changed source files: `bundle://proof/changed-source-test-file-hashes-final.txt`.
- Production assertions: `bundle://proof/SB03/playwright-results.json` records Send `In 1 / Out 2`, Split `In 1 / Out 6`, and Merge `In 3 / Out 1`.
- Red-team negative case: `bundle://proof/SB03/red-team-verifier.md`.
- Downstream dependency check: raw notes N001-N008 are closed in `bundle://reviews/01-execution-report.md`.

## Invariant ID: UTXO-BROWSER-002

- Invariant ID: UTXO-BROWSER-002
- Source raw note: transaction detail dialogs for merge/split can have plenty of inputs or outputs and must not overflow or overlay.
- Expected behavior: large desktop details cards constrain long outpoints and addresses and use scrollable input/output columns.
- Disallowed shallow implementation: only testing empty/single-row details, hiding rows, or accepting horizontal overflow.
- Failing-first test: process/non-production exemption; SB03 proof found layout/detail issues and reran after CSS/Razor fixes.
- Passing test: final Playwright proof asserts `horizontalOverflowIssues(...)` returns no issues for send dialog and details dialog.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TxInputCard.razor`, `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TxOutputCard.razor`, and `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`.
- Production assertions: final screenshots `bundle://proof/SB03/screenshots/02-split-details-io.png` and `bundle://proof/SB03/screenshots/04-merge-details-io.png`.
- Red-team negative case: Split proof shows 6 output rows; Merge proof shows 3 input rows and one output row.
- Downstream dependency check: AccountApp large-screen desktop workflow remains supported; small/medium screens intentionally remain out of scope.
