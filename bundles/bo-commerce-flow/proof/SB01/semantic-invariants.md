# SB01 Semantic Invariants

## Long Body Details Rendering

- Invariant ID: `cda-bo-long-description`
- Source raw note: `RN-01` reported that Alice's `bo:post:new-ledger-post` long description was not visible in details or raw inspection.
- Expected behavior: BO media payload `text` remains serialized and details overview renders it separately from the short description through `data-testid="cda-bo-long-description"`.
- Disallowed shallow implementation: Adding a hard-coded string or object-id-specific branch for `bo:post:new-ledger-post` without reading typed payload `Text`.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-long-description-test.txt` fails before the fix because the details selector is absent.
- Passing test: `bundle://proof/SB01/transcripts/passing-long-description-test.txt` passes component and payload round-trip coverage.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`, `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`, and `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectLongDescriptionComponentTests.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/api-long-description-state.txt` and `bundle://proof/SB01/transcripts/playwright-long-description-browser-proof.txt` prove raw state and browser details visibility.
- Red-team negative case: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` confirms no object id, data hash, TODO, or placeholder shortcut in production scope.
- Downstream dependency check: `bundle://proof/SB01/transcripts/source-assertions.txt` confirms commerce detail dialogs can rely on the same typed payload rendering path.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Serialized media payload `text` state | `bundle://proof/SB01/transcripts/passing-long-description-test.txt` | `bundle://proof/SB01/transcripts/api-long-description-state.txt` | `bundle://proof/SB01/transcripts/playwright-long-description-browser-proof.txt` | `bundle://proof/SB01/transcripts/failing-first-long-description-test.txt` |
| Details dialog long-description record | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` | `bundle://proof/SB01/browser/long-description-details.png` | `bundle://proof/SB01/transcripts/passing-long-description-test.txt` | `bundle://proof/SB01/transcripts/anti-stub-audit.txt` |
| Raw-state inspection behavior | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` | `bundle://proof/SB01/browser/long-description-raw.png` | `bundle://proof/SB01/transcripts/api-long-description-state.txt` | `bundle://proof/SB01/transcripts/source-assertions.txt` |

## Visual Review Answers

- Does the details dialog show the long body in a readable area? `Yes`.
- Does the long body overlap media, metadata, tags, or action tabs at desktop width? `No`.
- Does the raw state remain accessible after opening details? `Yes`.
- Does the fix preserve the existing data item gallery? `Yes`.
