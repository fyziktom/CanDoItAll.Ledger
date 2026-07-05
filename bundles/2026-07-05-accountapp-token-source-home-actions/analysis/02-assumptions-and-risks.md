# Assumptions And Risks

## Assumptions

- The unlocked AccountApp workspace is the requested home page surface.
- Main currency source display can be represented by the existing main balance row unless implementation discovers a current main-source row pattern already exists elsewhere.
- Token-source display should align with the existing domain predicate: non-BO token output with amount greater than `1`.
- The separate permission grants subsection is not a user-requested token source and can be removed from the compact home card if it conflicts with the one-card requirement.

## Critical Path Risks

- SB01 is the critical foundation because a weak token-source predicate would keep showing unique BO tokens while later visual proof could still look superficially compact.
- If SB01 changes only text labels without filtering BO-backed or one-amount token outputs, N001 remains unsolved and SB02 browser proof is invalid.
- If action labels are implemented by changing top-frame toolbar behavior globally, N003 regresses the explicit icon-only constraint.

## Validation Risks

- Browser proof requires a runnable AccountApp state with an unlocked or importable demo account; if local browser setup is unavailable, execution must record the blocker and retain component-test proof.
- Generated Bootstrap or vendor assets can pollute broad source searches; validation should use targeted `rg` globs and source assertions.
- The adjacent CanDoItAll.Components repo may be referenced by the AccountApp project, but this bundle should avoid cross-repo component changes unless the local page cannot satisfy N003.

## Reopen Triggers

- Reopen SB01 if tests or browser evidence show `TokenSourceOutputs` still includes `Token.Amount == 1` or `BusinessObject is not null`.
- Reopen SB01 if the Balances card does not contain the real token-source outpoints after the separate source card is removed.
- Reopen SB02 if `.account-app__frame-actions` gains visible text or if `.cda-vef-account-actions` still hides button text.
- Reopen SB02 if large-screen visual proof shows labels overflowing, wrapping incoherently, or pushing the primary workspace down more than the removed card saves.
