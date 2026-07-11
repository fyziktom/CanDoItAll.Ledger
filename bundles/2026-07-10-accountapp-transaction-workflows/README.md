# AccountApp Transaction Workflow UX

## Profile

- `initiative`

## Mission

Turn Send Transaction into a reusable, data-assisted transaction workbench. Every current or adapter-supported transaction mode must be interactive, validated, reviewable, and either directly supported or explicitly delegated. Clone must provide the requested source-strategy wizard and compact BO selection.

## Outcome Contract

- Shared transaction flow, validation, BO selection, amount/balance, review, and action primitives.
- Guided Payment, Token, UTXO, and BO forms with preload and explicit empty/error states.
- BO Merge and Split Payment become available; Rule Token creation remains explicitly delegated to its dedicated flow.
- Per-form/per-step imagegen proposals guide implementation but do not count as proof.
- All flows are visually and functionally validated in AccountApp WASM and repaired until passing.

## Hard Constraints

- Preserve explicit UTXO eligibility/domain behavior.
- Preserve dependency direction; no new project reference, partial class, or service locator.
- Do not overwrite unrelated dirty BO detail work.
- Use BaseLib components before custom structural CSS.

## Recommended Execution Order

1. `subbundles/01-transaction-inventory-and-ux-contract`
2. `subbundles/02-shared-transaction-interaction-foundation`
3. `subbundles/03-payment-token-and-utxo-flows`
4. `subbundles/04-business-object-transaction-wizards`
5. `subbundles/05-accountapp-browser-validation-and-closure`

## Validation Summary

- Bundle preparation status: `Prepared-stage validator passed`
- Bundle readiness gate: `Pass after manual input/dependency/proof/architecture audit`
- Execution status: `All subbundles completed`
- Subbundle gate review: `SB01-SB05 closed`
- Final closure gate: `Pass with CodeAnalytics validation gap`
- Browser validation analytics: `Final AccountApp WASM visual pass complete at 1920x1080; populated lookup/card semantics have direct BUnit proof`
- CodeAnalytics: `Unavailable; failed zero-project snapshot recorded`
