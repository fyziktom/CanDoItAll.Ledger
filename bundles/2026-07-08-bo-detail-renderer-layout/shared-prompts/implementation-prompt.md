# Implementation Prompt

Implement the current subbundle only.

Rules:

- Prefer BaseLib components (`Grid`, `Stack`, `Cluster`, `StatusBadge`, `SummaryTile`, existing panel/fact classes) and existing Tailwind utilities.
- Do not add new CSS files or new custom selectors.
- Keep detail work in `CanDoItAll.Economy.Components`.
- Keep domain parsing in `CanDoItAll.Economy.BusinessObjects` unless a missing domain contract is proven.
- Do not add partial classes.
- Reuse one gallery-card component for both gallery page cards and related BO cards.
- Component tests must prove no-DataItems behavior and media behavior separately.

