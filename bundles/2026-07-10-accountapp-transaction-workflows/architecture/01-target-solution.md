# Target Solution

## Layered Component Set

1. `TransactionFlowCatalog`: closed metadata/disposition map for every `BusinessObjectTransactionMode`.
2. `TransactionFlowShell`: shared focused flow header, optional step navigation, content, validation, review, and actions.
3. `TransactionValidationSummary` plus small pure validation helpers: immediate readiness and accessible errors.
4. `BusinessObjectSourcePicker`: strategy selection (`Mine`, `Address`, `Direct id`) and compact result cards.
5. `BusinessObjectSelectionCard`: compact renderer-derived identity, type, version, owner, and selected state.
6. `TransactionAmountInput`: amount field with available/planned context and validation.
7. Mode-specific forms: thin compositions of shared primitives, not copies of shell/validation/selection markup.

## Flow Families

- Money: Main Payment, Token Transfer.
- Business objects: Create, Transfer, Update, Close, Clone, Merge, Split Payment.
- UTXO management: Merge UTXOs, Split UTXOs.
- Delegated: Create Rule Token -> existing Token Creation dialog.

## State Boundary

- Durable ledger request fields stay in `BusinessObjectTransactionDraft`.
- Transient UI state (current step, search strategy, selected card before commit, expanded advanced fields) stays inside the active mode component.
- Every mode publishes `TransactionFlowValidation` to the shell through a callback.
- The shell owns submit enablement; individual forms own validation rules and review facts.

## UI Composition

- Desktop: operation chooser rail/list + focused form; review remains visible where space permits.
- Tablet/mobile: chooser collapses above content; review follows form; footer actions stay reachable without horizontal overflow.
- Raw JSON/state remains available under an advanced/edit section after a selected BO preloads it.

## Exclusions

- No transaction-domain rewrite.
- No remote BO search API invented in UI code.
- No project split or new project reference.
- No replacement of DialogService/BaseLib theme.

