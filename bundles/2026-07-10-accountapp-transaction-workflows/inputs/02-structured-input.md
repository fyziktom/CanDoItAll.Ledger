# Structured Input

## Objectives

1. Replace the flat transaction tab strip and raw-field experience with clear, data-assisted transaction flows.
2. Build reusable transaction UI primitives for mode choice, steps, source selection, entity cards, address/balance inputs, validation, review, and submission.
3. Give every visible composer mode an interactive form with preload, selection, or validation appropriate to its domain.
4. Add the currently adapter-supported BO merge and split-payment modes to the composer; keep `CreateRuleToken` in its existing dedicated token-creation flow unless the implementation proves a safe shared entry point.
5. Implement the clone flow as a wizard that supports owned objects, address-owned objects, and direct object-id lookup.
6. Visually and functionally validate every transaction flow in AccountApp WASM at desktop, tablet, and mobile widths.

## Hard Constraints

- Generated mockups are planning evidence only, never shipped proof.
- Existing explicit plain-UTXO Merge/Split behavior and eligibility rules must remain intact.
- No raw JSON or identifier field may be the only way to perform an operation when the workspace already has selectable data.
- Submit must remain disabled until the active flow's required inputs are valid.
- No new partial classes, project references, or service-location patterns.
- Existing user edits in BO detail renderer files are out of scope and must be preserved.

## Visual Thesis

A calm, dense transaction workbench: one obvious current task, progressive disclosure, selectable ledger entities as compact cards, inline validation close to the field, and a persistent plain-language review summary.

## Content Plan

- Choose: transaction families and operation cards.
- Configure: mode-specific data selection and fields.
- Review: consequences, selected objects/UTXOs, balances, recipients, and validation.
- Submit: one enabled primary action with clear success/failure feedback.

## Interaction Thesis

- Mode changes use a short content transition and preserve compatible draft values.
- Selection cards visibly move between available and selected states.
- Validation and the review summary update immediately; advanced raw fields remain collapsible.

