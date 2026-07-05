# Target Solution

## End State

- `EconomyAccountWorkspace` renders one compact Balances panel for main balance, token balances, and real token-source rows.
- Token-source rows use the existing domain predicate: token output, no attached BO, and token amount greater than `1`.
- Unique token holdings remain visible only through token type or balance summaries where appropriate; they are not called token sources.
- The former Token Sources panel space is removed so the top work grid uses fewer cards and preserves more vertical space.
- The in-home action group uses visible short labels, while the AccountApp top frame remains icon-only.

## Boundaries

- Do not change ledger storage, transaction building, token minting, or BO source selection rules.
- Do not change top-frame `PageHeaderActionButton` behavior globally unless the change is opt-in and top-frame actions remain icon-only.
- Prefer local page/component CSS and existing shared components over a new shared component abstraction.
- Keep tests scoped to `EconomyAccountWorkspace` unless implementation moves behavior into a reusable helper.

## Proof Strategy

- Add focused bUnit tests that fail against the current over-broad token-source display and hidden home action labels.
- Run targeted tests and the AccountApp build.
- Record source assertions for the real token-source predicate and label visibility.
- Capture a large-screen browser screenshot of the unlocked home page when the local AccountApp can be run.
