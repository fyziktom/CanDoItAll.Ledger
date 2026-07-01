# Normalized Requirements

| ID | Requirement | Verification |
| --- | --- | --- |
| R1 | Study VEFramework multiminting and document its reusable UX semantics. | `analysis/01-current-state.md` maps `MultimintForm.razor`. |
| R2 | Add Ledger-native ticket multimint UI to the BO creation wizard. | Component tests and browser screenshot. |
| R3 | Support recipient targets: own account, existing subaccounts, address book entries, and pasted recipient lists. | Component tests cover recipient resolution; browser proof uses a real recipient target. |
| R4 | Generate one valid `BusinessObjectTransactionDraft` per ticket with a unique object id and ticket number suffix. | Component/unit tests inspect staged drafts. |
| R5 | Submit batch drafts through existing adapter signing and broadcast path with one final refresh and summary notification. | Adapter host tests and browser proof. |
| R6 | Ensure SSR/real-node create honors draft `OwnerAddress` the same as WASM. | Unit/source tests and real validation. |
| R7 | Keep validation batch small: 5-10 tickets only. | Proof manifest records batch size. |
| R8 | Preserve event prefill behavior added previously. | Ticket event picker tests remain passing. |
