# C# Testability Plan

## Characterization

- Preserve current draft mutation behavior for Payment and UTXO Merge/Split.
- Preserve adapter request assembly tests for every supported mode.

## Isolated Tests

- Flow catalog covers every enum member and delegates `CreateRuleToken` explicitly.
- Validation rules test valid, missing, malformed, duplicate, and over-balance cases.
- BO source picker tests mine/address/direct-id strategies, selection, and empty/not-found states.
- Each mode form updates the draft and validation callback without rendering the full composer.

## Negative Tests

- Invalid JSON, invalid address, zero/excess amount, no UTXO, duplicate BO merge source, empty split outputs, and mismatched plan block submission.

## Composition Smoke

- Composer changes active flow from catalog and enables Submit only after the form reports valid.
- AccountApp dialog receives workspace BOs/balance/tokens/UTXOs and renders all supported/delegated entries.

## Architecture Proof

- `TransactionComposerShell.razor` shrinks and no longer contains mode-specific input markup.
- No partial class is added.
- Direct BUnit tests instantiate extracted components.

