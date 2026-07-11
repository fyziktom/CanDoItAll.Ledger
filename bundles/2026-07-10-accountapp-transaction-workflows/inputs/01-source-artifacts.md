# Source Artifacts

| Artifact | Role | Durable path |
|---|---|---|
| User request | Raw scope and absolute language | `bundle://inputs/00-original-request.md` |
| Existing Send Transaction screenshots | Baseline visual evidence for every visible mode | `bundle://evidence/baseline/send-*.png` |
| Prior Merge/Split bundle | Existing explicit UTXO domain behavior and proof | `repo://bundles/2026-07-04-accountapp-utxo-merge-split/README.md` |
| Current composer | Current nine-tab, 441-line coordinator | `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor` |
| Draft contract | Twelve declared transaction modes | `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTransactionDraft.cs` |
| WASM adapter | Eleven implemented composer build paths; `CreateRuleToken` is separate | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` |
| AccountApp host | Dialog data and submission wiring | `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor` |

## Tool Evidence

- BaseLib component search succeeded for `Steps`, `TimelineStepper`, `DialogScaffold`, `FormSection`, `FormField`, `ActionReviewPanel`, `CardGrid`, `SelectionListItem`, and `EmptyState`.
- Follow-up component detail queries lost the MCP transport; exact source and local usages must be inspected before implementation.
- CodeAnalytics snapshot `snap-20260710162123-996d4827-failed` is not usable: the MCP process resolved `C:\\repositories\\CanDoItAll\\global.json`, requested SDK `10.0.200`, and loaded zero projects. Architecture proof must record this validation gap and use direct project/source inspection plus build/tests.

